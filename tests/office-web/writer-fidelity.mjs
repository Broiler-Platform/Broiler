import { readFile, writeFile } from 'node:fs/promises';
import { resolve, basename } from 'node:path';
import assert from 'node:assert/strict';

const root = resolve('../../artifacts/web-w3');
const fixtures = JSON.parse(await readFile(resolve(root, 'fixtures/fixtures.json'), 'utf8'));
const contents = JSON.parse(await readFile(resolve(root, 'contents.json'), 'utf8'));
const browserReport = JSON.parse(await readFile(resolve(root, 'browser/report.json'), 'utf8'));
assert.equal(browserReport.runs.length, 3);
assert.ok(browserReport.runs.every(run => run.pass));
const rows = [];
for (const engine of ['chromium', 'firefox', 'webkit']) {
    const files = new Map(contents.filter(item => item.path.replaceAll('\\', '/').includes('/' + engine + '/')).map(item => [basename(item.path.replaceAll('\\', '/')), item.content]));
    for (const { format, expected } of fixtures) {
        const actual = files.get('roundtrip.' + format); assert.ok(actual, `${engine} missing ${format}`);
        for (const property of ['text', 'paragraphs', 'bold', 'italic', 'underline', 'links']) assert.deepEqual(actual[property], expected[property], `${engine}/${format}/${property}`);
        rows.push({ engine, format, text: true, paragraphs: actual.paragraphs, bold: actual.bold, italic: actual.italic, underline: actual.underline, links: actual.links.length });
    }
    for (const file of ['styled.rtf', 'redone.rtf']) {
        assert.equal(files.get(file).text, 'Alpha ÄΩ');
        for (const style of ['bold', 'italic', 'underline']) assert.equal(files.get(file)[style], true, engine + '/' + file + '/' + style);
    }
    for (const style of ['bold', 'italic', 'underline']) assert.equal(files.get('undone.rtf')[style], false);
    assert.equal(files.get('save-before-new.rtf').text, 'Alpha ÄΩ');
    assert.equal(files.get('handle.rtf').text, 'Handle text newer');
    assert.equal(files.get('image.docx').images, 1, engine + ' embedded image retained');
    assert.ok(files.get('font.rtf').fonts.includes('NonexistentTestTypeface'), engine + ' requested font name preserved');
    assert.equal(files.get('long.md').paragraphs, 1000); assert.match(files.get('long.md').text, /Paragraph 999:/);
    assert.equal(files.get('lossy.md').underline, false);
}
await writeFile(resolve(root, 'fidelity.json'), JSON.stringify({ checked: new Date().toISOString(), rows, pass: true }, null, 2));
const lines = ['# Writer browser fidelity sample results', '', 'These are bounded fixture results, not full format conformance.', '', '| Engine | Format | Text | Paragraphs | Bold | Italic | Underline | Links |', '| --- | --- | --- | --- | --- | --- | --- | --- |'];
for (const row of rows) lines.push(`| ${row.engine} | ${row.format} | Pass | ${row.paragraphs} | ${row.bold} | ${row.italic} | ${row.underline} | ${row.links} |`);
lines.push('', 'Markdown underline=false is an explicit format limitation, surfaced in Writer notes. Embedded DOCX PNG, font-name preservation, 1,000 paragraphs, styled undo/redo, pending-write races and save-before-New have separate assertions.');
await writeFile(resolve(root, 'fidelity.md'), lines.join('\n') + '\n');
console.log('Passed 15 engine/format comparisons and document-state/image/font/large-document assertions.');
