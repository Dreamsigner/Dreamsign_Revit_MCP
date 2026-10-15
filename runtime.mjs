import {readFileSync,writeFileSync,mkdirSync,readdirSync,renameSync} from 'node:fs';
import {randomUUID} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {win32} from 'node:path';
const root=new URL('./',import.meta.url),reports=new URL('./reports/',root),stateFile=new URL('./project-state.json',root);
export function enumeratePids(){try{return readdirSync('\\\\.\\pipe\\').map(n=>/^AstraRevitBridge-2027-(\d+)$/.exec(n)?.[1]).filter(Boolean).map(Number);}catch{return [];}}
export async function resolveTarget(target,send,enumerate=enumeratePids){
 if(!target.document||!target.path||!win32.isAbsolute(target.path))throw Error('Explicit project name and absolute path required');
 const candidates=[...new Set([...enumerate(),target.pid].filter(n=>Number.isSafeInteger(n)&&n>0))];if(candidates.length>8)throw Error('Too many bridge instances; narrow the target');
 const matches=[];let outdated=false;
 for(const pid of candidates){const r=await send('get_active_document',{}, {...target,pid});if(r.success&&r.document===target.document){if(!r.path){outdated=true;continue;}if(r.path.replaceAll('/','\\').toLowerCase()===target.path.replaceAll('/','\\').toLowerCase())matches.push({pid,document:r.document,path:r.path,bridge_version:r.bridge_version});}}
 if(matches.length>1)return {success:false,code:'AMBIGUOUS_TARGET',message:'Multiple open instances match the configured project; no write executed',instances:matches};
 if(!matches.length)return {success:false,code:outdated?'UPGRADE_REQUIRED':'TARGET_UNAVAILABLE',message:outdated?'Revit must load bridge v4':'Open the configured project '+target.document+' in Revit 2027'};
 return {success:true,...matches[0]};
}
export function compactResult(result,reportPath){
 if(!result.results&&!result.actions&&!result.before)return result;
 const {results=[],actions,before,...summary}=result;const errors=results.map((r,i)=>({...r,action_index:r.action_index??i})).filter(r=>r.success===false);
 return {...summary,result_count:results.length,errors:errors.slice(0,5),errors_truncated:errors.length>5,detail_report:reportPath};
}
function state(){try{return JSON.parse(readFileSync(stateFile,'utf8'));}catch(e){if(e.code==='ENOENT')throw Error('Copy project-state.example.json to project-state.json and configure the exact Revit document title and path.');throw e;}}
function persist(value){const temp=new URL('./project-state-'+randomUUID()+'.tmp',root);writeFileSync(temp,JSON.stringify(value,null,2));renameSync(temp,stateFile);}
export function createRuntime(send,options={}){
 const getTarget=options.getTarget??(()=>state().target),resolve=options.resolve??resolveTarget;
 return async function handle(operation,args){
  if(operation==='get_project_state'){const s=state();return {success:true,target:s.target,decisions:s.decisions,open_items:s.open_items,baseline:s.baseline,last_operation:s.last_operation??null,mapping_count:Object.keys(s.mapping??{}).length};}
  if(operation==='create_test_project'){
   const r=await send(operation,{name:args.name},{pid:args.pid,document:'',path:''});
   if(r.success){try{const previous=state();const backup=new URL('./project-state.before-test-'+randomUUID()+'.json',root);writeFileSync(backup,JSON.stringify(previous,null,2));r.previous_state_file=fileURLToPath(backup);persist({version:1,target:{pid:r.pid,document:r.document,path:r.path},decisions:{units:'meters',purpose:'isolated bridge test'},open_items:[],baseline:{live:false,counts:{}},mapping:{},mapping_status:'new_test_project'});}catch(e){r.telemetry_warning='Project created; local targeting state could not be persisted: '+e.message;}}return r;
  }
  const target=getTarget(),found=await resolve(target,send);
  if(operation==='discover_instances')return found;
  if(!found.success)return found;
  const features=['capture_snapshot','read_snapshot','diff_snapshots','preview_batch','apply_plan','set_window_sills','assign_marks','tag_rooms'];
  if(features.includes(operation)&&found.bridge_version!=='0.3.0')return {success:false,code:'UPGRADE_REQUIRED',message:'Load bridge v5 for this operation'};
  const {response_mode='compact',...input}=args;
  const r=await send(operation,input,{...target,pid:found.pid});
  if(r.code==='SAVE_FAILED')r.changes_applied=((r.created??0)+(r.modified??0)+(r.deleted??0))>0;
  const mutation=['create_elements','modify_elements','delete_elements','execute_batch','run_workflow','save_project','preview_batch','apply_plan','set_window_sills','assign_marks','tag_rooms'].includes(operation);
  if(!mutation)return r;
  // Persist the complete result before shortening the model-facing response.
  let reportPath;try{if(options.record){reportPath=options.record(operation,r);}else{
   mkdirSync(reports,{recursive:true});const id=r.request_id??randomUUID(),url=new URL(id+'.json',reports);writeFileSync(url,JSON.stringify({utc:new Date().toISOString(),operation,result:r},null,2));reportPath=fileURLToPath(url);
   const s=state();s.last_operation={utc:new Date().toISOString(),operation,success:r.success,request_id:id,detail_report:reportPath};s.mapping??={};
   if(!r.rolled_back&&r.status!=='preview')for(const row of r.results??[]){if(row.success&&row.source_id&&row.unique_id)s.mapping[row.source_id]={unique_id:row.unique_id,kind:row.class};if(row.success&&row.status==='deleted')for(const [key,v]of Object.entries(s.mapping))if(v.unique_id===row.identifier)delete s.mapping[key];}
   if(r.success&&operation==='run_workflow'){s.mapping_status='refresh_required_after_category_delete';}persist(s);}
  }catch(e){r.telemetry_warning='Result applied; local report/state could not be persisted: '+e.message;}
  return response_mode==='full'||!reportPath?r:compactResult(r,reportPath);
 };
}
