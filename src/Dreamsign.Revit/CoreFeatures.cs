using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Architecture;
namespace AstraRevitBridge
{
    partial class Handler
    {
        internal static readonly string[] FeatureOperations = { "capture_snapshot", "read_snapshot", "diff_snapshots", "preview_batch", "apply_plan", "set_window_sills", "assign_marks", "tag_rooms" };
        static UIApplication Context;
        static Dictionary<string, long> revisions = new Dictionary<string, long>();
        class Capture
        {
            public string Key, Token, Scope; public long Revision; public DateTime Created = DateTime.UtcNow, Expires; public JsonObject Data; public JsonArray Actions; public long? View; public string Selection;
        }
        static bool InPreview;
        static Dictionary<string, Capture> snapshots = new Dictionary<string, Capture>(), plans = new Dictionary<string, Capture>();
        static long Revision(Document d)
        {
            return revisions.TryGetValue(Key(d), out var value) ? value : 0;
        }
        internal static void Changed(Document d)
        {
            if (InPreview)
                return;
            string key = Key(d);
            revisions[key] = Revision(d) + 1;
        }
        static void DropCaptures(Document d)
        {
            string key = Key(d);
            foreach (var dict in new[] { snapshots, plans })
                foreach (var id in dict.Where(p => p.Value.Key == key).Select(p => p.Key).ToArray())
                    dict.Remove(id);
            revisions.Remove(key);
        }
        static double Ft(double m)
        {
            return UnitUtils.ConvertToInternalUnits(m, UnitTypeId.Meters);
        }
        static XYZ P(JsonNode n)
        {
            var a = n.AsArray();
            if (a.Count != 3)
                throw new ArgumentException("Three coordinates required");
            return new XYZ(Ft(a[0].GetValue<double>()), Ft(a[1].GetValue<double>()), Ft(a[2].GetValue<double>()));
        }
        static string Selection(UIApplication ui)
        {
            return string.Join(",", ui.ActiveUIDocument.Selection.GetElementIds().Select(x => x.Value).OrderBy(x => x));
        }
        static double? Measure(Element e, string field)
        {
            if (field == "area_m2")
                return e is Room r ? (double?)UnitUtils.ConvertFromInternalUnits(r.Area, UnitTypeId.SquareMeters) : null;
            if (field == "width_m" && e is Wall w)
                return UnitUtils.ConvertFromInternalUnits(w.WallType.Width, UnitTypeId.Meters);
            BuiltInParameter bi = field == "height_m" ? (e is Wall ? BuiltInParameter.WALL_USER_HEIGHT_PARAM : BuiltInParameter.FAMILY_HEIGHT_PARAM) : field == "width_m" ? BuiltInParameter.FAMILY_WIDTH_PARAM : field == "sill_m" ? BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM : throw new ArgumentException("Unsupported measured field");
            var p = e.get_Parameter(bi);
            if (p == null && e is FamilyInstance fi)
                p = fi.Symbol.get_Parameter(bi);
            return p != null && p.StorageType == StorageType.Double && p.HasValue ? (double?)UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Meters) : null;
        }
        static bool Matches(Element e, JsonObject rule)
        {
            double? value = Measure(e, Str(rule, "field"));
            double target = rule["value"].GetValue<double>();
            if (!double.IsFinite(target))
                throw new ArgumentException("Finite predicate value required");
            if (value == null)
                return false;
            switch (Str(rule, "op"))
            {
                case "gt":
                    return value > target;
                case "gte":
                    return value >= target;
                case "lt":
                    return value < target;
                case "lte":
                    return value <= target;
                case "eq":
                    return Math.Abs(value.Value - target) < 1e-7;
                default:
                    throw new ArgumentException("Unsupported comparison");
            }
        }
        static void BoundedStore(Dictionary<string, Capture> store, string id, Capture capture)
        {
            foreach (var key in store.Where(p => p.Value.Expires < DateTime.UtcNow).Select(p => p.Key).ToArray())
                store.Remove(key);
            if (store.Count >= 8)
                store.Remove(store.OrderBy(p => p.Value.Expires).First().Key);
            store[id] = capture;
        }
        static Capture GetCapture(Dictionary<string, Capture> store, string id, Document d, UIApplication ui)
        {
            if (!store.TryGetValue(id, out var c))
                throw new InvalidOperationException("CAPTURE_NOT_FOUND");
            if (c.Expires < DateTime.UtcNow || DateTime.UtcNow < c.Created)
            {
                store.Remove(id);
                throw new InvalidOperationException("CAPTURE_EXPIRED");
            }
            if (c.Key != Key(d) || c.Token != Token(d) || c.Revision != Revision(d) || (c.View != null && ui.ActiveUIDocument.ActiveView.Id.Value != c.View) || (c.Selection != null && Selection(ui) != c.Selection))
                throw new InvalidOperationException("CAPTURE_STALE: document, revision, view or selection changed");
            return c;
        }
        static JsonObject Preview(UIApplication ui, Document d, JsonArray actions, bool save)
        {
            if (d.IsReadOnly || d.IsFamilyDocument || d.IsModifiable)
                throw new InvalidOperationException("Document unavailable");
            if (actions.Count < 1 || actions.Count > 250)
                throw new ArgumentException("Plan requires 1..250 actions");
            var before = new JsonArray();
            foreach (var action in actions)
            {
                if (Str(action.AsObject(), "operation") != "create")
                {
                    var e = Resolve(d, action["id"]);
                    if (e == null)
                        throw new ArgumentException("Plan target missing");
                    var row = Describe(e);
                    var values = new JsonObject();
                    foreach (var change in action["changes"]?.AsObject() ?? new JsonObject())
                    {
                        if (change.Key == "name")
                            values[change.Key] = e.Name;
                        else if (change.Key == "mark")
                            values[change.Key] = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
                        else if (change.Key == "sill_m")
                            values[change.Key] = Measure(e, "sill_m");
                        else if (change.Key == "elevation_m" && e is Level level)
                            values[change.Key] = UnitUtils.ConvertFromInternalUnits(level.Elevation, UnitTypeId.Meters);
                    }
                    row["values"] = values;
                    before.Add(row);
                }
            }
            JsonObject checkedResult;
            using (var dry = new TransactionGroup(d, "Astra preview (rollback)"))
            {
                dry.Start();
                InPreview = true;
                try
                {
                    checkedResult = Write(d, actions.Select(x => x.AsObject()).ToList(), "atomic");
                }
                finally { try { dry.RollBack(); } finally { InPreview = false; } }
            }
            if (checkedResult["success"]?.GetValue<bool>() != true)
                return checkedResult;
            string id = Guid.NewGuid().ToString("N");
            var data = new JsonObject { ["success"] = true, ["status"] = "preview", ["plan_id"] = id, ["requested"] = actions.Count, ["revision"] = Revision(d), ["expires_utc"] = DateTime.UtcNow.AddMinutes(5).ToString("o"), ["before"] = before, ["actions"] = actions.DeepClone(), ["save"] = save, ["changes_applied"] = false };
            BoundedStore(plans, id, new Capture { Key = Key(d), Token = Token(d), Revision = Revision(d), Expires = DateTime.UtcNow.AddMinutes(5), View = ui.ActiveUIDocument.ActiveView.Id.Value, Actions = (JsonArray)actions.DeepClone(), Data = data });
            return data;
        }
        static List<Element> Targets(Document d, JsonObject filter, string category)
        {
            foreach (var key in new[] { "limit", "offset", "types", "fields", "count_only", "detail" })
                if (filter[key] != null)
                    throw new ArgumentException("Procedure filters apply to all matches; pagination/projection flags not permitted: " + key);
            var f = (JsonObject)filter.DeepClone();
            if (f["category"] != null && Str(f, "category") != category && Str(f, "category") != "OST_" + category)
                throw new ArgumentException("Procedure category mismatch");
            f["category"] = category;
            f["count_only"] = true;
            int count = Query(d, f)["count"].GetValue<int>();
            if (count > 250)
                throw new ArgumentException("Procedure limit 250; no changes made");
            f["count_only"] = false;
            f["fields"] = new JsonArray("unique_id");
            f["limit"] = 100;
            var elements = new List<Element>();
            for (int offset = 0; offset < count; offset += 100)
            {
                f["offset"] = offset;
                foreach (var row in Query(d, f)["elements"].AsArray())
                    elements.Add(Resolve(d, row["unique_id"]));
            }
            return elements.OrderBy(e => e.UniqueId).ToList();
        }
        static JsonObject FeatureDispatch(UIApplication ui, Document d, string op, JsonObject a)
        {
            if (op == "capture_snapshot")
            {
                var snapshotFilter = (JsonObject)(a["filter"]?.DeepClone() ?? new JsonObject());
                if (snapshotFilter["count_only"]?.GetValue<bool>() == true)
                    throw new ArgumentException("Snapshot needs rows, not count_only");
                if (snapshotFilter["fields"] is JsonArray fields && !fields.Any(x => x.ToString() == "unique_id"))
                    fields.Add("unique_id");
                var data = Query(d, snapshotFilter);
                string id = Guid.NewGuid().ToString("N");
                BoundedStore(snapshots, id, new Capture { Key = Key(d), Token = Token(d), Scope = snapshotFilter.ToJsonString(), Revision = Revision(d), Expires = DateTime.UtcNow.AddMinutes(5), Data = (JsonObject)data.DeepClone(), View = snapshotFilter["active_view"]?.GetValue<bool>() == true ? ui.ActiveUIDocument.ActiveView.Id.Value : (long?)null, Selection = snapshotFilter["selected_only"]?.GetValue<bool>() == true ? Selection(ui) : null });
                return new JsonObject { ["success"] = true, ["snapshot_id"] = id, ["revision"] = Revision(d), ["count"] = data["count"].DeepClone(), ["captured_rows"] = data["elements"].AsArray().Count, ["next_offset"] = data["next_offset"]?.DeepClone(), ["expires_in_seconds"] = 300 };
            }
            if (op == "read_snapshot")
            {
                var c = GetCapture(snapshots, Str(a, "snapshot_id"), d, ui);
                var result = (JsonObject)c.Data.DeepClone();
                result["snapshot_id"] = Str(a, "snapshot_id");
                result["revision"] = c.Revision;
                result["from_cache"] = true;
                return result;
            }
            if (op == "diff_snapshots")
            {
                if (!snapshots.TryGetValue(Str(a, "before_id"), out var old) || old.Key != Key(d) || old.Token != Token(d) || old.Expires < DateTime.UtcNow || DateTime.UtcNow < old.Created)
                    throw new InvalidOperationException("Historical snapshot unavailable");
                var fresh = GetCapture(snapshots, Str(a, "after_id"), d, ui);
                if (old.Scope != fresh.Scope || old.View != fresh.View || old.Selection != fresh.Selection)
                    throw new ArgumentException("Snapshot scopes differ");
                var before = old.Data["elements"].AsArray().ToDictionary(x => x["unique_id"].ToString(), x => x.ToJsonString());
                var after = fresh.Data["elements"].AsArray().ToDictionary(x => x["unique_id"].ToString(), x => x.ToJsonString());
                var changed = after.Keys.Where(k => before.ContainsKey(k) && before[k] != after[k]).ToArray();
                return new JsonObject { ["success"] = true, ["scope"] = "captured_pages_only", ["before_revision"] = old.Revision, ["after_revision"] = fresh.Revision, ["entered_scope"] = new JsonArray(after.Keys.Except(before.Keys).Select(k => (JsonNode)JsonValue.Create(k)).ToArray()), ["left_scope"] = new JsonArray(before.Keys.Except(after.Keys).Select(k => (JsonNode)JsonValue.Create(k)).ToArray()), ["changed"] = new JsonArray(changed.Select(k => (JsonNode)JsonValue.Create(k)).ToArray()), ["partial"] = old.Data["next_offset"] != null || fresh.Data["next_offset"] != null };
            }
            if (Str(a, "document_token") != Token(d))
                throw new InvalidOperationException("DOCUMENT_CHANGED");
            if (d.IsReadOnly || d.IsFamilyDocument || d.IsModifiable)
                throw new InvalidOperationException("Document unavailable for project transaction");
            if (op == "apply_plan")
            {
                var c = GetCapture(plans, Str(a, "plan_id"), d, ui);
                plans.Remove(Str(a, "plan_id"));
                var result = Write(d, c.Actions.Select(x => x.AsObject()).ToList(), "atomic");
                result["plan_id"] = Str(a, "plan_id");
                if (result["success"]?.GetValue<bool>() == true && c.Data["save"]?.GetValue<bool>() == true)
                    result = Save(d, result);
                return result;
            }
            if (op == "preview_batch")
                return Preview(ui, d, a["actions"].AsArray(), a["save"]?.GetValue<bool>() == true);
            var filter = a["filter"]?.AsObject() ?? new JsonObject();
            var actions = new JsonArray();
            if (op == "set_window_sills")
            {
                double value = a["value_m"].GetValue<double>();
                if (!double.IsFinite(value) || value < 0 || value > 10)
                    throw new ArgumentException("Sill range 0..10m");
                foreach (var e in Targets(d, filter, "Windows"))
                    actions.Add(new JsonObject { ["operation"] = "modify", ["id"] = e.UniqueId, ["changes"] = new JsonObject { ["sill_m"] = value } });
            }
            else if (op == "assign_marks")
            {
                string cat = Str(a, "category");
                if (!new[] { "Doors", "Windows" }.Contains(cat))
                    throw new ArgumentException("Marks only supported for doors/windows");
                int index = a["start"]?.GetValue<int>() ?? 1;
                foreach (var e in Targets(d, filter, cat))
                {
                    if (a["overwrite"]?.GetValue<bool>() != true && !string.IsNullOrWhiteSpace(e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()))
                        continue;
                    actions.Add(new JsonObject { ["operation"] = "modify", ["id"] = e.UniqueId, ["changes"] = new JsonObject { ["mark"] = Str(a, "prefix") + (index++).ToString("D3") } });
                }
            }
            else if (op == "tag_rooms")
            {
                var view = Resolve(d, a["view_id"]) as ViewPlan;
                if (view == null || view.IsTemplate)
                    throw new ArgumentException("Project floorplan required");
                var type = Resolve(d, a["type_id"]) as FamilySymbol;
                if (type == null || type.Category.Id.Value != (long)BuiltInCategory.OST_RoomTags)
                    throw new ArgumentException("Room tag symbol required");
                var tagged = new HashSet<long>(new FilteredElementCollector(d, view.Id).OfCategory(BuiltInCategory.OST_RoomTags).WhereElementIsNotElementType().Cast<RoomTag>().Where(t => t.Room != null).Select(t => t.Room.Id.Value));
                foreach (var e in Targets(d, filter, "Rooms"))
                {
                    var room = e as Room;
                    if (room == null || room.Area <= 0 || room.LevelId != view.GenLevel.Id || tagged.Contains(room.Id.Value))
                        continue;
                    var p = ((LocationPoint)room.Location).Point;
                    actions.Add(new JsonObject { ["operation"] = "create", ["spec"] = new JsonObject { ["kind"] = "room_tag", ["source_id"] = "astra-roomtag-" + room.UniqueId + "-" + view.UniqueId, ["room_id"] = room.UniqueId, ["view_id"] = view.UniqueId, ["type_id"] = type.UniqueId, ["point_m"] = new JsonArray(UnitUtils.ConvertFromInternalUnits(p.X, UnitTypeId.Meters), UnitUtils.ConvertFromInternalUnits(p.Y, UnitTypeId.Meters), UnitUtils.ConvertFromInternalUnits(p.Z, UnitTypeId.Meters)) } });
                }
            }
            else
                throw new ArgumentException("Unsupported feature");
            if (actions.Count == 0)
                return new JsonObject { ["success"] = true, ["requested"] = 0, ["status"] = "no_changes", ["changes_applied"] = false };
            return Preview(ui, d, actions, a["save"]?.GetValue<bool>() != false);
        }
    }
}
