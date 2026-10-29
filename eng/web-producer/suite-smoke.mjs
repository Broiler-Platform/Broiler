// Cross-origin acceptance uses verified/extracted archives and an intercepted approval
// catalog only. It neither changes production approval nor uploads document content.
import { chromium, firefox, webkit } from 'playwright';
import { readFile, mkdir, writeFile, mkdtemp, rm } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { tmpdir } from 'node:os';
import assert from 'node:assert/strict';
const [lockFile, directory, reportFile] = process.argv.slice(2);
const lock = JSON.parse(await readFile(lockFile,'utf8'));
const rows = lock.components;
assert.deepEqual(new Set(rows.map(r=>r.product)), new Set(['launcher','writer','cell']));
const catalog = {schemaVersion:1,status:'approved',channel:lock.channel,catalogVersion:lock.catalogVersion,
    launcherUrl:rows.find(r=>r.product==='launcher').entryUrl,
    products:rows.filter(r=>r.product!=='launcher').map(r=>({id:r.product,version:r.version,url:r.entryUrl}))};
const mime={'.html':'text/html','.js':'text/javascript','.json':'application/json','.wasm':'application/wasm','.css':'text/css','.woff2':'font/woff2'};
await mkdir(resolve(reportFile,'..'),{recursive:true});
const runs=[];
for (const [name,engine] of Object.entries({chromium,firefox,webkit})) {
    const profile=await mkdtemp(resolve(tmpdir(),'broiler-suite-'));
    const context=await engine.launchPersistentContext(profile,{viewport:{width:1280,height:960}}), run={engine:name,version:context.browser().version()}, errors=[];
    try {
        await context.route('**/*',async route=>{
            const url=new URL(route.request().url());
            const row=rows.find(r=>new URL(r.entryUrl).origin===url.origin);
            if (!row) { errors.push('Unexpected origin: '+url.origin); return route.abort(); }
            if (row.product==='launcher' && url.pathname==='/office-web.json') return route.fulfill({contentType:'application/json',body:JSON.stringify(catalog)});
            const root=resolve(directory,row.product),path=resolve(root,'.'+decodeURIComponent(url.pathname),'index.html');
            // File requests use their exact path; directory requests resolve the generated entry.
            const file=url.pathname.endsWith('/')?path:resolve(root,'.'+decodeURIComponent(url.pathname));
            if (!file.startsWith(root+sep)) return route.abort();
            try { return route.fulfill({contentType:mime[extname(file)]||'application/octet-stream',body:await readFile(file)}); }
            catch { errors.push('Missing asset: '+url.href); return route.fulfill({status:404,body:'Missing verified artifact'}); }
        });
        const page=await context.newPage();
        page.on('pageerror',error=>errors.push(String(error)));
        page.on('console',message=>{if(message.type()==='error')errors.push(message.text());});
        await page.goto('https://demo.broiler.app/');
        await page.waitForURL(catalog.launcherUrl);
        await page.waitForFunction(()=>document.body.dataset.ready==='true');
        assert.ok((await page.locator('#deployment').textContent()).includes(lock.catalogVersion));
        for (const row of rows.filter(r=>r.product!=='launcher')) {
            const link=page.locator(`[data-product="${row.product}"]`);
            assert.equal(await link.getAttribute('href'),row.entryUrl);
            await link.click({noWaitAfter:true}); await page.waitForURL(row.entryUrl,{waitUntil:'domcontentloaded'});
            await page.waitForFunction(id=>document.getElementById(id+'-status')?.hidden,row.product==='cell'?'cells':'writer');
            await page.getByRole('link',{name:'Return to Office'}).click({noWaitAfter:true});
            await page.waitForURL(catalog.launcherUrl,{waitUntil:'domcontentloaded'});
            await page.waitForFunction(()=>document.body.dataset.ready==='true');
        }
        assert.deepEqual(errors,[]); run.pass=true;
    } catch(error) {run.pass=false;run.error=String(error);}
    finally {await context.close();await rm(profile,{recursive:true,force:true,maxRetries:5});runs.push(run);console.log(JSON.stringify(run));}
}
await writeFile(reportFile,JSON.stringify({catalogVersion:lock.catalogVersion,runs},null,2));
if(runs.some(r=>!r.pass))process.exitCode=1;
