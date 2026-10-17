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
規則型觸發在狀態效果執行時播放；傷害計算中的狀態增減傷則隨圖示階段播放。

## 共同音效
場景的 `AudioSystem > BattleFeedback` 元件：

- `Card Use Sound`：卡片使用演出開始。
- `Damage Sound`：實際血量降低（零傷害不播放）。
- `Heal Sound`：實際血量增加。
- `Draw Sound`：一次成功抽牌播放一次，即使該次抽取多張。
- `Recycle Sound`：回收成功。
- `Volume`：共同音量倍率，仍受 Settings 音量與靜音控制。

素材欄位預設留空，請拖入自己的 AudioClip／特效 Prefab。
各場景保有自己的共用音效設定；現有卡片動畫音效不會被替換。
