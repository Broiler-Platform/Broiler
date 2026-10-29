import { readFile, writeFile } from 'node:fs/promises';
import { resolve, basename } from 'node:path';
import assert from 'node:assert/strict';

const root=resolve('../../artifacts/web-w5');
const browsers=JSON.parse(await readFile(resolve(root,'browser/report.json'),'utf8'));
assert.equal(browsers.runs.length,3);assert.ok(browsers.runs.every(run=>run.pass));
const inspected=JSON.parse(await readFile(resolve(root,'writer-fidelity.json'),'utf8'));
assert.equal(inspected.length,3);
for(const {path,content} of inspected){
    const engine=basename(path).split('-writer-')[0];
    assert.equal(content.text,`Recovery ${engine} writer ÄΩ`);
    assert.equal(content.bold,true,`${engine}: recovered RTF must retain bold`);
    assert.notEqual(content.status,'Rejected');
}
for(const engine of ['chromium','firefox','webkit']){
    const bundle=JSON.parse(await readFile(resolve(root,`browser/${engine}-cell-recovered.json`),'utf8'));
    const rows=JSON.parse(bundle.payload);assert.equal(rows.length,100);assert.equal(rows[0].length,100);
    assert.equal(rows[0][0],`Recovery ${engine} cell ÄΩ`);
}
await writeFile(resolve(root,'fidelity.json'),JSON.stringify({pass:true,writer:inspected.length,cells:3,scope:'Bounded text/bold and pending-cell fixture recovery after forced browser termination.'},null,2));
console.log('Passed independent recovered RTF text/bold checks and exact Cells pending-value checks in all three engines.');
