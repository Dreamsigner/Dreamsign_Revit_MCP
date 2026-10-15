import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
export function connect(){
 const child=spawn(process.execPath,[fileURLToPath(new URL('./server.mjs',import.meta.url))],{stdio:['pipe','pipe','inherit'],windowsHide:true,env:{...process.env,ASTRA_MCP_PROFILE:'full'}});let buffer='',seq=0;const pending=new Map();
 child.stdout.setEncoding('utf8');child.stdout.on('data',s=>{buffer+=s;while(buffer.includes('\n')){const i=buffer.indexOf('\n'),r=JSON.parse(buffer.slice(0,i));buffer=buffer.slice(i+1);pending.get(r.id)?.(r);pending.delete(r.id);}});
 const request=(method,params={})=>new Promise((resolve,reject)=>{const id=++seq;const timer=setTimeout(()=>{pending.delete(id);reject(Error('MCP timeout; inspect state before retry'));},40000);pending.set(id,r=>{clearTimeout(timer);resolve(r);});child.stdin.write(JSON.stringify({jsonrpc:'2.0',id,method,params})+'\n');});
 async function call(name,args={}){
  // Preserve legacy reconstruction scripts; new MCP callers use compact results.
  if(name==='get_project_info'&&args.save_document){const doc=await call('get_active_document');if(!doc.success)return doc;const saved=await call('save_project',{document_token:doc.document_token});if(!saved.success)return saved;const {save_document,...readArgs}=args;const info=await call(name,readArgs);return {...info,saved:true};}
  if(['create_elements','modify_elements','delete_elements','execute_batch'].includes(name))args={response_mode:'full',...args};
  const r=await request('tools/call',{name,arguments:args});if(r.error)throw Error(JSON.stringify(r.error));return JSON.parse(r.result.content[0].text);
 }
 return {async start(){await request('initialize',{protocolVersion:'2024-11-05',capabilities:{},clientInfo:{name:'astra-reconstruction',version:'2'}});},call,close(){child.stdin.end();child.kill();}};
}
