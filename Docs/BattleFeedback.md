# 音效與狀態特效接口

## 個別狀態
在 `Assets/Resources/Cards` 選卡牌資產，展開 `Definition > Statuses To Apply`：

- `Trigger Effect Prefab`：狀態觸發的單次特效。
- `Idle Effect Prefab`：狀態持續期間跟隨棋子的特效；移除狀態時清除。循環播放需在 ParticleSystem 或 Animator 的 Prefab 中設定。
- `Trigger Sound`：狀態觸發音效。
- `Trigger Effect Lifetime`：單次特效存活秒數。
- `Effect Offset`：相對棋子的局部位置。
- `Trigger Sound Volume`：此狀態的音量倍率。

空欄位會略過，不需放置假素材。Prefab 可直接指定，不必重新烘焙樣板庫。
具有傷害／回復演出的狀態，以及增減傷狀態，均在對應圖示跳出的同一幀播放；沒有圖示演出的其他規則型觸發仍在效果執行時播放。

## 共同音效
場景的 `AudioSystem > BattleFeedback` 元件：

- `Card Use Sound`：卡片使用演出開始。
- `Damage Sound`：血條實際降低時播放（零傷害不播放）。
- `Heal Sound`：血條實際增加時播放。
- `Draw Sound`：一次成功抽牌播放一次，即使該次抽取多張。
- `Recycle Sound`：回收成功。
- `Volume`：共同音量倍率，仍受 Settings 音量與靜音控制。

素材欄位預設留空，請拖入自己的 AudioClip／特效 Prefab。
各場景保有自己的共用音效設定；現有卡片動畫音效不會被替換。

## 屬性數字與事件回饋
在各場景 `AudioSystem > BattleFeedback > Presentation Styles` 編輯：

| Type | 用途／預設顏色 |
| --- | --- |
| Neutral | 無屬性，白色 |
| Fire | 火，紅色 |
| Poison | 毒，黃綠色 |
| Curse | 詛咒，紫紅色 |
| Electric | 電，紫色 |
| TrueDamage | 真實傷害，白色 |
| Cost | 代價，橘色 |
| Heal | 回復，綠色 |
| AntiHeal | 禁療，灰色 |
| Trigger | 一般來源／效果圖示觸發 |

每類可設定 `Icon`、`Number Color`、`Font Style`、`Number Scale`、`Effect Prefab`、`Effect Offset`、`Effect Lifetime`、`Sound` 與 `Sound Volume`。
數字沿用原本的 TMP 字體；左側圖示位於 `DamageResultFly/TypeIcon`，大小可直接在 Scene 或對應 Prefab 修改。
電圖示暫用既有電網技能圖，真傷暫用攻擊圖；可替換成自己的專用圖示。新音效／特效欄位預設留空。

- 類型音效／特效在最終數字出現時播放，零傷害不播放造成傷害回饋。
- 一般 Trigger 隨每個來源圖示播放；禁療隨阻止回復的來源圖示播放，結果為 0 時改顯示禁療樣式。
- 特效在受影響位置播放，物件收納於 `DMGSystem/TypeEffects`，到時或取消演出自動清理。
- 類型音效跟隨圖示跳出；共同扣血／回血音效在血條實際改變時播放。初次同步與相同數值刷新不播放共同音效。
- 連線序列會保存已決定的屬性，客戶端不重新隨機。雙方須更新至同版程式。
- 吃子最終數字仍是一個合計，不把不受減傷的「價值」拆成第二次扣血；外觀設定不改計算規則。

## 場地卡發動呈現（2026-10-25）

場地卡的 Activation／增傷／減傷演出改用獨立路徑：來源場地欄位顯示與可使用手牌相同的 CardGlowGraphic，目標不掛場地來源圖示或通用 Trigger 特效，改播 slash。數字與傷害結算維持既有流程。

樣板：`Assets/Resources/UI/FieldEffectFeedback.prefab`。測試素材使用 `Assets/slash5-HungNguyen/prefab/slash/white-blue bolder.prefab`；可在樣板調整 Target Effect Prefab、位置、旋轉、縮放及生命週期。光暈沿用玩家的手牌光暈樣式、顏色與亮度設定。

同一來源的多目標效果共用光暈，各目標分別播放 slash；來源卡移除後不留下光暈。一次性特效使用真實時間清理，取消演出及切換普通西洋棋時一併清除。

驗證選單：`Tools > Chess > Presentation > Verify Field Effect Feedback`。結果：`output/field-effect-feedback.txt`。

## 共用傷害修正提示（2026-10-27）

非場地卡的 skillEffectPrefab／skillSound、卡牌動畫特效音效、狀態待機與觸發特效音效引用已清除。素材檔保留，場地卡設定保留；棋子移動及吃子動畫不受影響。載入非場地卡時也套用相同規則，避免舊個別設定重新生效。

共用配置在 `Assets/Resources/DamageModifierFeedback.asset`：傷害上升使用 ATK 圖示與「振奮士氣」音效，傷害下降使用 AntiATK 圖示與「能力下降」音效，可在 Inspector 替換及調整音量。

依實際 contribution 正負判定，因此增傷、易傷為上升，弱化、減傷為下降。傷害沒有變化、基礎傷害或治療計算不播放這組提示。同輪多目標的同方向音效只播放一次；場地修正圖示放在來源場地卡，保留其既有光暈及 slash。

驗證：`Tools > Chess > Presentation > Verify Shared Damage Modifiers`，輸出 `output/shared-damage-modifiers.txt`。


2026-10-27 修正：轉職卡保留狀態待機特效（idleEffectPrefab），已還原電網、血腥瑪麗、聖女貞德先前的待機引用。非場地卡的個別觸發特效／音效仍停用，共用傷害上升／下降提示維持不變。

### 攻守修正圖示（2026-10-28）
- 增傷、弱化：顯示在本次攻擊發動者頭上；使用傷害屬性圖示，無屬性時用 ATK。
- 減傷、易傷：顯示在受擊者頭上，使用 DEF 盾牌。
- 增傷／減傷搭配 Arrow_up；弱化／易傷搭配 Arrow_down 並旋轉 180 度（原素材朝上）。
- 場地仍播放原場地效果，修正圖示不再掛在場地卡上；轉職待機特效保留。
- 圖示與箭頭可在 Resources/DamageModifierFeedback 及 Damage calculation 樣板調整。

### 場地來源與箭頭音效（2026-10-28）
- 上箭頭一律播放 increaseSound，下箭頭一律播放 decreaseSound；例如減傷是盾牌上升，因此與增傷共用上升音效。
- 場地增減傷及發動提示（含圖示與場地觸發特效）固定顯示於對應場地，群體傷害不在每顆棋子重複播放來源提示。
- 最終傷害數字、屬性圖示及受傷特效仍依每個目標各自顯示在棋子上。

### 來源分階段演出（2026-10-29）
- 演出順序：發動 → 增傷 → 減傷 → 弱化 → 易傷 → 實際傷害。每階段收起圖示後才進下一階段，沒有參與結算的效果不補播。
- 同階段的不同來源同步播放；血腥瑪麗的增傷顯示在血腥瑪麗上，場地加成顯示在場地上。
- 修正圖示採來源的屬性限制：無限制使用物理 ATK；火山使用火圖示。modifierTags 隨聯機演出資料傳送。
- 箭頭音效已移至各場景 AudioSystem / BattleFeedback 的「共同音效」Modifier Up Sound、Modifier Down Sound，不再由 DamageModifierFeedback 資產持有。
- 魔法師原本無特效、無音效的動畫項目仍留下 effectLifetime=2；現在僅實際有素材的項目才延長等待，也移除非吃子技能的額外 captureDelay。
- E06 虛弱的弱化只影響持有者造成的傷害；持有者受擊時僅其易傷參與承傷結算。

### 上下箭頭統一 Presentation Styles（2026-10-29）
- AudioSystem / BattleFeedback / Presentation Styles 新增 ModifierUp、ModifierDown，保存箭頭 Icon、Sound、Sound Volume、Effect Prefab、Effect Offset、Effect Lifetime。
- 原共同音效的 Modifier Up/Down Sound 已移除，原音檔已搬到對應樣式；Effect Prefab 留空供指定共用特效。
- 每個可見來源播放該方向的特效，同輪同方向音效只播一次；場地效果定位在場地上。
- 有特效時等待 Effect Lifetime 再進下一階段。箭頭以樣式 Icon 優先，ModifierDown 仍旋轉 180 度。

### 修正場地增減傷重複斬擊（2026-10-29）
- 場地修正階段僅使用 FieldEffectFeedback 的光暈，不再播放其測試用 targetEffectPrefab。
- ModifierUp／ModifierDown 的特效由 Presentation Styles 單獨負責；場地發動階段仍保留原先發動特效。

### 黑市與狀態待機（2026-11-01）
- 黑市回收觸發在修改能力後，排入「場地發動 → 場地能力上升」演出，使用 ModifierUp 共用音效／特效。
- 能力演出以 presentationOnly 標記傳送給聯機對手，不顯示虛假的傷害數字或飛向血條。
- 所有卡片的狀態皆保留 idleEffectPrefab。待機依持有狀態、剩餘回合與充能判斷，不再限制為轉職卡。
- 狀態充能用盡時隱藏待機，恢復充能時重新顯示；移除、到期與普通棋模式會清除。
- 待機素材設定於各 Status Definition 的 Idle Effect Prefab 與 Effect Offset。

### 箭頭圖示與特效同時顯示（2026-11-01）
- 移除「設定 3D 特效後隱藏 UI 箭頭」判斷，箭頭 icon 與 ModifierUp／ModifierDown 特效並存。
- BattleFeedback 生成特效時保留 Prefab 原始旋轉，不再強制 Quaternion.identity。Animator 使用 UnscaledTime 與 AlwaysAnimate，與演出計時及清除一致。
- Unity 驗證走實際 CreateTypeEffect 生成路徑，確認箭頭可見且上升／下降方向正確。

### 黑市受益棋子與世界特效根節點（2026-11-01）
- 黑市場地保留發動提示；presentationOnly 的能力變化圖示、ModifierUp 特效與音效改在受益棋子上播放，網路 iconPiece 同步對應目標。
- ChessScene 的 TypeEffects 位於縮放為 0 的 UI Canvas 下。世界特效現在遇到 Canvas 或零縮放父節點會保留在世界根節點，避免生成後被縮沒；仍由 BattleFeedback 追蹤與清除。
- 驗證改用實際 typeEffectRoot，不再以 AudioSystem transform 代替。

### 全部計算演出統一樣式與來源／目標定位（2026-11-01）
- 計算流程的圖示、音效、特效只取 AudioSystem / Presentation Styles；移除場地斬擊、狀態觸發素材、卡片圖片與 DamageModifierFeedback 素材回退。
- 發動使用 Trigger 音效與特效，圖示取傷害屬性；修正使用 ModifierUp／ModifierDown，盾牌圖示新增 Defense 樣式。
- 有明確來源棋子的技能／增傷顯示於來源棋子；場地來源使用對應 FieldCardPlace。無棋子來源（如火球）使用 Card Effect Source Anchor，未指定則使用同場景第一個場地欄位。
- 減傷、易傷、實際傷害與黑市能力變化顯示在目標；來源提示不再借用受擊者位置。
- 事件卡傷害也建立 Activation 階段。卡片 OnDamageDealt / OnDamageModifier / OnHealed 的個別動畫不再另播計算特效。
- 待機狀態特效不屬於計算演出，繼續由持有狀態管理。

### 火球重複提示與箭頭動畫定位（2026-11-01）
- 事件卡直接傷害不額外建立 Activation 階段；火球顯示出牌後直接進入實際適用的修正與傷害演出。
- 所有 Presentation Styles 世界特效使用獨立定位外層，動畫 Prefab 放在內層。動畫即使寫入根節點座標，也不能覆蓋發動者／目標的世界位置。
- 驗證使用 (7,2,5) 的非原點位置與實際 TypeEffects 設定，檢查上升、下降動畫更新後外層與箭頭仍維持在指定位置。

### 中毒與詛咒狀態（2026-11-01）
- E09 中毒狀態接回 VenomBloom 循環待機；E05 詛咒狀態接回 CurseChains 循環待機。
- 狀態觸發的 Activation iconPiece 使用 sourceStatus.owner，與傷害歸屬 source 分開。來源為事件卡、原施加者不存在、詛咒轉移時，都定位在目前持有者；透過既有 iconPiece 網路欄位同步。
- 觸發圖示與傷害仍使用 Presentation Styles；持續待機使用 Status Definition 的 idleEffectPrefab，隨狀態移除／到期清除。
