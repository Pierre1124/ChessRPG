import fs from 'node:fs/promises';
import {PresentationFile, FileBlob} from 'file:///C:/Users/pierr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const p=await PresentationFile.importPptx(await FileBlob.load('D:/【聯成台中站前範本】成果發表簡報 (2).pptx'));
const layouts=await Promise.all(p.slides.items.map((_,i)=>fs.readFile(`tmp/ppt-revision/layout-${i+1}.json`,'utf8').then(JSON.parse)));
function shape(n,name){return p.resolve(layouts[n-1].elements.find(e=>e.kind==='textbox'&&e.name===name).id)}
function write(s,txt,size=24,color='#695D46',position){
 s.text=txt.split('\n').map(t=>({runs:[{run:t,style:{fontSize:`${size}px`,typeface:'Arial',color,bold:t.length<14 && size>=19}}],bulletCharacter:'',marginLeft:0,indent:0,spaceBefore:0,spaceAfter:size>=23?1100:400}));
 s.text.style={fontSize:size,typeface:'Arial',color,bold:false,alignment:'left',verticalAlignment:'top',autoFit:'none',insets:0};
 if(position)s.position=position;
}
function title(n,t){const s=shape(n,'PlaceHolder 1');write(s,t,30.48,'#EF6C00');s.text.style={bold:true,typeface:'PT Sans Narrow'};}
function body(n,t,size=24,pos){const name=({6:'PlaceHolder 4',7:'PlaceHolder 6',8:'PlaceHolder 9',9:'PlaceHolder 5'})[n]||'PlaceHolder 2';write(shape(n,name),t,size,'#695D46',pos);}
const left={left:42,top:96,width:376,height:396};
title(2,'簡報大綱');
body(2,'01  自我介紹\n02  遊戲特色與操作流程\n03  RPG 規則與傷害屬性\n04  核心技術與目前成果\n05  開發難題與解決方式\n06  後續規劃與職涯目標\n07  學習心得',24,{left:60,top:96,width:820,height:410});
body(3,'程式學習\n從小學 Scratch 開始，陸續接觸 App Inventor、\nHTML、CSS、Python 與 C#。\n學歷與工作\n嶺東資管系學士，目前擔任 MIS 網管工程師，\n曾在電商業擔任行政助理。\n課後練習\n每週約 20～30 小時，持續累積實作經驗。',24,{left:60,top:96,width:830,height:410});
title(4,'主選單與牌組編輯');
body(4,'雙人回合制的西洋棋卡牌遊戲，\n結合標準走法與 RPG 戰鬥。\n主選單提供本機測試、線上對戰\n與牌組編輯。\n卡庫共 36 張：轉職、事件、場地\n各 12 張，可分類、搜尋及篩選。\n各卡可選 0～2 張，另外指定起手卡。\n儲存後更新摘要，取消保留原牌組。',20,left);
title(5,'線上對戰與房間');
body(5,'使用 Photon PUN 建立雙人房間。\nHost 建立房間，Client 加入房間。\n雙方輸入相同暗語後進入同一局。\n主機驗證操作，再同步棋局、\n卡牌狀態與傷害演出。',20,{...left,height:180});
write(shape(5,'文字方塊 7'),'連線提示\n房間不存在、已滿或連線異常時，\n介面顯示失敗原因。\n手牌同步只提供自己的牌面，\n對手僅收到手牌張數。',20,'#695D46',{left:42,top:307,width:376,height:188});
title(6,'抽牌、回收與出牌');
body(6,'開局取得牌組設定的起手卡。\n每回合可抽 1 張，移動後停止抽牌。\n手牌拖到回收格，可重新取得抽牌機會。\n符合條件時，卡牌與合法目標會提示。\n拖曳至棋子或場地區即可使用。\n無法使用時顯示原因，不消耗卡牌。',18,{...left,height:207});
title(7,'HP、勝負與特殊走法');
body(7,'雙方各有 100 HP，吃子與卡牌能造成傷害。\n對手 HP 歸零或國王被將死即可獲勝。\n血腥瑪麗等特殊卡可改變勝負判定。\n長槍兵改變兵的走法，護城河限制路徑。\n出牌前會預覽棋盤變化並檢查國王安全，\n防止卡牌造成非法將軍或暴露己方國王。',18,{...left,height:197});
title(8,'場地卡與合成');
body(8,'場上最多放置 2 張場地卡。\n場地效果可影響雙方，需配合牌組使用。\n符合配方的場地會合成進階卡。\n例如「火山＋火山」合成「熔岩高原」，\n讓持續傷害成為另一種取勝手段。\nF12 另支援切換普通西洋棋規則。',18,{...left,height:187});
title(9,'RPG 規則與關鍵字');
body(9,'保留西洋棋基本規則\n吃子、吃過路兵、升變、易位、\n將軍與將死。\n加入卡牌與狀態\n裝備轉職、施放事件、配置場地，\n在指定時機觸發效果。\n資訊提示\n卡牌說明提供關鍵字解釋，\n協助理解增傷、減傷與特殊規則。',18,{left:42,top:94,width:320,height:395});
title(10,'傷害屬性與修正規則');
const tab10=p.resolve(layouts[9].elements.find(e=>e.kind==='table').id);
const desc=['效果說明','預設傷害類型','接受一般修正與火屬性加成','略過一般增傷與弱化','依狀態在移動或回合結束時觸發','回合結束觸發，被吃後可轉移','略過一般與屬性增減傷','直接支付 HP，不套用增減傷'];
desc.forEach((t,r)=>tab10.cells.set(r,1,t));
const tab9=p.resolve(layouts[8].elements.find(e=>e.kind==='table').id);
tab9.cells.set(3,1,'最多 2 張，依卡牌規則持續生效；特定組合可合成');
tab9.cells.set(10,1,'吃子結算時加入傷害的棋子數值');
tab9.cells.set(11,1,'被吃棋子的數值，加入吃子傷害結算');
tab9.cells.set(12,1,'最終傷害歸 0');
title(11,'核心技術與目前成果');
body(11,'Unity 6 與 UGUI：棋盤互動、牌組編輯及卡牌提示。\nScriptableObject：36 張 CardAsset，資料與對局狀態分離。\nPhoton PUN：主機驗證指令、同步對局及隱藏對手手牌。\n佇列與演出流程：依序顯示修正來源、最終傷害及 HP 變動。\nPrefab 與 URP：4 款狀態待機特效已接入卡牌。\n回歸工具：涵蓋核心規則、牌組、傷害、國王安全及特效。',23,{left:56,top:100,width:850,height:390});
title(12,'開發難題與目前限制');
body(12,'特殊走法與國王安全\n出牌會改變攻擊範圍，必須同時檢查雙方國王。\n多人同步與手牌隱私\n處理重複指令、非法來源及僅傳送接收者可見資訊。\n規則計算與視覺演出\n多目標傷害、增減傷與動畫必須維持相同結算順序。\n尚待完成\n雙端連線與效能仍待驗收，完整斷線續局尚未實作。',23,{left:56,top:96,width:850,height:415});
title(13,'問題解決與驗證方式');
const s13=p.slides.items[12];
const add=s13.shapes.add({geometry:'textbox',position:{left:42,top:100,width:455,height:390},fill:'none',line:{fill:'none',width:0}});
write(add,'規則預覽\n先模擬卡牌效果、檢查合法性，再還原棋盤。\n連線驗證\n檢查來源、序號與牌組，阻擋重複或非法指令。\n可重現的回歸案例\n以火球、火山、聖騎士等組合核對傷害與演出。\n開發工具\n使用 AI 協助分析與修正，搭配 Git 保留變更，\n再以測試及實際操作確認結果。',19);
const imgs=layouts[12].elements.filter(e=>e.kind==='image');
p.resolve(imgs[0].id).position={left:530,top:405,width:155,height:77.45};
p.resolve(imgs[1].id).position={left:520,top:100,width:380,height:279.15};
p.resolve(imgs[2].id).position={left:720,top:400,width:155,height:89.57};
title(14,'已完成進展與後續規劃');
body(14,'近期進展\n完成牌組篩選、卡牌關鍵字提示與傷害分段演出。\n貞德、血腥瑪麗、電網及毒蛇已接入待機特效。\n優先補齊\n驗收雙端連線與效能；補上新版電網主動操作及同步。\n確認毒／詛咒疊層規則，持續調整平衡與操作回饋。\n後續擴充\n更多卡牌、世界觀、專屬動畫與玩家角色表現。',23,{left:56,top:96,width:850,height:410});
title(15,'合作方向與職涯目標');
body(15,'目前暫不徵求合作夥伴。\n希望先投入遊戲相關工作，累積實務開發經驗。\n以 ChessRPG 持續完善玩法與技術作品集。',26,{left:60,top:120,width:820,height:300});
title(16,'學習心得與未來期許');
body(16,'學習心得\n透過持續練習與完成專案，逐步提升自己的程式能力。\n專案實作\n從棋盤規則延伸到卡牌系統、多人連線與戰鬥演出。\n未來目標\n進入遊戲公司，參與大型專案並累積團隊開發經驗。',24,{left:46,top:105,width:860,height:360});
const refs={4:'Docs/DeckEditor.md; Docs/CardAuthoring.md; Assets/Scripts/ChessCard.cs',5:'Tools/Verification/README.md; Assets/Scripts/Networking/MULTIPLAYER_README.md',6:'README.md; Docs/DeckEditor.md',7:'output/card-king-safety-regression.txt; README.md',8:'Assets/Scripts/FieldCardRules.cs; Tools/Verification/README.md',9:'Docs/CardKeywords.md; README.md',10:'Assets/Scripts/CardDamageRules.cs; Docs/CardAuthoring.md',11:'Docs/CardAuthoring.md; Docs/DamagePresentation.md; Docs/CardIdleEffects.md; output/card-idle-assets.txt; output/card-idle-live.txt',12:'Tools/Verification/README.md; Docs/CardIdleEffects.md',13:'output/card-king-safety-regression.txt; Tools/Verification/README.md; Docs/DamagePresentation.md',14:'Docs/DeckEditor.md; Docs/CardAuthoring.md; Docs/CardIdleEffects.md; Docs/CardKeywords.md'};
for(const [n,src] of Object.entries(refs)) p.slides.items[Number(n)-1].speakerNotes.text=`專案進度截至 2026-10-27。資料來源（D:/project/ChessRPG/）：${src}。\n畫面沿用原簡報中的實際遊戲截圖。既有回歸紀錄不等同雙端實機驗收。`;
for(let i=0;i<p.slides.items.length;i++){
 const s=p.slides.items[i];
 await fs.writeFile(`tmp/ppt-revision/after-${i+1}.png`,new Uint8Array(await(await s.export({format:'png',scale:1})).arrayBuffer()));
}
await(await PresentationFile.exportPptx(p)).save('D:/project/ChessRPG/tmp/ppt-revision/candidate.pptx');
console.log('Saved candidate and 16 previews');


