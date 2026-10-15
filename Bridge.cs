using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace AstraRevitBridge {
public class App : IExternalApplication {
    internal static string[] Operations;
    internal static readonly Queue<JsonObject> Errors = new Queue<JsonObject>();
    internal static readonly object Gate = new object();
    static CancellationTokenSource stop;
    static ExternalEvent evt;
    static Handler handler;
    static Task worker;
    public Result OnStartup(UIControlledApplication app) {
        var contract=JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(typeof(App).Assembly.Location),"command-registry.json"))).AsObject();if(contract["version"]?.ToString()!="0.3.0")throw new InvalidOperationException("Command registry version mismatch");Operations=contract["commands"].AsArray().Select(x=>x.GetValue<string>()).ToArray();
        app.ControlledApplication.DocumentClosing += (sender, args) => Handler.Forget(args.Document);
        app.ControlledApplication.DocumentChanged += (sender,args) => Handler.Changed(args.GetDocument());
        handler = new Handler(); evt = ExternalEvent.Create(handler); stop = new CancellationTokenSource();
        worker = Task.Run(() => Serve(stop.Token)); return Result.Succeeded;
    }
    public Result OnShutdown(UIControlledApplication app) { stop.Cancel(); evt.Dispose(); return Result.Succeeded; }
    internal static JsonObject Error(string code, string message) {
        var e = new JsonObject { ["success"] = false, ["code"] = code, ["message"] = message.Length > 400 ? message.Substring(0,400) : message };
        lock(Gate) { Errors.Enqueue((JsonObject)e.DeepClone()); while(Errors.Count>20) Errors.Dequeue(); } return e;
    }
    static async Task Serve(CancellationToken ct) {
        while(!ct.IsCancellationRequested) {
            try {
                using(var pipe = new NamedPipeServerStream("AstraRevitBridge-2027-"+Environment.ProcessId, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly)) {
                    await pipe.WaitForConnectionAsync(ct);
                    using(var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                        deadline.CancelAfter(45000);
                        var bytes = new List<byte>(); var b = new byte[1];
                        while(await pipe.ReadAsync(b.AsMemory(),deadline.Token)>0 && b[0]!=10) { bytes.Add(b[0]); if(bytes.Count>262144) throw new InvalidDataException("Request exceeds 256 KiB"); }
                        JsonObject response; string op="invalid", id=""; var start=DateTime.UtcNow;
                        try {
                            var req=JsonNode.Parse(Encoding.UTF8.GetString(bytes.ToArray())).AsObject();
                            op=req["operation"]?.GetValue<string>()??""; id=req["request_id"]?.GetValue<string>()??"";
                            if(!Operations.Contains(op)) throw new ArgumentException("Unknown operation");
                            var job=new Job { Request=req, Expires=DateTime.UtcNow.AddSeconds(25) };
                            handler.Pending=job;
                            var raised=evt.Raise();
                            if(raised!=ExternalEventRequest.Accepted && raised!=ExternalEventRequest.Pending) { Interlocked.CompareExchange(ref handler.Pending,null,job); throw new InvalidOperationException("Revit rejected ExternalEvent: "+raised); }
                            var winner=await Task.WhenAny(job.Done.Task,Task.Delay(30000,ct));
                            if(winner!=job.Done.Task) { Interlocked.CompareExchange(ref handler.Pending,null,job); response=Error("TIMEOUT_OUTCOME_UNKNOWN","Revit busy or operation still running. Read state before retrying a write."); }
                            else response=await job.Done.Task;
                        } catch(Exception ex) { response=Error("INVALID_REQUEST",ex.Message); }
                        response["request_id"]=id;
                        Log(op,id,start,response);
                        var output=Encoding.UTF8.GetBytes(response.ToJsonString()+"\n");
                        await pipe.WriteAsync(output.AsMemory(),deadline.Token);
                    }
                }
            } catch(OperationCanceledException) {} catch(Exception ex) { Error("TRANSPORT",ex.Message); await Task.Delay(200); }
        }
    }
    static void Log(string op,string id,DateTime start,JsonObject r) {
        try {
            var dir=Path.GetDirectoryName(typeof(App).Assembly.Location); var path=Path.Combine(dir,"bridge.log");
            if(File.Exists(path)&&new FileInfo(path).Length>262144) File.Move(path,path+".1",true);
            File.AppendAllText(path,new JsonObject { ["utc"]=start.ToString("o"),["request_id"]=id,["operation"]=op,["ms"]=(DateTime.UtcNow-start).TotalMilliseconds,["success"]=r["success"]?.DeepClone(),["number_of_elements"]=r["requested"]?.DeepClone()??r["count"]?.DeepClone(),["code"]=r["code"]?.DeepClone() }.ToJsonString()+"\n");
        } catch {} // Logging must never affect a transaction.
    }
}
class Job { public JsonObject Request; public DateTime Expires; public TaskCompletionSource<JsonObject> Done=new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously); }
class Identity { public string Token=Guid.NewGuid().ToString("N"); }
partial class Handler : IExternalEventHandler {
    internal Job Pending;
    static Dictionary<string,Identity> identities=new Dictionary<string,Identity>();
    static string Key(Document d) { return d.PathName+"|"+d.Title+"|"+d.ProjectInformation.UniqueId; }
    internal static void Forget(Document d) { identities.Remove(Key(d));DropCaptures(d); }
    static readonly Guid SchemaId=new Guid("aaac585c-99a7-4833-8be2-48ed4efcd375");
    public string GetName() { return "Astra validated Revit operations"; }
    public void Execute(UIApplication ui) {
        var j=Interlocked.Exchange(ref Pending,null); if(j==null) return;
        try { if(DateTime.UtcNow>j.Expires) throw new InvalidOperationException("Request expired before execution; no changes made");Context=ui; j.Done.TrySetResult(Dispatch(ui,j.Request)); }
        catch(Exception e) { j.Done.TrySetResult(App.Error("REVIT_ERROR",e.Message)); }
        finally{Context=null;}
    }
    static string Str(JsonObject o,string k,string fallback="") { return o[k]?.GetValue<string>()??fallback; }
    static string Token(Document d) { string k=Key(d);if(!identities.TryGetValue(k,out var identity)){identity=new Identity();identities.Add(k,identity);}return identity.Token; }
    static JsonObject Ok() { return new JsonObject{["success"]=true}; }
    static System.Reflection.Assembly adapterAssembly;
    static DateTime adapterStamp;
    static JsonObject Adapter(string method,Document d,JsonObject input) {
        var path=Path.Combine(Path.GetDirectoryName(typeof(App).Assembly.Location),"AstraBimAdapter.dll");
        var stamp=File.GetLastWriteTimeUtc(path);if(adapterAssembly==null||stamp!=adapterStamp){adapterAssembly=System.Reflection.Assembly.Load(File.ReadAllBytes(path));adapterStamp=stamp;}var asm=adapterAssembly;
        try {return (JsonObject)asm.GetType("AstraBimAdapter.Actions").GetMethod(method).Invoke(null,new object[]{d,input});}
        catch(System.Reflection.TargetInvocationException e){throw e.InnerException??e;}
    }
    static JsonObject Describe(Element e) {
        var o=new JsonObject{["id"]=e.Id.Value,["unique_id"]=e.UniqueId,["name"]=e.Name,["category"]=e.Category?.Name};
        if(e is Level l) o["elevation_m"]=UnitUtils.ConvertFromInternalUnits(l.Elevation,UnitTypeId.Meters);
        var source=Source(e); if(source!="") o["source_id"]=source; return o;
    }
    static Schema GetSchema() {
        var s=Schema.Lookup(SchemaId); if(s!=null)return s;
        var b=new SchemaBuilder(SchemaId);b.SetSchemaName("AstraSourceIdentity");b.SetReadAccessLevel(AccessLevel.Public);b.SetWriteAccessLevel(AccessLevel.Public);b.AddSimpleField("source_id",typeof(string));return b.Finish();
    }
    static string Source(Element e) { var s=Schema.Lookup(SchemaId);if(s==null)return "";var ent=e.GetEntity(s);return ent.IsValid()?ent.Get<string>(s.GetField("source_id")):""; }
    static Element Resolve(Document d,JsonNode n) {
        if(n==null)throw new ArgumentException("Missing element identifier");
        var str=n.ToString(); if(long.TryParse(str,out var id))return d.GetElement(new ElementId(id));return d.GetElement(str);
    }
    static JsonObject Dispatch(UIApplication ui,JsonObject req) {
        string op=Str(req,"operation");var a=req["arguments"]?.AsObject()??new JsonObject();var d=ui.ActiveUIDocument?.Document;
        if(op=="create_test_project"){
            if(d!=null)throw new InvalidOperationException("TEST_REQUIRES_HOME: do not switch away from an existing project");
            string name=Str(a,"name");if(name.Length<1||name.Length>80||!System.Text.RegularExpressions.Regex.IsMatch(name,"^[A-Za-z0-9_-]+$"))throw new ArgumentException("Invalid test name");
            string template=Environment.GetEnvironmentVariable("ASTRA_REVIT_TEST_TEMPLATE");if(string.IsNullOrWhiteSpace(template)||!File.Exists(template)||!template.EndsWith(".rte",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Set ASTRA_REVIT_TEST_TEMPLATE to an existing RTE before starting Revit");string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DreamsignRevitMCP","test-projects");Directory.CreateDirectory(dir);string path=Path.Combine(dir,name+".rvt");if(File.Exists(path))throw new InvalidOperationException("Test file exists; refusing overwrite");
            var test=ui.Application.NewProjectDocument(template);try{test.SaveAs(path,new SaveAsOptions{OverwriteExistingFile=false});}finally{test.Close(false);}var opened=ui.OpenAndActivateDocument(path).Document;
            return new JsonObject{["success"]=true,["document"]=opened.Title,["path"]=opened.PathName,["pid"]=Environment.ProcessId,["document_token"]=Token(opened),["bridge_version"]="0.3.0",["template"]=template};
        }
        if(Str(req,"expected_document")==""||d==null||d.Title!=Str(req,"expected_document"))throw new InvalidOperationException("WRONG_DOCUMENT: target document must be active; no action executed");
        string expectedPath=Str(req,"expected_document_path");if(expectedPath!=""&&!string.Equals(Path.GetFullPath(d.PathName),Path.GetFullPath(expectedPath),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("WRONG_DOCUMENT_PATH: no action executed");
        if(op=="ping_revit") return new JsonObject{["success"]=true,["pid"]=Environment.ProcessId,["revit_running"]=true,["addin_loaded"]=true,["available"]=true,["document_open"]=d!=null,["revit_version"]=ui.Application.VersionNumber,["build"]=ui.Application.VersionBuild,["previous_error"]=App.Errors.Count>0};
        if(op=="get_last_errors") {lock(App.Gate)return new JsonObject{["success"]=true,["errors"]=new JsonArray(App.Errors.Select(x=>(JsonNode)x.DeepClone()).ToArray())};}
        if(d==null)throw new InvalidOperationException("NO_ACTIVE_DOCUMENT");
        if(op=="get_active_document")return new JsonObject{["success"]=true,["document"]=d.Title,["path"]=d.PathName,["pid"]=Environment.ProcessId,["bridge_version"]="0.3.0",["revision"]=Revision(d),["document_token"]=Token(d),["revit_version"]=ui.Application.VersionNumber,["read_only"]=d.IsReadOnly,["is_family"]=d.IsFamilyDocument};
        if(FeatureOperations.Contains(op))return FeatureDispatch(ui,d,op,a);
        if(op=="get_project_info"){if(a["save_document"]?.GetValue<bool>()==true)throw new ArgumentException("Use save_project for writes");var r=Adapter("ProjectInfo",d,a);r["document_token"]=Token(d);return r;}
        if(op=="save_project"||op=="run_workflow"){
            if(d.IsReadOnly||d.IsFamilyDocument||d.IsModifiable)throw new InvalidOperationException("Document unavailable for write");
            if(op=="save_project"||a["document_token"]!=null){if(Str(a,"document_token")!=Token(d))throw new InvalidOperationException("DOCUMENT_CHANGED");}
            if(op=="save_project")return Save(d,Ok());
            if(expectedPath=="")throw new ArgumentException("Workflow requires expected document path");
            if(Str(a,"workflow")!="remove_tags")throw new ArgumentException("Unsupported workflow");
            string category=Str(a,"category");if(!new[]{"DoorTags","WindowTags","RoomTags"}.Contains(category))throw new ArgumentException("Only annotation tag categories allowed");
            var tagFilter=new JsonObject{["category"]=category,["limit"]=100};if(a["source_id"]!=null)tagFilter["source_id"]=a["source_id"].DeepClone();var selected=Query(d,tagFilter);int count=selected["count"].GetValue<int>();if(count>250)throw new ArgumentException("Workflow limit 250 tags; no changes made");
            var tagActions=new List<JsonObject>();for(int offset=0;offset<count;offset+=100){tagFilter["offset"]=offset;var page=Query(d,tagFilter);foreach(var row in page["elements"].AsArray())tagActions.Add(new JsonObject{["operation"]="delete",["id"]=row["unique_id"].DeepClone()});}
            var result=count==0?new JsonObject{["success"]=true,["deleted"]=0}:Write(d,tagActions,"atomic");if(result["success"]?.GetValue<bool>()!=true)return result;
            tagFilter["count_only"]=true;result["remaining"]=Query(d,tagFilter)["count"].DeepClone();if(result["remaining"].GetValue<int>()!=0){result["success"]=false;result["code"]="VERIFICATION_FAILED";return result;}
            result["workflow"]="remove_tags";result["category"]=category;return a["save"]?.GetValue<bool>()==false?result:Save(d,result);
        }
        if(op=="get_levels") { var list=new FilteredElementCollector(d).OfClass(typeof(Level)).Cast<Level>().OrderBy(x=>x.Elevation).ToList();return new JsonObject{["success"]=true,["count"]=list.Count,["elements"]=new JsonArray(list.Take(100).Select(x=>(JsonNode)Describe(x)).ToArray()),["truncated"]=list.Count>100}; }
        if(op=="query_elements") return Query(d,a["filter"]?.AsObject()??new JsonObject());
        if(op=="validate_elements") {var ids=Ids(a);var rows=new JsonArray();foreach(var n in ids){var e=Resolve(d,n);rows.Add(new JsonObject{["identifier"]=n?.DeepClone(),["exists"]=e!=null,["element"]=e==null?null:Describe(e)});}return new JsonObject{["success"]=true,["elements"]=rows};}
        if(!new[]{"create_elements","modify_elements","delete_elements","execute_batch"}.Contains(op))throw new ArgumentException("Unknown operation");
        if(Str(a,"document_token")!=Token(d))throw new InvalidOperationException("DOCUMENT_CHANGED: supply document_token from get_active_document");
        if(d.IsReadOnly||d.IsFamilyDocument||d.IsModifiable)throw new InvalidOperationException("Document unavailable for project transaction");
        var actions=new List<JsonObject>();
        if(op=="execute_batch") {var arr=a["actions"]?.AsArray()??throw new ArgumentException("actions required");foreach(var item in arr)actions.Add(item.AsObject());}
        else Expand(op,a,actions);
        if(actions.Count==0||actions.Count>250)throw new ArgumentException("Batch requires 1..250 actions");
        return Write(d,actions,Str(a,"strategy","atomic"));
    }
    static JsonObject Save(Document d,JsonObject result){try{d.Save();result["saved"]=true;}catch(Exception e){result["success"]=false;result["saved"]=false;result["code"]="SAVE_FAILED";result["message"]=e.Message;result["changes_applied"]=(result["deleted"]?.GetValue<int>()??0)>0;}return result;}
    static JsonArray Ids(JsonObject a){var ids=a["ids"]?.AsArray()??throw new ArgumentException("ids required");if(ids.Count<1||ids.Count>250)throw new ArgumentException("ids requires 1..250 identifiers");return ids;}
    static void Expand(string op,JsonObject a,List<JsonObject> list) {
        if(op=="create_elements") {foreach(var s in a["spec"]?.AsArray()??throw new ArgumentException("spec array required"))list.Add(new JsonObject{["operation"]="create",["spec"]=s.DeepClone()});}
        else foreach(var id in Ids(a))list.Add(new JsonObject{["operation"]=op=="modify_elements"?"modify":"delete",["id"]=id.DeepClone(),["changes"]=a["changes"]?.DeepClone()});
    }
    static JsonObject Query(Document d,JsonObject f) {
        var allowed=new[]{"category","level","name","source_id","limit","offset","types","count_only","detail","class","fields","view_id","active_view","selected_only","type_id","family","box_min_m","box_max_m","predicates"};foreach(var kv in f)if(!allowed.Contains(kv.Key))throw new ArgumentException("Unknown filter: "+kv.Key);
        int limit=f["limit"]?.GetValue<int>()??25, offset=f["offset"]?.GetValue<int>()??0;if(limit<1||limit>100||offset<0)throw new ArgumentException("limit 1..100; offset >=0");
        var view=f["active_view"]?.GetValue<bool>()==true?Context?.ActiveUIDocument.ActiveView:(f["view_id"]!=null?Resolve(d,f["view_id"]) as View:null);if((f["view_id"]!=null||f["active_view"]?.GetValue<bool>()==true)&&(view==null||view.IsTemplate||!FilteredElementCollector.IsViewValidForElementIteration(d,view.Id)))throw new ArgumentException("Iterable project view required");
        if(view!=null&&f["types"]?.GetValue<bool>()==true)throw new ArgumentException("View scope only supports instances");var c=view==null?new FilteredElementCollector(d):new FilteredElementCollector(d,view.Id);if(f["types"]?.GetValue<bool>()==true)c.WhereElementIsElementType();else c.WhereElementIsNotElementType();
        if(f["box_min_m"]!=null||f["box_max_m"]!=null){if(f["box_min_m"]==null||f["box_max_m"]==null)throw new ArgumentException("Both box corners required");var min=P(f["box_min_m"]);var max=P(f["box_max_m"]);if(min.X>max.X||min.Y>max.Y||min.Z>max.Z)throw new ArgumentException("Reversed bounding box");c.WherePasses(new BoundingBoxIntersectsFilter(new Outline(min,max)));}
        string cls=Str(f,"class");if(cls!=""){var classes=new Dictionary<string,Type>{{"ImportInstance",typeof(ImportInstance)},{"ViewPlan",typeof(ViewPlan)},{"Grid",typeof(Grid)},{"FamilySymbol",typeof(FamilySymbol)}};if(!classes.ContainsKey(cls))throw new ArgumentException("Unsupported class");c.OfClass(classes[cls]);}
        string cat=Str(f,"category");if(cat!=""){if(!cat.StartsWith("OST_"))cat="OST_"+cat;if(!Enum.TryParse<BuiltInCategory>(cat,out var bc)||!Enum.IsDefined(typeof(BuiltInCategory),bc))throw new ArgumentException("Use built-in category, e.g. Walls or OST_Walls");c.OfCategory(bc);}
        string level=Str(f,"level");if(level!=""){var ls=new FilteredElementCollector(d).OfClass(typeof(Level)).Cast<Level>().Where(x=>x.Name==level).ToList();if(ls.Count!=1)throw new ArgumentException("Level must match exactly one name");c.WherePasses(new ElementLevelFilter(ls[0].Id));}
        IEnumerable<Element> q=c;string name=Str(f,"name"),source=Str(f,"source_id");if(name!="")q=q.Where(e=>e.Name==name);if(source!="")q=q.Where(e=>Source(e)==source);
        if(f["selected_only"]?.GetValue<bool>()==true){if(Context==null)throw new ArgumentException("Selection context unavailable");var selected=new HashSet<long>(Context.ActiveUIDocument.Selection.GetElementIds().Select(x=>x.Value));q=q.Where(e=>selected.Contains(e.Id.Value));}
        if(f["type_id"]!=null){var type=Resolve(d,f["type_id"]);if(!(type is ElementType))throw new ArgumentException("type_id must identify a type");q=q.Where(e=>e.GetTypeId()==type.Id);}
        if(f["family"]!=null)q=q.Where(e=>(e is FamilyInstance fi?fi.Symbol.FamilyName:e is FamilySymbol fs?fs.FamilyName:"")==Str(f,"family"));
        if(f["predicates"]!=null){var rules=f["predicates"].AsArray().Select(x=>x.AsObject()).ToList();if(rules.Count>8)throw new ArgumentException("Predicate limit 8");q=q.Where(e=>rules.All(rule=>Matches(e,rule)));}
        string[] fields=f["fields"]?.AsArray().Select(x=>x.GetValue<string>()).ToArray();bool detail=f["detail"]?.GetValue<bool>()==true||fields?.Any(x=>!new[]{"id","unique_id","name","category","source_id","elevation_m"}.Contains(x))==true;
        int count=0;var rows=new JsonArray();bool countOnly=f["count_only"]?.GetValue<bool>()??false;foreach(var e in q){if(!countOnly&&count>=offset&&rows.Count<limit){var row=detail?Adapter("Describe",d,new JsonObject{["id"]=e.UniqueId}):Describe(e);if(fields!=null){var projected=new JsonObject();foreach(var key in fields)projected[key]=row[key]?.DeepClone();row=projected;}rows.Add(row);}count++;}
        return new JsonObject{["success"]=true,["count"]=count,["elements"]=rows,["next_offset"]=!countOnly&&offset+rows.Count<count?(int?)(offset+rows.Count):null};
    }
    static JsonObject Write(Document d,List<JsonObject> actions,string strategy) {
        if(strategy!="atomic"&&strategy!="independent")throw new ArgumentException("strategy: atomic or independent");
        var rows=new JsonArray();int failed=0;
        using(var group=new TransactionGroup(d,"Astra batch")) {
            if(group.Start()!=TransactionStatus.Started)throw new InvalidOperationException("Cannot start group");
            foreach(var a in actions) {
                using(var t=new Transaction(d,"Astra "+Str(a,"operation"))) {
                    try {
                        t.Start();var guard=new FailureGuard();t.SetFailureHandlingOptions(t.GetFailureHandlingOptions().SetFailuresPreprocessor(guard).SetClearAfterRollback(true));
                        var row=Act(d,a);d.Regenerate();var status=t.Commit();
                        if(status!=TransactionStatus.Committed)throw new InvalidOperationException("Transaction rolled back: "+guard.Message);
                        row["action_index"]=rows.Count;if(a["id"]!=null)row["identifier"]=a["id"].DeepClone();rows.Add(row);
                    } catch(Exception ex) {
                        if(t.GetStatus()==TransactionStatus.Started)t.RollBack();failed++;var error=App.Error("ACTION_FAILED",ex.Message);error["action_index"]=rows.Count;error["source_id"]=a["spec"]?["source_id"]?.DeepClone();rows.Add(error);
                        if(strategy=="atomic") {group.RollBack();return new JsonObject{["success"]=false,["code"]="BATCH_ROLLED_BACK",["requested"]=actions.Count,["committed"]=0,["failed"]=failed,["rolled_back"]=true,["reverted_actions"]=rows.Count-1,["error"]=rows.Last().DeepClone()};}
                    }
                }
            }
            if(group.Assimilate()!=TransactionStatus.Committed)throw new InvalidOperationException("Group not committed");
        }
        return new JsonObject{["success"]=failed==0,["requested"]=actions.Count,["committed"]=actions.Count-failed,["created"]=rows.Count(x=>x?["status"]?.ToString()=="created"),["modified"]=rows.Count(x=>new[]{"updated","modified"}.Contains(x?["status"]?.ToString())),["unchanged"]=rows.Count(x=>x?["status"]?.ToString()=="unchanged"),["deleted"]=rows.Count(x=>x?["status"]?.ToString()=="deleted"),["failed"]=failed,["results"]=rows};
    }
    static JsonObject Act(Document d,JsonObject a) {
        string op=Str(a,"operation");
        if(op=="create") {
            var s=a["spec"]?.AsObject()??throw new ArgumentException("spec required");
            if(Str(s,"kind")!="level")return Adapter("Create",d,s);
            string source=Str(s,"source_id"),name=Str(s,"name");if(source.Length<1||source.Length>128||name.Length<1)throw new ArgumentException("source_id (1..128) and name required");
            double z=s["elevation_m"]?.GetValue<double>()??throw new ArgumentException("elevation_m required");if(!double.IsFinite(z)||Math.Abs(z)>10000)throw new ArgumentException("Invalid elevation_m");
            var existing=new FilteredElementCollector(d).WherePasses(new ExtensibleStorageFilter(SchemaId)).Where(e=>Source(e)==source).ToList();if(existing.Count>1)throw new InvalidOperationException("Duplicate source_id in model");
            var level=existing.SingleOrDefault() as Level;if(existing.Count==1&&level==null)throw new InvalidOperationException("source_id belongs to different kind");
            if(new FilteredElementCollector(d).OfClass(typeof(Level)).Cast<Level>().Any(x=>x.Name==name&&(level==null||x.Id!=level.Id)))throw new InvalidOperationException("Name already exists without matching source_id; refusing duplicate");
            bool created=level==null;bool changed=created||level.Name!=name||Math.Abs(level.Elevation-UnitUtils.ConvertToInternalUnits(z,UnitTypeId.Meters))>1e-8;
            if(created)level=Level.Create(d,UnitUtils.ConvertToInternalUnits(z,UnitTypeId.Meters));else if(changed)level.Elevation=UnitUtils.ConvertToInternalUnits(z,UnitTypeId.Meters);
            if(level.Name!=name)level.Name=name;var schema=GetSchema();var ent=new Entity(schema);ent.Set<string>(schema.GetField("source_id"),source);level.SetEntity(ent);
            var result=Describe(level);result["status"]=created?"created":changed?"updated":"unchanged";result["success"]=true;return result;
        }
        var e=Resolve(d,a["id"]);if(e==null)throw new ArgumentException("Element not found");
        if(op=="modify") {
            var c=a["changes"]?.AsObject()??throw new ArgumentException("changes required");if(c.Count==0)throw new ArgumentException("Empty changes");
            foreach(var kv in c) {
                if(kv.Key=="sill_m"){if(!(e is FamilyInstance)||e.Category.Id.Value!=(long)BuiltInCategory.OST_Windows)throw new ArgumentException("Sill writes require a window");double value=kv.Value.GetValue<double>();if(!double.IsFinite(value)||value<0||value>10)throw new ArgumentException("Invalid sill");var p=e.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);if(p==null||p.IsReadOnly)throw new ArgumentException("Writable sill missing");p.Set(Ft(value));if(Math.Abs(p.AsDouble()-Ft(value))>1e-7)throw new InvalidOperationException("Sill verification failed");}
                else if(kv.Key=="mark"){if(e.Category==null||!new[]{(long)BuiltInCategory.OST_Doors,(long)BuiltInCategory.OST_Windows}.Contains(e.Category.Id.Value))throw new ArgumentException("Mark writes require door/window");var p=e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);if(p==null||p.IsReadOnly)throw new ArgumentException("Writable mark missing");p.Set(kv.Value.GetValue<string>());if(p.AsString()!=kv.Value.GetValue<string>())throw new InvalidOperationException("Mark verification failed");}
                else if(kv.Key=="name")e.Name=kv.Value.GetValue<string>();
                else if(kv.Key=="elevation_m"&&e is Level l){double z=kv.Value.GetValue<double>();if(!double.IsFinite(z)||Math.Abs(z)>10000)throw new ArgumentException("Invalid elevation");l.Elevation=UnitUtils.ConvertToInternalUnits(z,UnitTypeId.Meters);}
                else throw new ArgumentException("Unsupported change: "+kv.Key);
            }var r=Describe(e);r["success"]=true;r["status"]="modified";return r;
        }
        if(op=="delete") {
            var deleted=d.Delete(e.Id);if(deleted.Count!=1)throw new InvalidOperationException("Deletion would also remove dependent elements; rolled back");
            return new JsonObject{["success"]=true,["status"]="deleted",["deleted"]=deleted.Count};
        }
        throw new ArgumentException("Unsupported batch operation: "+op);
    }
}
class FailureGuard : IFailuresPreprocessor {
    public string Message="";
    public FailureProcessingResult PreprocessFailures(FailuresAccessor a) {
        var failures=a.GetFailureMessages();if(failures.Count==0)return FailureProcessingResult.Continue;
        Message=string.Join("; ",failures.Take(3).Select(x=>x.GetDescriptionText()));return FailureProcessingResult.ProceedWithRollBack;
    }
}
}
