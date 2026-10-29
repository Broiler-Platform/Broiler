import { chromium, firefox, webkit } from 'playwright';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, extname, sep, basename } from 'node:path';
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import assert from 'node:assert/strict';

const output = resolve('../../artifacts/web-w5/browser'); await mkdir(output,{recursive:true});
const roots = { writer:resolve('../../artifacts/web-w5/writer/wwwroot'), cell:resolve('../../artifacts/web-w5/cell/wwwroot') };
assert.deepEqual(await readFile(resolve(roots.writer,'broiler.recovery.js')),await readFile(resolve(roots.cell,'broiler.recovery.js')));
const mime={'.html':'text/html','.js':'text/javascript','.json':'application/json','.wasm':'application/wasm'};
const server=createServer(async(request,response)=>{
    try {
        const url=new URL(request.url,'http://localhost');
        if(url.pathname==='/host') {response.writeHead(200,{'Content-Type':'text/html'}).end('<!doctype html><html lang="en"><meta charset="utf-8"><body><nav class="office-nav"></nav></body></html>');return;}
        const match=/^\/(writer|cell)\/v[12]\/(.*)$/.exec(url.pathname);
        if(!match){response.writeHead(404).end();return;}
        const root=roots[match[1]],path=resolve(root,decodeURIComponent(match[2])||'index.html');
        if(!path.startsWith(root+sep)){response.writeHead(403).end();return;}
        const bytes=await readFile(path);response.writeHead(200,{'Content-Type':mime[extname(path)]||'application/octet-stream'}).end(bytes);
    }catch{response.writeHead(404).end();}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const origin=`http://127.0.0.1:${server.address().port}`;
const report={generated:new Date().toISOString(),runs:[]};let failed=false;
async function ready(page,app,version=1){
    await page.goto(`${origin}/${app}/v${version}/`);
    await page.waitForFunction(app=>document.getElementById(`${app==='cell'?'cells':'writer'}-status`)?.hidden,app);
    await page.evaluate(async()=>{
        const script=document.querySelector('script[type="module"][src]').src;
        const main=await import(script);window.captureRecovery=main.recoverySnapshot;
        window.recoveredText=main.documentPreview||(()=>JSON.parse(main.recoverySnapshot().payload)[0][0]);
        const recovery=await import(new URL('./broiler.recovery.js',script));window.RecoveryStore=recovery.RecoveryStore;
    });
    await page.waitForFunction(()=>document.getElementById('recovery-status')?.dataset.state);
}
async function drafts(page,app){return page.evaluate(async app=>new window.RecoveryStore(app==='cell'?'cells':'writer').list(),app);}
async function focus(page,app){await page.locator(`#${app==='cell'?'cells':'writer'}-text-input`).focus();}
async function newDocument(page,app){
    await page.locator('canvas').click({position:{x:120,y:170}});await focus(page,app);await page.keyboard.press('Control+n');
    await page.waitForFunction(app=>document.getElementById(`${app==='cell'?'cells':'writer'}-operation`).dataset.outcome==='New',app);
}
async function type(page,app,text){
    await focus(page,app);await page.keyboard.insertText(text);
    await page.waitForFunction(text=>window.recoveredText()===text,text);
    await page.waitForFunction(()=>['saved','warning'].includes(document.getElementById('recovery-status').dataset.state));
    return (await drafts(page,app))[0];
}
async function openPanel(page){await page.bringToFront();if(!await page.locator('#office-recovery').evaluate(node=>node.open))await page.locator('#office-recovery summary').click();}
async function restore(page,record,text){
    await openPanel(page);await page.getByRole('button',{name:'Refresh drafts',exact:true}).click();
    await page.locator(`[data-draft-id="${record.id}"]`).getByRole('button',{name:'Restore',exact:true}).click();
    await page.getByRole('dialog',{name:'Recovery confirmation'}).getByRole('button',{name:'Continue',exact:true}).click();
    await page.waitForFunction(text=>window.recoveredText()===text,text);
    await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='restored');
    assert.equal(await page.getAttribute('body','data-modified'),'true');
}
function childBrowserPid(engine){
    if(process.platform!=='win32')throw new Error('Abrupt persistent-browser termination test currently requires Windows.');
    const executable=engine.executablePath().replaceAll("'","''");
    const script=`Get-CimInstance Win32_Process -Filter "ParentProcessId = ${process.pid}" | Where-Object { $_.ExecutablePath -eq '${executable}' } | Select-Object -ExpandProperty ProcessId`;
    const values=execFileSync('powershell.exe',['-NoProfile','-Command',script],{encoding:'utf8',windowsHide:true}).trim().split(/\s+/).filter(Boolean).map(Number);
    assert.equal(values.length,1,`Expected one task-owned ${basename(engine.executablePath())} child, found ${values}`);
    return values[0];
}

try{for(const [name,engine] of Object.entries({chromium,firefox,webkit})){
    if(process.env.W5_ENGINES&&!process.env.W5_ENGINES.split(',').includes(name))continue;
    const result={engine:name,checks:[]};const mark=text=>{result.checks.push(text);console.log(name+': '+text);};
    let browser,context,lastPage;
    try{
        context=await engine.launchPersistentContext(resolve(output,name+'-contracts-profile-'+randomUUID()),{acceptDownloads:true,...(name==='chromium'?{channel:'chromium'}:{})});browser=context.browser();result.version=browser.version();
        let page=await context.newPage();lastPage=page;await page.goto(origin+'/host');
        const contract=await page.evaluate(async()=>{
            const module=await import('/cell/v1/broiler.recovery.js');window.recoveryModule=module;
            const store=new module.RecoveryStore('contract');
            const record={schema:1,id:'legacy',app:'contract',name:'Legacy.csv',format:'cells-json',payload:'[["legacy"]]',revision:3,generation:1,updatedAt:1};
            await store.put(record);
            const nativePut=IDBObjectStore.prototype.put;
            IDBObjectStore.prototype.put=function(...args){
                const request=nativePut.apply(this,args);
                if(this.name==='drafts')request.addEventListener('success',()=>this.transaction.abort());
                return request;
            };
            let aborted=false;try{await store.migrate(record);}catch{aborted=true;}
            finally{IDBObjectStore.prototype.put=nativePut;}
            const unchanged=(await store.list())[0];
            const migrated=await store.migrate(unchanged);
            const backup=await store.transaction(['backups'],'readonly',tx=>{const request=tx.objectStore('backups').get('legacy:schema1');return()=>request.result;});
            let rejected=false;try{module.validateRecord({...record,schema:999},'contract');}catch{rejected=true;}
            return{aborted,oldPayload:unchanged.payload,oldSchema:unchanged.schema,newSchema:migrated.schema,backupSchema:backup.original.schema,rejected};
        });
        assert.deepEqual(contract,{aborted:true,oldPayload:'[["legacy"]]',oldSchema:1,newSchema:2,backupSchema:1,rejected:true});
        mark('real IndexedDB migration commits backup and new record atomically; aborted migration retains old schema/payload; future schemas rejected');
        const capacity=await page.evaluate(async()=>{
            const store=new window.recoveryModule.RecoveryStore('capacity');
            const record={schema:2,id:'0',app:'capacity',name:'Bounded.csv',format:'cells-json',payload:'[["keep"]]',revision:1,generation:1,updatedAt:1};
            for(let i=0;i<30;i++)await store.put({...record,id:String(i)});
            let rejected=false;try{await store.put({...record,id:'31'});}catch{rejected=true;}
            await store.put({...record,id:'0',revision:2});
            return{rejected,count:(await store.list()).length,existingRevision:(await store.list()).find(item=>item.id==='0').revision};
        });
        assert.deepEqual(capacity,{rejected:true,count:30,existingRevision:2});
        mark('draft-count limit refuses new drafts without eviction and still permits updating an existing draft');

        await page.evaluate(async()=>{
            const {OfficeRecovery}=window.recoveryModule;
            window.manager=new OfficeRecovery({app:'failure',capture:()=>({name:'Failure.csv',format:'cells-json',payload:'[["keep"]]',generation:1,revision:1}),restore:()=>{},acceptFormat:format=>format==='cells-json'});
            manager.changed({modified:true,generation:1,revision:1});await manager.start();
            manager.realPut=manager.store.put.bind(manager.store);
            manager.store.put=async()=>{throw new DOMException('quota fixture','QuotaExceededError');};
            await manager.flush(true);
        });
        await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='unavailable');
        await openPanel(page);const [bundleDownload]=await Promise.all([page.waitForEvent('download'),page.getByRole('link',{name:'Export current recovery',exact:true}).click({timeout:5000})]);
        const bundlePath=resolve(output,name+'-quota-recovery.json');await bundleDownload.saveAs(bundlePath);
        assert.equal(JSON.parse(await readFile(bundlePath,'utf8')).payload,'[["keep"]]');
        await page.evaluate(async()=>{manager.store.put=manager.realPut;await manager.flush(true);});
        assert.equal((await page.evaluate(()=>manager.store.list())).length,1);
        mark('quota failure is visible; direct recovery-bundle export works without a successful storage write; retry commits');
        await page.evaluate(async()=>{
            await manager.store.transaction(['drafts'],'readwrite',tx=>tx.objectStore('drafts').put({schema:999,id:'future',app:'failure',name:'Future.csv',format:'unknown',payload:'opaque future data',revision:1,generation:1,updatedAt:2}));
            await manager.refresh();
        });
        const [futureDownload]=await Promise.all([page.waitForEvent('download'),page.locator('[data-draft-id="future"]').getByRole('link',{name:'Export bundle',exact:true}).click()]);
        const futurePath=resolve(output,name+'-future-schema.json');await futureDownload.saveAs(futurePath);
        assert.equal(JSON.parse(await readFile(futurePath,'utf8')).schema,999);
        await page.locator('[data-draft-id="future"]').getByRole('button',{name:'Restore',exact:true}).click();
        await page.getByRole('dialog',{name:'Recovery confirmation'}).getByRole('button',{name:'Continue',exact:true}).click();
        await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='unavailable');
        assert.ok((await page.evaluate(()=>manager.store.list())).some(item=>item.id==='future'&&item.payload==='opaque future data'));
        await page.locator('[data-draft-id="future"]').getByRole('button',{name:'Delete',exact:true}).click();
        await page.getByRole('dialog',{name:'Recovery confirmation'}).getByRole('button',{name:'Continue',exact:true}).click();
        await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='deleted');
        assert.equal((await page.evaluate(()=>manager.store.list())).length,1);
        mark('future-schema drafts remain exportable and are never restored or erased implicitly; selected deletion is confirmed');
        const scheduling=await page.evaluate(async()=>{
            let revision=2;
            manager.capture=()=>({name:'Failure.csv',format:'cells-json',payload:'[["keep"]]',generation:1,revision});
            for(let i=0;i<25;i++){
                revision++;manager.changed({modified:true,generation:1,revision});
                await new Promise(resolve=>setTimeout(resolve,100));
            }
            const committedWhileTyping=(await manager.store.list())[0].revision>1;
            await manager.flush(true);
            revision++;manager.changed({modified:true,generation:1,revision});
            Object.defineProperty(document,'hidden',{configurable:true,value:true});
            document.dispatchEvent(new Event('visibilitychange'));await manager.queue;
            delete document.hidden;
            const hiddenRevision=(await manager.store.list())[0].revision;
            revision++;manager.changed({modified:true,generation:1,revision});
            window.dispatchEvent(new Event('pagehide'));await manager.queue;
            return{committedWhileTyping,hiddenFlushed:hiddenRevision===revision-1,pageHideFlushed:(await manager.store.list())[0].revision===revision};
        });
        assert.deepEqual(scheduling,{committedWhileTyping:true,hiddenFlushed:true,pageHideFlushed:true});
        mark('continuous edits checkpoint within bounded debounce; visibility/pagehide signals flush through committed transactions');
        await page.evaluate(async()=>{
            const record={schema:1,id:'backup-fixture',app:'failure',name:'Before-migration.csv',format:'cells-json',payload:'[["before migration"]]',revision:3,generation:1,updatedAt:1};
            await manager.store.put(record);await manager.store.migrate(record);await manager.refresh();
        });
        const [backupDownload]=await Promise.all([page.waitForEvent('download'),page.getByRole('link',{name:'Export migration backup',exact:true}).click()]);
        const backupPath=resolve(output,name+'-migration-backup.json');await backupDownload.saveAs(backupPath);
        const backup=JSON.parse(await readFile(backupPath,'utf8'));assert.equal(backup.schema,1);assert.equal(backup.payload,'[["before migration"]]');
        await page.locator('[data-draft-id="backup-fixture"]').getByRole('button',{name:'Delete',exact:true}).click();
        await page.getByRole('dialog',{name:'Recovery confirmation'}).getByRole('button',{name:'Continue',exact:true}).click();
        await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='deleted');
        assert.equal((await page.evaluate(()=>manager.store.backups())).length,0);
        mark('migration backups export their untouched original schema/payload; confirmed draft deletion removes its backup');



        await context.close();await browser.close();context=null;browser=null;

        const profile=resolve(output,name+'-profile-'+randomUUID());
        context=await engine.launchPersistentContext(profile,{acceptDownloads:true,viewport:{width:1280,height:960},...(name==='chromium'?{channel:'chromium'}:{})});
        const saved=[];
        for(const app of ['writer','cell']){
            page=await context.newPage();lastPage=page;await ready(page,app);await newDocument(page,app);
            const text=`Recovery ${name} ${app} ÄΩ`;
            await type(page,app,text);
            if(app==='writer'){
                await focus(page,app);await page.keyboard.press('Control+a');await page.keyboard.press('Control+b');
                await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='saved');
                await openPanel(page);await page.getByRole('button',{name:'Save checkpoint now',exact:true}).click();
                await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='saved');
            }
            const record=(await drafts(page,app))[0];assert.ok(record.payload.length>0);
            saved.push({app,text,record});
        }
        // A second Writer tab must not overwrite the first one's draft, even with the same name.
        const concurrent=await context.newPage();lastPage=concurrent;await ready(concurrent,'writer');await newDocument(concurrent,'writer');await type(concurrent,'writer','Other tab');
        const writerDrafts=await drafts(concurrent,'writer');assert.equal(writerDrafts.length,2);assert.equal(new Set(writerDrafts.map(record=>record.tabId)).size,2);
        assert.ok(writerDrafts.some(record=>record.id===saved[0].record.id));
        mark('Writer rich text and uncommitted Cells editor text checkpoint through real managed adapters; concurrent tabs use distinct draft/tab identities');

        // Kill only the verified direct child launched by this test, including its process tree.
        // No unload, pagehide, context.close or browser.close is requested before termination.
        const pid=childBrowserPid(engine);
        execFileSync('taskkill.exe',['/PID',String(pid),'/T','/F'],{windowsHide:true,stdio:'pipe'});
        await context.close().catch(()=>{});context=null;
        context=await engine.launchPersistentContext(profile,{acceptDownloads:true,viewport:{width:1280,height:960},...(name==='chromium'?{channel:'chromium'}:{})});
        for(const item of saved){
            page=await context.newPage();lastPage=page;await ready(page,item.app,2);
            assert.ok((await drafts(page,item.app)).some(record=>record.id===item.record.id));
            await restore(page,item.record,item.text);
            if(item.app==='writer')assert.match(Buffer.from(await page.evaluate(()=>window.captureRecovery().payload),'base64').toString(),/\\b(?:\s|\\)/);
            assert.ok((await drafts(page,item.app)).some(record=>record.id===item.record.id),'Original draft must survive restoration');
            const [downloading]=await Promise.all([page.waitForEvent('download'),page.getByRole('link',{name:'Export current recovery',exact:true}).click({timeout:5000})]);
            const file=resolve(output,`${name}-${item.app}-recovered.json`);await downloading.saveAs(file);
            assert.equal(JSON.parse(await readFile(file,'utf8')).format,item.app==='writer'?'rtf':'cells-json');
            await page.screenshot({path:resolve(output,`${name}-${item.app}.png`)});
            const badPayload=item.app==='writer'?'!!!':'[[null]]';
            const badBundle={...item.record,payload:badPayload};
            await page.getByLabel('Import recovery bundle').setInputFiles({name:'invalid-recovery.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(badBundle))});
            await page.waitForFunction(async app=>{
                const records=await new window.RecoveryStore(app==='cell'?'cells':'writer').list();
                return records.some(record=>record.payload===(app==='writer'?'!!!':'[[null]]'));
            },item.app);
            const badRecord=(await drafts(page,item.app)).find(record=>record.payload===badPayload);
            await page.getByRole('button',{name:'Refresh drafts',exact:true}).click();
            await page.locator(`[data-draft-id="${badRecord.id}"]`).getByRole('button',{name:'Restore',exact:true}).click();
            await page.getByRole('dialog',{name:'Recovery confirmation'}).getByRole('button',{name:'Continue',exact:true}).click();
            await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='unavailable');
            assert.equal(await page.evaluate(()=>window.recoveredText()),item.text);
            await page.locator(`[data-draft-id="${badRecord.id}"]`).getByRole('button',{name:'Delete',exact:true}).click();
            await page.getByRole('dialog',{name:'Recovery confirmation'}).getByRole('button',{name:'Continue',exact:true}).click();
            await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='deleted');

        }
        mark('forced browser-process-tree termination followed by persistent-profile restart restores last committed Writer/Cells data; version-prefix update retains drafts and bundle export; invalid app payloads preserve current content');
        // Preferences persist independently of drafts. Clearing Writer must leave Cells intact.
        page=await context.newPage();lastPage=page;await ready(page,'writer');await openPanel(page);
        await page.getByLabel('Automatic checkpoints',{exact:false}).uncheck();
        await page.reload();await ready(page,'writer');await openPanel(page);assert.equal(await page.getByLabel('Automatic checkpoints',{exact:false}).isChecked(),false);
        const cellCount=(await drafts(page,'cell')).length;assert.ok(cellCount>0);
        await page.getByRole('button',{name:'Clear local data',exact:true}).click();
        await page.getByRole('dialog',{name:'Recovery confirmation'}).getByRole('button',{name:'Continue',exact:true}).click();
        await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='paused');
        assert.equal((await drafts(page,'writer')).length,0);assert.equal((await drafts(page,'cell')).length,cellCount);
        // Import a separately exported bundle after all Writer local drafts are gone.
        await page.getByLabel('Import recovery bundle').setInputFiles(resolve(output,`${name}-writer-recovered.json`));
        await page.waitForFunction(()=>document.querySelectorAll('.recovery-draft').length===1);
        mark('checkpoint preference survives reload; clear-local-data is confirmed and app-scoped; exported bundles import after local deletion');
        await context.close();context=null;

        context=await engine.launchPersistentContext(resolve(output,name+'-denied-profile-'+randomUUID()),{acceptDownloads:true,...(name==='chromium'?{channel:'chromium'}:{})});browser=context.browser();
        await context.addInitScript(()=>{
            Object.defineProperty(window,'indexedDB',{configurable:true,get(){throw new DOMException('storage denied fixture','SecurityError');}});
            Object.defineProperty(window,'localStorage',{configurable:true,get(){throw new DOMException('preferences denied fixture','SecurityError');}});
        });
        page=await context.newPage();lastPage=page;page.on('dialog',async dialog=>{result.deniedStorageDialogs??=[];result.deniedStorageDialogs.push(dialog.type());await dialog.accept();});await ready(page,'cell');await newDocument(page,'cell');
        await focus(page,'cell');await page.keyboard.insertText('Storage unavailable');
        await page.waitForFunction(()=>document.getElementById('recovery-status').dataset.state==='unavailable');
        assert.equal(await page.evaluate(()=>window.recoveredText()),'Storage unavailable');
        await openPanel(page);
        for(let attempt=0;attempt<3;attempt++){
            const link=page.getByRole('link',{name:'Export current recovery',exact:true});
            if(attempt===1)await link.focus();
            const [deniedDownload]=await Promise.all([page.waitForEvent('download'),attempt===1?page.keyboard.press('Enter'):link.click()]);
            await deniedDownload.saveAs(resolve(output,`${name}-storage-denied-${attempt}.json`));
            assert.equal(await page.evaluate(()=>window.recoveredText()),'Storage unavailable');
        }
        mark('denied IndexedDB and preferences leave editing and direct bundle export working; no durability claim is made');
        result.pass=true;
    }catch(error){failed=true;result.pass=false;result.error=error.stack||String(error);try{result.status=await lastPage.locator('#recovery-status').textContent();result.details=await lastPage.evaluate(()=>({focus:document.activeElement?.outerHTML?.slice(0,160),hasFocus:document.hasFocus(),recoveryOpen:document.getElementById('office-recovery')?.open,exports:[...document.querySelectorAll('a.recovery-export')].map(a=>({text:a.textContent,href:a.href.slice(0,80),bounds:{width:a.getBoundingClientRect().width,height:a.getBoundingClientRect().height}}))}));await lastPage.screenshot({path:resolve(output,name+'-failure.png')});}catch{}}
    finally{await context?.close().catch(()=>{});await browser?.close().catch(()=>{});report.runs.push(result);console.log(JSON.stringify(result));}
}}finally{await new Promise(resolve=>server.close(resolve));await writeFile(resolve(output,'report.json'),JSON.stringify(report,null,2));}
if(failed)process.exitCode=1;
