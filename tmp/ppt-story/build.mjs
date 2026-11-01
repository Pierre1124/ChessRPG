import fs from 'node:fs/promises';
import {PresentationFile,FileBlob} from 'file:///C:/Users/pierr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const source='D:/project/ChessRPG/output/presentations/ChessRPG_成果發表簡報_修改版.pptx';
const p=await PresentationFile.importPptx(await FileBlob.load(source));
const cards=JSON.parse(await fs.readFile('tmp/ppt-story/cards.json','utf8'));
const card=id=>cards.find(c=>c.id===id);
const originals=[...p.slides.items];
const template=originals[13];
function text(s,value,x,y,w,h,size=20,color='#695D46',bold=false){
 const q=s.shapes.add({name:value.slice(0,20),geometry:'textbox',position:{left:x,top:y,width:w,height:h},fill:'none',line:{fill:'none',width:0}});
 q.text=value.split('\n').map(run=>({runs:[{run,style:{typeface:'Arial',fontSize:`${size}px`,color,bold}}],bulletCharacter:'',marginLeft:0,indent:0,spaceAfter:600}));
 q.text.style={typeface:'Arial',fontSize:size,color,bold,insets:0,autoFit:'none',verticalAlignment:'top'};return q;
}
async function picture(s,id,x,y,w,h){const c=card(id);s.images.add({blob:new Uint8Array(await fs.readFile(c.imagePath)),contentType:'image/png',fit:'contain',position:{left:x,top:y,width:w,height:h},alt:`${id} ${c.cardName} 原始卡圖`});}
const specs=[
 {title:'聖女貞德與血腥瑪麗',ids:['J10','J09'],labels:['J10 聖女貞德','J09 血腥瑪麗'],
  h1:'卡牌關聯：兩種領導方式',t1:'貞德的增傷隨吃子成長，友軍損失\n會削弱額外加成。\n瑪麗移除國王，換取增傷 5 與禁療，\n自身被吃時觸發對局結束。',
  h2:'故事構想：旗幟與王座',t2:'貞德站在前線，勝敗牽動全軍士氣。\n瑪麗以自己的存亡取代王權，\n用不能回頭的代價換取力量。\n兩張卡可成為同一場戰爭的對照。',
  note:'這是依卡圖與效果延伸的角色對照，未設定兩人為既定敵手。',
  talk:'兩張卡都能強化全軍，但風險不同。貞德的效果會隨戰況增減，讓保護同伴成為策略；瑪麗則在開局條件下移除國王並禁療，把勝敗集中在自身。卡圖一張是前線旗手，一張是王座上的女王，因此可以發展為兩種領導理念。貞德需我方無棋子被吃；瑪麗另需滿 HP 且場上無任何棋子被吃。這裡討論遊戲角色，沒有宣稱歷史人物的關係。'},
 {title:'光暗勢力與宿命終結',ids:['F05','F07','F12'],labels:['F05 聖萊恩王國','F07 瑪莉埃古堡','F12 宿命終結之地'],relation:'王國＋古堡＝宿命終結之地',
  h1:'卡牌關聯：合成與對立',t1:'聖萊恩王國提供減傷，重複合成後\n成為強化回復的「光之帝國」。\n瑪莉埃古堡提高攻擊，重複合成後\n成為攻擊更強的「暗之帝國」。',
  h2:'故事構想：力量耗盡的決戰',t2:'當光與暗交會，雙方失去特殊力量。\nF12 重啟棋局、移除卡池與傷害步驟，\n讓戰爭回到純粹的西洋棋較量。\n可作為世界觀中的終局篇章。',
  note:'合成配方與重啟效果已存在；光暗決戰的情節屬故事構想。',
  talk:'F05+F05=F06 光之帝國；F07+F07=F08 暗之帝國；F05+F07=F12。這些都是實際配方。F12 卡圖也呈現明亮王國與黑暗城堡兩方交戰，因此合成規則和畫面可以共同支持光暗勢力的敘事方向。場地效果會影響雙方，並不代表玩家只能固定加入某個陣營。'},
 {title:'回春與血契：生命的時間差',ids:['E10','E11'],labels:['E10 回春','E11 血契'],
  h1:'卡牌關聯：先付或後付',t1:'回春：HP ≥ 50 時可用，先支付 10 HP，\n之後每次回合結束回復 3，持續 5 回合。\n血契：HP ≤ 50 時可用，先回復 40 HP，\n之後每次回合結束支付 5，持續 10 回合。',
  h2:'故事構想：國王的求生抉擇',t2:'卡圖都描繪年老國王與生命危機。\n一條路接受眼前的痛苦，等待療癒；\n另一條路以未來生命交換眼前生機。\n可寫成同一位國王的兩條命運分支。',
  note:'同一位國王的設定屬故事構想；實際回復仍受禁療等效果影響。',
  talk:'這組的關聯同時來自卡圖和效果。回春畫面是床上的國王接受光明治療；血契畫面是國王割掌向骷髏杯獻血。規則一個先付出、後回復，一個先救急、後付出。不能把名義回復總量當成必然淨收益，因為可能受 HP 上限、禁療與對局提前結束影響。'},
 {title:'黑市與罪金之域',ids:['F09','F10','E03'],labels:['F09 黑市','F10 罪金之域','E03 強奪'],relation:'黑市＋黑市＝罪金之域',
  h1:'卡牌關聯：交易與掠奪',t1:'黑市把回收牌轉成隨機己方棋子的\n攻擊力 +1、價值 −1。\n罪金之域在抽牌時隨機選擇目標與傷害。\n強奪則支付 5 HP，奪走他人的轉職卡。',
  h2:'故事構想：戰爭中的地下市場',t2:'最初只是交換裝備、買賣力量。\n當利益擴張成賭局，暴力便失去控制，\n甚至回頭傷害交易者自己。\n強奪可作為其中一段掠奪事件。',
  note:'黑市合成已有規則；強奪與地下市場的劇情連結屬構想。',
  talk:'黑市是回收時成長，罪金之域是抽牌時造成隨機傷害。F10 描述為隨機對場上 1～6 個棋子造成 1～6 點無屬性傷害，因此不是只攻擊敵人的穩定增益。卡圖也從交易攤位轉成持槍對賭。強奪沒有與黑市的合成配方，這裡將它作為題材相關的事件卡，而非額外的系統聯動。'},
 {title:'火山與沼澤的災變',ids:['F01','F03','F11'],labels:['F01 火山','F03 沼澤','F11 煉獄沼原'],relation:'火山＋沼澤＝煉獄沼原',
  h1:'卡牌關聯：屬性與環境',t1:'火山提高火屬性傷害，沼澤提高毒傷。\n合成後同時強化兩種屬性，\n回合結束時對隨機棋子及周圍造成\n火屬性或毒屬性傷害。',
  h2:'故事構想：戰場本身成為威脅',t2:'熔岩流進沼澤，灼熱與毒霧交織。\n軍隊即使不交戰，也無法避開災變。\n火球與毒蛇可延伸成這片土地上的\n戰術手段，讓地形參與故事。',
  note:'合成與屬性效果已有規則；災變起因和事件情節屬構想。',
  talk:'F01 和 F03 提供屬性加成，F11 同時保留火與毒加成，並追加回合結束的範圍傷害。卡圖同時包含火山、積水和焦枯植物。可延伸成戰爭破壞環境或自然災變的章節，但目前沒有資產文字指定這場災變由誰引起。E04 火球及 E09 毒蛇分別呼應兩種傷害屬性。'}
];
for(let j=0;j<specs.length;j++){
 const sp=specs[j],s=template.duplicate();s.moveTo(8+j);
 const lay=JSON.parse(await(await s.export({format:'layout'})).text());
 const ttl=lay.elements.find(e=>e.name==='PlaceHolder 1');p.resolve(ttl.id).text=sp.title;
 const b=lay.elements.find(e=>e.name==='PlaceHolder 2');p.delete(b.id);
 const pair=sp.ids.length===2;
 for(let k=0;k<sp.ids.length;k++){
  const x=pair?44+k*230:42+k*150,w=pair?211:139,h=pair?286:190,y=pair?115:142;
  await picture(s,sp.ids[k],x,y,w,h);
  text(s,sp.labels[k],x,y+h+12,w,50,pair?20:17,'#695D46',false);
 }
 if(sp.relation)text(s,sp.relation,44,390,438,62,22);
 text(s,sp.h1.replace('卡牌關聯：',''),514,100,410,34,22,'#695D46',true);
 text(s,sp.t1,514,143,410,146,19);
 text(s,sp.h2.replace('故事構想：','')+'（構想）',514,299,410,34,22,'#695D46',true);
 text(s,sp.t2,514,342,410,136,19);
 text(s,sp.note,42,480,835,36,16);
 s.speakerNotes.text=sp.talk+'\n\n卡牌描述與卡圖來源：'+sp.ids.map(id=>`D:/project/ChessRPG/Assets/Resources/Cards/${id}.asset；${card(id).imagePath}`).join('\n')+'\n合成來源：D:/project/ChessRPG/Assets/Scripts/FieldCardRules.cs。資料閱讀日期：2026-10-31。故事構想為依現有資產延伸的創作提案。';
}
for(let i=0;i<p.slides.items.length;i++){
 const s=p.slides.items[i];const lay=JSON.parse(await(await s.export({format:'layout'})).text());
 for(const el of lay.elements){if(el.kind==='textbox'&&/^\d+$/.test(el.text||'')&&el.position.left>850)p.resolve(el.id).text=String(i+1);}
}
const agenda=JSON.parse(await(await originals[1].export({format:'layout'})).text());
const a=p.resolve(agenda.elements.find(e=>e.name==='PlaceHolder 2').id);
a.text=['01  自我介紹','02  遊戲特色與操作流程','03  卡牌關聯與故事構想','04  RPG 規則與傷害屬性','05  核心技術與目前成果','06  開發難題與解決方式','07  後續規劃與職涯目標','08  學習心得'].map(run=>({runs:[run],bulletCharacter:'',marginLeft:0,indent:0,spaceAfter:1000}));
a.text.style={fontSize:24,typeface:'Arial',color:'#695D46',autoFit:'none',insets:0};
for(let i=0;i<p.slides.items.length;i++) await fs.writeFile(`tmp/ppt-story/after-${i+1}.png`,new Uint8Array(await(await p.slides.items[i].export({format:'png',scale:1})).arrayBuffer()));
await(await PresentationFile.exportPptx(p)).save('D:/project/ChessRPG/tmp/ppt-story/candidate.pptx');
console.log('Created 21 slides');

