# 主介面與牌組編輯

- 分類：全部、轉職卡、事件卡、場地卡。
- 搜尋：卡號、名稱、效果說明，可與分類及「只看已選」組合。
- 點擊卡片循環選擇 0、1、2 張；「全選」為全卡庫各 1 張。
- 初始卡牌獨立選取，只列出目前牌組內的卡片。移除最後一張時清除初始設定。
- 初始卡維持原本保證起手規則，不額外增加牌組或起手張數。
- 取消不儲存草稿；儲存後主選單更新牌組摘要。空牌組不可儲存。

場景：`Assets/Scenes/StartScene.unity`。
工具列：`DeckEditorPanel/PanelBody/Deck filters`，由 `DeckEditorFilters` 引用。
主選單摘要：`Saved deck summary`。
可直接在 Scene 中編輯尺寸、顏色和字體。一般編輯不需重新執行建立工具。

驗證選單：`Tools/Chess/Run Deck Editor Regression`，測試結束會還原玩家牌組存檔。
