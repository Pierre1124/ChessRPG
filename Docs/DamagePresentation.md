# 傷害演出

規則仍先計算最終傷害；演出不改變增減傷套用順序或數值。

1. 事件卡先完成展示與縮小至棋子的動畫，才套用傷害、治療或狀態。技能／狀態效果以 Activation 顯示來源圖標（含聖職者、電網、護城河），再進入兩階段修正。
2. FinalDMG 先顯示基礎數值；Increase 的增傷／易傷圖示同時浮出，演完才加上該階段的修正。
3. Decrease 的減傷／弱化圖示同時浮出，演完更新 FinalDMG 至正式結果。例如火球＋火山＋聖萊恩王國：5 → 6 → 5。
4. 顯示每個受傷目標的最終數字，停留後加速飛往對應玩家血條。
5. 抵達時套用血量，傷害震動血條。治療不震動；0 顯示後收起、不飛行。

多目標共用兩個階段，同一来源及位置在同階段只演一次。來源圖示使用現有 Scene Templates 的 Damage calculation，只顯示圖示，不顯示名稱、數字或底板。多目標在 FinalDMG 各列一行，傷害與回復分開。聖騎士位置取提供減傷的棋子，場地提示取受影響棋子位置。

## 編輯

- DMGSystem / DamageCalculationVisualizer：Step Duration、Capture Delay、Result Hold Duration、Result Offset、Fly Duration、Flight Acceleration、Health Shake Duration / Distance。
- Editable Scene Templates / Damage calculation：來源圖示、來源名稱、浮窗外觀。
- DMGSystem / FinalDMG：計算中的固定文字，位置、字型與大小直接在 Scene 編輯。
- DMGSystem / DamageResultFly：最終數字外觀。
- CardAsset / animations：新增 OnDamageModifier 可掛專屬來源動畫；effectLifetime 同時作為該階段最少等待時間。新特效需執行既有 Bake Editable Scene Objects 將樣板加入 Scene。
- 場地尚無專屬美術動畫時，使用卡圖、名稱與上飄提示。聖萊恩王國目前使用此提示。

網路傷害步驟傳送 modifierPhase 與 sourceCardId，使雙方分組和卡牌圖示一致。中斷演出會隱藏浮窗、停止延遲結算、還原震動座標。

驗證入口：Tools > Chess > Run Damage Panel Regression。包含火球 3 + 火山 1 − 聖騎士 1 − 王國 1 = 2、分組時序、批次、零傷害、出牌等待、中斷取消與血條還原。
