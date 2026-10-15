import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {allTools,validate} from './server.mjs';
import {resolveTarget,compactResult,createRuntime} from './runtime.mjs';

const target={pid:99,document:'ExampleProject',path:'C:\\Projects\\ExampleProject.rvt'};
assert.equal((await resolveTarget(target,async(op,args,t)=>({success:true,document:target.document,path:target.path,bridge_version:'0.3.0'}),()=>[7])).code,'AMBIGUOUS_TARGET');
assert.equal((await resolveTarget(target,async()=>({success:true,document:target.document,path:'C:\\Other\\ExampleProject.rvt'}),()=>[7])).code,'TARGET_UNAVAILABLE');
assert.equal((await resolveTarget(target,async()=>({success:true,document:target.document}),()=>[7])).code,'UPGRADE_REQUIRED');
const discovered=await resolveTarget({...target,pid:null},async()=>({success:true,document:target.document,path:target.path,bridge_version:'0.3.0'}),()=>[7]);assert.equal(discovered.pid,7);
const compact=compactResult({success:false,results:Array.from({length:8},()=>({success:false,message:'example'}))},'reports/example.json');assert.equal(compact.errors.length,5);assert.equal(compact.errors_truncated,true);
let sends=0;const refused=createRuntime(async()=>{sends++;},{getTarget:()=>target,resolve:async()=>({success:false,code:'AMBIGUOUS_TARGET'})});assert.equal((await refused('apply_plan',{})).code,'AMBIGUOUS_TARGET');assert.equal(sends,0);
for(const [name,args] of [['create_elements',{document_token:'x',spec:[]}],['query_elements',{filter:{limit:10000}}],['execute_batch',{document_token:'x',actions:[{operation:'exec'}]}],['modify_elements',{document_token:'x',ids:[1],changes:{}}]])assert.throws(()=>validate(args,allTools.find(t=>t.name===name).inputSchema));
const child=spawn(process.execPath,[fileURLToPath(new URL('./server.mjs',import.meta.url))],{stdio:['pipe','pipe','inherit'],windowsHide:true,env:{...process.env,ASTRA_MCP_PROFILE:'inspection'}});
let buffer='',sequence=0;const pending=new Map();
child.stdout.setEncoding('utf8');child.stdout.on('data',chunk=>{buffer+=chunk;while(buffer.includes('\n')){const pos=buffer.indexOf('\n'),reply=JSON.parse(buffer.slice(0,pos));buffer=buffer.slice(pos+1);pending.get(reply.id)?.(reply);pending.delete(reply.id);}});
const request=(method,params={})=>new Promise((resolve,reject)=>{const id=++sequence,timer=setTimeout(()=>reject(Error('MCP test timed out')),10000);pending.set(id,result=>{clearTimeout(timer);resolve(result);});child.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');});
try{
 const init=await request('initialize',{protocolVersion:'2024-11-05',capabilities:{},clientInfo:{name:'offline-tests',version:'1'}});assert.equal(init.result.serverInfo.name,'astra-revit-local');
 const list=(await request('tools/list')).result.tools;assert.ok(list.every(t=>t.annotations.readOnlyHint));assert.ok(list.some(t=>t.name==='query_elements'));
 const hidden=await request('tools/call',{name:'delete_elements',arguments:{document_token:'x',ids:[1]}});assert.equal(hidden.result.isError,true);
 assert.equal((await request('unknown')).error.code,-32601);
 console.log('Passed: target isolation, bounded errors, refused writes, schema validation, MCP stdio and read-only profile.');
}finally{child.stdin.end();child.kill();}
