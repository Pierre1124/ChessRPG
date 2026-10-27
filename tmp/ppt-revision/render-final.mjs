import fs from 'node:fs/promises';
import {PresentationFile, FileBlob} from 'file:///C:/Users/pierr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const p = await PresentationFile.importPptx(await FileBlob.load('D:/project/ChessRPG/output/presentations/ChessRPG_成果發表簡報_修改版.pptx'));
await fs.writeFile('tmp/ppt-revision/final-inspect.ndjson',(await p.inspect({kind:'slide,textbox,shape,image,table,layout',maxChars:1000000})).ndjson);
for(let i=0;i<p.slides.items.length;i++) {
 const s=p.slides.items[i];
 await fs.writeFile(`tmp/ppt-revision/final-${i+1}.png`,new Uint8Array(await (await s.export({format:'png',scale:1})).arrayBuffer()));
 await fs.writeFile(`tmp/ppt-revision/final-layout-${i+1}.json`,await (await s.export({format:'layout'})).text());
 console.log('Rendered',i+1);
}


