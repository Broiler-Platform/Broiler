import { chromium, firefox, webkit } from 'playwright';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';

const root = resolve(process.env.CELL_WWWROOT || '../../artifacts/web-w4/cell/wwwroot');
const output = resolve('../../artifacts/web-w4/browser'); await mkdir(output, { recursive: true });
const mime = { '.html':'text/html', '.js':'text/javascript', '.json':'application/json', '.wasm':'application/wasm' };
const server = createServer(async (request,response) => {
    try {
        const path = resolve(root, decodeURIComponent(new URL(request.url,'http://localhost').pathname).slice(1) || 'index.html');
        if (!path.startsWith(root+sep)) { response.writeHead(403).end(); return; }
        response.writeHead(200, {'Content-Type':mime[extname(path)] || 'application/octet-stream'}).end(await readFile(path));
    } catch { response.writeHead(404).end(); }
});
await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
const origin = `http://127.0.0.1:${server.address().port}/`;
const report = { generated:new Date().toISOString(), runs:[] };
let failed = false;
for (const [name,engine] of Object.entries({chromium,firefox,webkit})) {
    if (process.env.W4_ENGINES && !process.env.W4_ENGINES.split(',').includes(name)) continue;
    let browser, context; const result = { engine:name, checks:[] };
    const mark = text => { result.checks.push(text); console.log(name + ': ' + text); };
    let page;
    try {
        const options={acceptDownloads:true,viewport:{width:1280,height:900}};
        if(process.env.OFFICE_WEB_PERSISTENT_PROFILES==='1'){
            context=await engine.launchPersistentContext(resolve(output,name+'-profile-'+randomUUID()),{...options,...(name==='chromium'?{channel:'chromium'}:{})});browser=context.browser();
        }else{browser=await engine.launch();context=await browser.newContext(options);}
        result.version=browser.version();
        const errors=[];
        page=await context.newPage(); page.on('pageerror', error=>errors.push(String(error)));
        page.on('console', message=>{if(message.type()==='error') errors.push(message.text());});
        await page.goto(origin); await page.waitForFunction(()=>document.getElementById('cells-status').hidden);
        await page.locator('canvas').click({position:{x:140,y:175}});
        await page.waitForFunction(()=>document.activeElement?.id==='cells-text-input');
        const focus=async()=>{ await page.locator('#cells-text-input').focus(); };
        const outcome=async value=>{await page.waitForFunction(value=>document.getElementById('cells-operation').dataset.outcome===value,value);};
        const snapshot=()=>page.evaluate(()=>window.cellsSnapshot());
        // Read-only managed snapshot; all mutations still go through real UI/file events.
        const entry = await page.evaluate(async()=>{
            const script = document.querySelector('script[type="module"][src]').src;
            const module = await import(script);
            window.cellsSnapshot = module.sheetSnapshot;
            return true;
        }); assert.equal(entry,true);
        await page.keyboard.press('Control+n'); await outcome('New');
        await focus(); await page.keyboard.insertText('ÄΩ Unicode'); await page.keyboard.press('Enter');
        await page.waitForFunction(()=>window.cellsSnapshot()[0][0]==='ÄΩ Unicode');
        await page.keyboard.press('ArrowRight'); await page.keyboard.insertText('=1+2'); await page.keyboard.press('Enter');
        await page.waitForFunction(()=>window.cellsSnapshot()[0][1]==='=1+2');
        assert.equal(await page.getAttribute('body','data-modified'),'true');
        mark('Unicode typing, navigation, committed string values and modified state');
        await page.evaluate(()=>{
            document.getElementById('cells-text-input').addEventListener('copy',event=>{ window.ownCopiedCell = event.clipboardData?.getData('text/plain') === '=1+2'; });
            document.getElementById('cells-text-input').addEventListener('paste',event=>{ window.ownPastedCell = { trusted:event.isTrusted, matchesOwnCopy:event.clipboardData?.getData('text/plain') === '=1+2', empty:!event.clipboardData?.getData('text/plain') }; });
        });
        await focus(); await page.keyboard.press('Control+c'); await page.waitForFunction(()=>window.ownCopiedCell===true); await page.keyboard.press('ArrowDown'); await page.keyboard.press('Control+v');
        await page.waitForFunction(()=>window.cellsSnapshot()[1][1]==='=1+2');
        await page.keyboard.press('Control+x'); await page.waitForFunction(()=>window.cellsSnapshot()[1][1]==='');
        mark('trusted single-cell copy/paste/cut');
        await focus(); await page.keyboard.press('ArrowDown');
        await page.locator('#cells-text-input').evaluate(input=>{
            input.dispatchEvent(new CompositionEvent('compositionstart',{data:'',bubbles:true}));
            input.dispatchEvent(new CompositionEvent('compositionupdate',{data:'日本語',bubbles:true}));
            input.dispatchEvent(new CompositionEvent('compositionend',{data:'日本語',bubbles:true}));
            input.dispatchEvent(new InputEvent('input',{data:'日本語',inputType:'insertText',bubbles:true}));
        });
        await page.keyboard.press('Enter');
        await page.waitForFunction(()=>window.cellsSnapshot()[2][1]==='日本語');
        mark('composition lifecycle commits once with duplicate input echo suppressed');
        await focus(); await page.keyboard.press('ArrowRight'); await page.keyboard.press('F2');
        await page.keyboard.insertText('Undo text'); await page.keyboard.press('Control+z'); await page.keyboard.press('Enter');
        assert.equal((await snapshot())[2][2], '');
        mark('undo within the active cell text editor; sheet-wide undo/redo is unavailable');
        const before=await snapshot();
        await focus(); await page.keyboard.press('Control+n');
        await page.getByRole('dialog',{name:'Keep your changes?'}).getByRole('button',{name:'Cancel',exact:true}).click();
        assert.deepEqual(await snapshot(),before);
        async function exportCsv(filename) {
            await focus(); await page.keyboard.press('Control+Shift+s');
            const dialog=page.getByRole('dialog',{name:'Export CSV'});
            await dialog.getByLabel('Filename').fill(filename);
            const downloading=page.waitForEvent('download');
            const [download]=await Promise.all([downloading,dialog.getByRole('button',{name:'Export',exact:true}).click()]);
            assert.equal(download.suggestedFilename(),filename);
            const path=resolve(output,name+'-'+filename); await download.saveAs(path); await outcome('ExportInitiated');
            return {bytes:await readFile(path),path};
        }
        await focus(); await page.keyboard.press('Control+w');
        await page.getByRole('dialog',{name:'Keep your changes?'}).getByRole('button',{name:'Cancel',exact:true}).click();
        assert.deepEqual(await snapshot(),before);
        await focus(); await page.keyboard.press('Control+n');
        await page.getByRole('button',{name:'Export and keep open',exact:true}).click();
        const [keepDownload]=await Promise.all([page.waitForEvent('download'),page.getByRole('dialog',{name:'Export CSV'}).getByRole('button',{name:'Export',exact:true}).click()]);
        await keepDownload.saveAs(resolve(output,name+'-before-new.csv')); await outcome('ExportInitiated');
        assert.deepEqual(await snapshot(),before); assert.equal(await page.getAttribute('body','data-modified'),'true');
        const exported=await exportCsv('roundtrip.csv');
        assert.equal(await page.getAttribute('body','data-modified'),'true');
        assert.match(exported.bytes.toString('utf8'),/ÄΩ Unicode/); assert.match(exported.bytes.toString('utf8'),/=1\+2/);
        await focus(); await page.keyboard.press('Control+n'); await page.getByRole('button',{name:'Discard',exact:true}).click(); await outcome('New');
        async function openCsv(file) {
            await page.evaluate(()=>{document.getElementById('cells-operation').dataset.outcome='TestPending';});
            await focus(); const choosing=page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
            await (await choosing).setFiles(file);
        }
        await openCsv(exported.path); await outcome('Opened'); assert.deepEqual(await snapshot(),before);
        mark('real CSV Blob download, retained dirty state, cancel/discard and exact export/reopen');
        const fixture=Buffer.from('"comma,quote""",ÄΩ,"line\nnext"\r\n=1+2,,tail\r\n');
        await openCsv({name:'quoted.csv',mimeType:'text/csv',buffer:fixture}); await outcome('Opened');
        assert.deepEqual(await snapshot(),[['comma,quote"','ÄΩ','line\nnext'],['=1+2','','tail']]);
        const retained=await snapshot();
        for (const [filename,buffer] of [
            ['rows.csv',Buffer.from(Array(101).fill('a,b').join('\r\n'))],
            ['columns.csv',Buffer.from(Array(101).fill('a').join(','))],
            ['large.csv',Buffer.alloc(2*1024*1024+1,65)],
            ['long-cell.csv',Buffer.from('x'.repeat(16385))],
            ['wrong.xlsx',Buffer.from('a,b')],
            ['malformed.csv',Buffer.from('"unterminated')]

        ]) { await openCsv({name:filename,mimeType:'text/csv',buffer}); await outcome('Failed'); assert.deepEqual(await snapshot(),retained); }
        mark('quoted/multiline/Unicode/empty/formula-string CSV; over-limit and unsupported files preserve sheet');
        await focus(); await page.keyboard.press('Control+s'); await page.getByRole('dialog',{name:'Export CSV'}).getByRole('button',{name:'Cancel'}).click(); await outcome('Canceled');
        await focus(); const cancelPicker=page.waitForEvent('filechooser'); await page.keyboard.press('Control+o'); await cancelPicker;
        await page.locator('input[type="file"][hidden]').dispatchEvent('cancel'); await outcome('Canceled'); assert.deepEqual(await snapshot(),retained);
        await page.locator('#cells-text-input').evaluate(input=>input.dispatchEvent(new ClipboardEvent('paste',{bubbles:true})));
        assert.deepEqual(await snapshot(),retained);
        await page.evaluate(()=>{window.originalObjectUrl=URL.createObjectURL;URL.createObjectURL=()=>{throw new Error('download transport fixture');};});
        await focus(); await page.keyboard.press('Control+s');
        await page.getByRole('dialog',{name:'Export CSV'}).getByRole('button',{name:'Export',exact:true}).click();
        await outcome('Failed'); assert.deepEqual(await snapshot(),retained);
        await page.evaluate(()=>{URL.createObjectURL=window.originalObjectUrl;});
        mark('export-before-New and Close cancellation preserve edits; canceled picker, synthetic clipboard and failed download preserve sheet');
        await page.setViewportSize({width:1000,height:700});
        await page.locator('canvas').dispatchEvent('pointercancel',{pointerId:1});
        await page.locator('canvas').dispatchEvent('wheel',{deltaY:100,deltaMode:0});
        await page.waitForTimeout(150); assert.ok(Number(await page.locator('canvas').getAttribute('data-frame'))>1);
        await page.locator('.limits summary').click();
        await page.screenshot({path:resolve(output,name+'.png')});
        assert.deepEqual(errors,[]); result.pass=true;
        await context.close();
    } catch(error) {
        failed=true; result.pass=false; result.error=error.stack || String(error);
        try {result.snapshot=await page.evaluate(()=>window.cellsSnapshot?.().map((row,r)=>row.map((value,c)=>({r,c,value})).filter(cell=>cell.value)).flat()); result.clipboard=await page.evaluate(()=>({copied:window.ownCopiedCell,pasted:window.ownPastedCell})); result.focus=await page.evaluate(()=>document.activeElement?.id); result.status=await page.locator('#cells-operation').textContent(); await page.screenshot({path:resolve(output,name+'-failure.png')});} catch {}
    } finally { await context?.close().catch(()=>{}); await browser?.close().catch(()=>{}); report.runs.push(result); console.log(JSON.stringify(result)); }
}
await new Promise(resolve=>server.close(resolve));
await writeFile(resolve(output,'report.json'),JSON.stringify(report,null,2));
if(failed) process.exitCode=1;
