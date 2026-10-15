import net from 'node:net';
import {randomUUID} from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {selectTools} from './profiles.mjs';
import {createRuntime} from './runtime.mjs';

const object=(properties={},required=[])=>({type:'object',properties,required,additionalProperties:false});
const str={type:'string',minLength:1,maxLength:256};
const id={anyOf:[str,{type:'integer',minimum:1}]};
const ids={type:'array',items:id,minItems:1,maxItems:250};
const changes=object({name:str,elevation_m:{type:'number',minimum:-10000,maximum:10000},sill_m:{type:'number',minimum:0,maximum:10},mark:str});
changes.minProperties=1;
const xyz={type:'array',items:{type:'number',minimum:-10000,maximum:10000},minItems:3,maxItems:3};
const num={type:'number',minimum:0,maximum:100};
const source_id={type:'string',minLength:1,maxLength:128};
const kindSpec=(kind,props,required)=>object({kind:{const:kind},source_id,...props},['kind','source_id',...required]);
const spec={oneOf:[
 kindSpec('furniture_solid',{name:str,points_m:{type:'array',items:xyz,minItems:3,maxItems:100},height_m:num},['name','points_m','height_m']),
 kindSpec('grid',{name:str,start_m:xyz,end_m:xyz,view_id:id},['name','start_m','end_m']),
 kindSpec('component',{type_id:id,level_id:id,point_m:xyz,rotation_deg:{type:'number',minimum:-360,maximum:360},center_xy:{type:'boolean'},description:str},['type_id','level_id','point_m']),
 kindSpec('curtain_opening',{host_id:id,level_id:id,type_id:id,door_type_id:id,point_m:xyz,width_m:num,height_m:num,mark:str},['host_id','level_id','type_id','point_m','width_m','height_m']),
 kindSpec('level',{name:str,elevation_m:{type:'number',minimum:-10000,maximum:10000}},['name','elevation_m']),
 kindSpec('wall_type',{name:str,base_type_id:id,width_m:num},['name','base_type_id','width_m']),
 kindSpec('wall',{type_id:id,level_id:id,top_level_id:id,start_m:xyz,end_m:xyz,height_m:num},['type_id','level_id','start_m','end_m','height_m']),
 kindSpec('family_type',{name:str,base_type_id:id,width_m:num,height_m:num,width_parameter:str,height_parameter:str,dimensions:{type:'array',items:object({name:str,value_m:num},['name','value_m']),minItems:1,maxItems:12}},['name','base_type_id']),
 ...['door','window'].map(k=>kindSpec(k,{type_id:id,host_id:id,level_id:id,point_m:xyz,sill_m:num,unjoin_wall_ids:ids,hand_flipped:{type:'boolean'},facing_flipped:{type:'boolean'},facing_vector:xyz,hand_vector:xyz,mark:str},['type_id','host_id','level_id','point_m'])),
 kindSpec('room',{level_id:id,point_m:xyz,name:str,number:str,height_m:num},['level_id','point_m','name','number']),
 kindSpec('room_tag',{room_id:id,view_id:id,type_id:id,point_m:xyz},['room_id','view_id','point_m']),
 kindSpec('element_tag',{element_id:id,view_id:id,type_id:id,point_m:xyz},['element_id','view_id','type_id','point_m']),
 kindSpec('room_boundary',{view_id:id,start_m:xyz,end_m:xyz},['view_id','start_m','end_m']),
 kindSpec('load_family',{path:{type:'string',minLength:1,maxLength:500}},['path'])
 ,kindSpec('view_setup',{view_id:id,normal_display:{type:'boolean'},hide_ids:ids,crop_min_m:xyz,crop_max_m:xyz,scale:{type:'integer',minimum:1,maximum:1000},cut_height_m:num,top_height_m:num},['view_id'])
]};
const action={oneOf:[object({operation:{const:'create'},spec},['operation','spec']),object({operation:{const:'modify'},id,changes},['operation','id','changes']),object({operation:{const:'delete'},id},['operation','id'])]};
const write={document_token:str,strategy:{enum:['atomic','independent']},response_mode:{enum:['compact','full']}};
const filter=object({category:str,level:str,name:str,source_id:str,limit:{type:'integer',minimum:1,maximum:100},offset:{type:'integer',minimum:0},types:{type:'boolean'},count_only:{type:'boolean'},detail:{type:'boolean'},class:{enum:['ImportInstance','ViewPlan','Grid','FamilySymbol']},fields:{type:'array',items:{enum:['id','unique_id','name','category','source_id','elevation_m','bounds_m','point_m','curve_m','family','placement','type_id','host_id','parameters','dimensions','area_m2','number','bounded','level_id','facing_vector','hand_vector']},minItems:1,maxItems:24},view_id:id,active_view:{type:'boolean'},selected_only:{type:'boolean'},type_id:id,family:str,box_min_m:xyz,box_max_m:xyz,predicates:{type:'array',items:object({field:{enum:['height_m','width_m','sill_m','area_m2']},op:{enum:['gt','gte','lt','lte','eq']},value:{type:'number',minimum:-10000,maximum:10000}},['field','op','value']),minItems:1,maxItems:8}});
export const allTools=[
 ['diff_snapshots','Compare two captured pages in the same scope; old revision may differ, newest must be live.',object({before_id:str,after_id:str},['before_id','after_id'])],
 ['capture_snapshot','Capture a bounded live query page; expires or invalidates after model changes.',object({filter},['filter'])],
 ['read_snapshot','Read captured rows only while document, revision and scope remain valid.',object({snapshot_id:str},['snapshot_id'])],
 ['preview_batch','Validate a batch in rolled-back transactions and return a document-bound plan.',object({document_token:str,actions:{type:'array',items:action,minItems:1,maxItems:250},save:{type:'boolean'},response_mode:{enum:['compact','full']}},['document_token','actions'])],
 ['apply_plan','Apply a fresh one-use plan atomically; verifies typed writes and optionally saves.',object({document_token:str,plan_id:str,response_mode:{enum:['compact','full']}},['document_token','plan_id'])],
 ['set_window_sills','Prepare a verified plan setting sill height on filtered windows. Apply with apply_plan.',object({document_token:str,filter,value_m:{type:'number',minimum:0,maximum:10},save:{type:'boolean'},response_mode:{enum:['compact','full']}},['document_token','filter','value_m'])],
 ['assign_marks','Prepare deterministic marks on filtered doors/windows; existing marks preserved by default.',object({document_token:str,filter,category:{enum:['Doors','Windows']},prefix:str,start:{type:'integer',minimum:1,maximum:100000},overwrite:{type:'boolean'},save:{type:'boolean'},response_mode:{enum:['compact','full']}},['document_token','filter','category','prefix'])],
 ['tag_rooms','Prepare room tags on enclosed filtered rooms, skipping rooms already tagged in the view.',object({document_token:str,filter,view_id:id,type_id:id,save:{type:'boolean'},response_mode:{enum:['compact','full']}},['document_token','filter','view_id','type_id'])],
 ['create_test_project','Create and activate a new project from the configured architectural template in the local test folder. Requires explicit PID at Revit home; never overwrites existing files.',object({pid:{type:'integer',minimum:1},name:{type:'string',minLength:1,maxLength:80,pattern:'^[A-Za-z0-9_-]+$'}},['pid','name'])],
 ['discover_instances','Find the single open instance matching the configured project path; read only.',object()],
 ['get_project_state','Compact local decisions, historical baseline and pending items; not a live model snapshot.',object()],
 ['save_project','Save the verified project outside a transaction.',object({document_token:str,response_mode:{enum:['compact','full']}},['document_token'])],
 ['run_workflow','Remove tags in one allowed category, optional exact source_id, verify and save in one Revit call. Max 250; dependency deletion refused.',object({workflow:{const:'remove_tags'},category:{enum:['DoorTags','WindowTags','RoomTags']},source_id,document_token:str,save:{type:'boolean'},response_mode:{enum:['compact','full']}},['workflow','category'])],
 ['ping_revit','Health and Revit version; no model changes.',object()],
 ['get_active_document','Active document and session token required for writes.',object()],
 ['get_project_info','Project information and active view; optional fixed-path PNG export. Saving uses save_project.',object({export_view:{type:'boolean'}})],
 ['get_levels','Levels sorted by elevation, at most 100.',object()],
 ['query_elements','Query filtered Revit elements; projection, view/selection, type/family, spatial box and dimensional predicates.',object({filter},['filter'])],
 ['create_elements','Batch levels, walls, hosted doors/windows, rooms and tags. Explicit types/hosts required. source_id stored in RVT prevents duplicates.',object({...write,spec:{type:'array',items:spec,minItems:1,maxItems:250}},['document_token','spec'])],
 ['modify_elements','Modify name or level elevation_m. Stable UniqueId preferred.',object({...write,ids,changes},['document_token','ids','changes'])],
 ['delete_elements','Delete explicit IDs; deletion of dependent elements is refused and rolled back.',object({...write,ids},['document_token','ids'])],
 ['execute_batch','One Revit round trip for max 250 create/modify/delete actions. Atomic default; independent commits valid actions.',object({...write,actions:{type:'array',items:action,minItems:1,maxItems:250}},['document_token','actions'])],
 ['validate_elements','Check existence and compact identity/elevation for max 250 IDs.',object({ids},['ids'])],
 ['get_last_errors','Last 20 compact errors; current add-in session only.',object()]
].map(([name,description,inputSchema])=>({name,description,inputSchema,annotations:{readOnlyHint:!['apply_plan','create_test_project','save_project','run_workflow','create_elements','modify_elements','delete_elements','execute_batch'].includes(name),openWorldHint:false}}));

export const tools=selectTools(allTools,process.env.ASTRA_MCP_PROFILE??'workflow');
export function validate(value,schema,path='$') {
 if(schema.anyOf||schema.oneOf){let n=0;for(const s of schema.anyOf??schema.oneOf){try{validate(value,s,path);n++;}catch{}}if(!n||(schema.oneOf&&n!==1))throw Error(`${path}: invalid variant`);return;}
 if('const'in schema&&value!==schema.const)throw Error(`${path}: expected ${schema.const}`);
 if(schema.enum&&!schema.enum.includes(value))throw Error(`${path}: invalid choice`);
 if(schema.type==='object'){
  if(!value||typeof value!=='object'||Array.isArray(value))throw Error(`${path}: object required`);
  for(const key of schema.required??[])if(!(key in value))throw Error(`${path}.${key}: required`);
  if(Object.keys(value).length<(schema.minProperties??0))throw Error(`${path}: empty object`);
  for(const [key,v]of Object.entries(value)){if(!schema.properties[key])throw Error(`${path}.${key}: unknown field`);validate(v,schema.properties[key],`${path}.${key}`);}
 }else if(schema.type==='array'){
  if(!Array.isArray(value)||value.length<(schema.minItems??0)||value.length>(schema.maxItems??Infinity))throw Error(`${path}: invalid array length`);
  value.forEach((v,i)=>validate(v,schema.items,`${path}[${i}]`));
 }else if(schema.type==='string'){
  if(typeof value!=='string'||value.length<(schema.minLength??0)||value.length>(schema.maxLength??Infinity))throw Error(`${path}: invalid string`);
  if(schema.pattern&&!new RegExp(schema.pattern).test(value))throw Error(`${path}: invalid string pattern`);
 }else if(schema.type==='number'||schema.type==='integer'){
  if(typeof value!=='number'||!Number.isFinite(value)||(schema.type==='integer'&&!Number.isSafeInteger(value))||value<(schema.minimum??-Infinity)||value>(schema.maximum??Infinity))throw Error(`${path}: invalid number`);
 }else if(schema.type==='boolean'&&typeof value!=='boolean')throw Error(`${path}: boolean required`);
}

export function requestPipe(operation,args,target){
 const request_id=randomUUID();const payload=JSON.stringify({operation,arguments:args,request_id,expected_document:target.document,expected_document_path:target.path})+'\n';
 if(Buffer.byteLength(payload)>262144)throw Error('Request exceeds 256 KiB');
 return new Promise(resolve=>{
  let response='',done=false;
  const socket=net.createConnection(`\\\\.\\pipe\\AstraRevitBridge-2027-${target.pid}`);
  const finish=result=>{if(done)return;done=true;socket.destroy();resolve(result);};
  socket.setTimeout(35000,()=>finish({success:false,code:'TIMEOUT_OUTCOME_UNKNOWN',message:'Read Revit state before retrying writes.',request_id}));
  socket.on('connect',()=>socket.write(payload));
  socket.setEncoding('utf8');socket.on('data',chunk=>{response+=chunk;if(response.length>1048576)return finish({success:false,code:'RESPONSE_TOO_LARGE'});if(response.includes('\n')){try{const r=JSON.parse(response.split('\n')[0]);if(r.request_id!==request_id)throw Error('Request identity mismatch');finish(r);}catch(e){finish({success:false,code:'PROTOCOL_ERROR',message:e.message});}}});
  socket.on('error',e=>finish({success:false,code:'BRIDGE_UNAVAILABLE',message:`${e.code}: Revit 2027 add-in not reachable; load/restart Revit.`,request_id}));
  socket.on('end',()=>{if(!done)finish({success:false,code:'INCOMPLETE_RESPONSE',request_id});});
 });
}
const handle=createRuntime(requestPipe);
export function bridge(operation,args){const tool=tools.find(t=>t.name===operation);if(!tool)throw Error('Unknown tool');validate(args,tool.inputSchema);return handle(operation,args);}
export async function rpc(req){
 const reply=result=>({jsonrpc:'2.0',id:req.id,result});
 const error=(code,message)=>({jsonrpc:'2.0',id:req.id??null,error:{code,message}});
 if(req.jsonrpc!=='2.0'||typeof req.method!=='string')return error(-32600,'Invalid Request');
 if(req.id===undefined)return null;
 if(req.method==='initialize')return reply({protocolVersion:'2024-11-05',capabilities:{tools:{}},serverInfo:{name:'astra-revit-local',version:'0.3.0'}});
 if(req.method==='ping')return reply({});
 if(req.method==='tools/list')return reply({tools});
 if(req.method==='tools/call'){
  try{const result=await bridge(req.params?.name,req.params?.arguments??{});return reply({content:[{type:'text',text:JSON.stringify(result)}],isError:result.success===false});}
  catch(e){return reply({content:[{type:'text',text:JSON.stringify({success:false,code:'INVALID_ARGUMENTS',message:e.message})}],isError:true});}
 }
 return error(-32601,'Method not found');
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 let buffer='',queue=Promise.resolve();
 process.stdin.setEncoding('utf8');
 process.stdin.on('data',chunk=>{
  buffer+=chunk;
  if(buffer.length>524288){process.stderr.write('Input buffer exceeded 512 KiB\n');process.exit(1);}
  while(buffer.includes('\n')){const pos=buffer.indexOf('\n'),line=buffer.slice(0,pos);buffer=buffer.slice(pos+1);if(!line.trim())continue;
   queue=queue.then(async()=>{let out;try{out=await rpc(JSON.parse(line));}catch{out={jsonrpc:'2.0',id:null,error:{code:-32700,message:'Parse error'}};}if(out)process.stdout.write(JSON.stringify(out)+'\n');});
  }
 });
}
