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
