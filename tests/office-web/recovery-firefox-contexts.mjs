// Optional diagnostic, not an acceptance pass/fail gate. Uses fixture-only profiles.
import { firefox } from 'playwright';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { randomUUID } from 'node:crypto';

const output=resolve('../../artifacts/web-w5');await mkdir(output,{recursive:true});
const module=await readFile(resolve(output,'cell/wwwroot/broiler.recovery.js'));
const server=createServer((request,response)=>{
    if(request.url==='/recovery.js')response.writeHead(200,{'Content-Type':'text/javascript'}).end(module);
    else response.writeHead(200,{'Content-Type':'text/html'}).end('<!doctype html><html><body><nav class="office-nav"></nav></body></html>');
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const results=[];
try{for(const kind of ['non-persistent','persistent']){
    let browser,context;const result={kind};
    try{
        if(kind==='persistent'){
            context=await firefox.launchPersistentContext(resolve(output,'firefox-context-probe-'+randomUUID()),{acceptDownloads:true});browser=context.browser();
        }else{browser=await firefox.launch();context=await browser.newContext({acceptDownloads:true});}
        result.version=browser.version();const page=await context.newPage();
        await page.goto(`http://127.0.0.1:${server.address().port}/host`);
        await page.evaluate(async()=>{
            const {OfficeRecovery,RecoveryStore}=await import('/recovery.js');
            const store=new RecoveryStore('capacity');
            const record={schema:2,id:'0',app:'capacity',name:'Bounded.csv',format:'cells-json',payload:'[["fixture"]]',generation:1,revision:1,updatedAt:1};
            for(let i=0;i<30;i++)await store.put({...record,id:String(i)});
            try{await store.put({...record,id:'31'});}catch{}
            window.manager=new OfficeRecovery({app:'failure',capture:()=>({name:'Failure.csv',format:'cells-json',payload:'[["fixture"]]',generation:1,revision:1}),restore:()=>{},acceptFormat:()=>true});
            manager.changed({modified:true,generation:1,revision:1});await manager.start();
            manager.store.put=async()=>{throw new DOMException('quota fixture','QuotaExceededError');};await manager.flush(true);
        });
        await page.locator('#office-recovery summary').click();
        const [download]=await Promise.all([page.waitForEvent('download',{timeout:8000}),page.getByRole('link',{name:'Export current recovery',exact:true}).click({timeout:8000})]);
        await download.saveAs(resolve(output,`firefox-${kind}-probe.json`));result.download=true;
    }catch(error){result.download=false;result.error=String(error);}
    finally{await context?.close().catch(()=>{});await browser?.close().catch(()=>{});results.push(result);console.log(JSON.stringify(result));}
}}finally{await new Promise(resolve=>server.close(resolve));await writeFile(resolve(output,'firefox-contexts.json'),JSON.stringify(results,null,2));}
