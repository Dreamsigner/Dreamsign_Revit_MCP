import {allTools} from './server.mjs';import {writeFileSync,mkdirSync} from 'node:fs';
mkdirSync(new URL('./bin/',import.meta.url),{recursive:true});
const local=new Set(['discover_instances','get_project_state']);
const contract={version:'0.3.0',commands:allTools.filter(t=>!local.has(t.name)).map(t=>t.name),tools:allTools.map(t=>({name:t.name,read_only:t.annotations.readOnlyHint}))};
writeFileSync(new URL('./bin/command-registry.json',import.meta.url),JSON.stringify(contract,null,2));
