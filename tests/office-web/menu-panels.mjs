import {chromium} from 'playwright';
import {createServer} from 'node:http';
import {readFile} from 'node:fs/promises';
import {resolve,extname,sep} from 'node:path';
import assert from 'node:assert/strict';
const roots={writer:resolve(process.env.WRITER_WWWROOT || '../../artifacts/office-menu-writer/wwwroot'),cells:resolve(process.env.CELL_WWWROOT || '../../artifacts/office-menu-cells/wwwroot')};
const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.json':'application/json','.wasm':'application/wasm'};
const server=createServer(async(req,res)=>{
 try{
  const path=new URL(req.url,'http://localhost').pathname;const [,product,...parts]=path.split('/');
  const root=roots[product];if(!root){res.writeHead(404).end();return;}
  const file=resolve(root,parts.join('/')||'index.html');if(!file.startsWith(root+sep)){res.writeHead(403).end();return;}
  res.setHeader('Content-Type',mime[extname(file)]||'application/octet-stream');res.end(await readFile(file));
 }catch{res.writeHead(404).end();}
});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const browser=await chromium.launch();
try {
 for(const product of ['writer','cells']){
  const page=await browser.newPage({viewport:{width:1280,height:900}}),errors=[];page.on('pageerror',e=>errors.push(String(e)));
  await page.goto(`http://127.0.0.1:${server.address().port}/${product}/`);
  await page.waitForFunction(product=>document.getElementById(product+'-status')?.hidden,product);
  assert.equal(await page.locator('.office-nav').isVisible(),false);
  await page.locator('canvas').click({position:{x:product==='writer'?258:80,y:12}});await page.waitForTimeout(80);await page.locator('canvas').click({position:{x:product==='writer'?285:110,y:67}});
  await page.waitForFunction(()=>document.getElementById('office-web-dialog')?.open,null,{timeout:1000});
  console.log(product+': managed Office menu opened '+await page.locator('#office-web-heading').textContent());
  await page.getByRole('button',{name:'Close',exact:true}).click();
  await page.evaluate(()=>window.dispatchEvent(new CustomEvent('broiler-open-office-panel',{detail:'information'})));
  assert.equal(await page.locator('#office-web-heading').textContent(),product==='writer'?'Document information':'Spreadsheet information');
  await page.keyboard.press('Escape');assert.equal(await page.locator('#office-web-dialog').evaluate(d=>d.open),false);
  await page.evaluate(()=>window.dispatchEvent(new CustomEvent('broiler-open-office-panel',{detail:'recovery'})));
  assert.equal(await page.locator('#office-recovery').isVisible(),true);
  await page.getByRole('button',{name:'Close',exact:true}).click();
  await page.evaluate(()=>{const s=document.getElementById('recovery-status');s.dataset.state='unavailable';s.textContent='Storage refused';});
  await page.locator('#office-web-notice').click();assert.equal(await page.locator('#office-web-heading').textContent(),'Recovery');
  assert.deepEqual(errors,[]);
  await page.getByRole('button',{name:'Close',exact:true}).click();
  await page.route('https://demo.broiler.app/**',route=>route.fulfill({contentType:'text/html',body:'<h1>Office</h1>'}));
  await page.locator('canvas').click({position:{x:product==='writer'?258:80,y:12}});
  await page.locator('canvas').click({position:{x:product==='writer'?285:110,y:41}});
  await page.waitForURL('https://demo.broiler.app/');
  console.log(product+': information, recovery, Escape, warning and Back to Office passed.');await page.close();
 }
}finally{await browser.close();await new Promise(r=>server.close(r));}
