# 卡片關鍵字與敘述整理

## 已接入的顯示位置

手牌、棋子資訊內的裝備卡預覽，以及主選單卡組編輯器的卡片敘述。
出現已登錄名詞時，文字會加底線；左鍵點擊顯示名詞說明，可捲動，按「關閉」或點視窗外關閉。
點擊或拖動底線詞不會同時選取卡組項目或出牌；其他卡片區域保留原有操作。
卡牌資料本身不會加入 TMP 標籤，搜尋、存檔、連線及戰鬥效果不受影響。

## 編輯字典與版型

- 字典：`Assets/Resources/CardKeywords.json`。每筆填唯一 `id`、`term`、`description`，可選填 `aliases`。
- `id` 使用英文字母、數字與連字號；新增、修改後重新進入遊戲載入。
- 同位置多個名詞吻合時，較長的名詞優先。例如「電網Token」顯示電網指示物規則，不拆成一般 Token。
- 版型：`Assets/Resources/UI/CardKeywordPopup.prefab`。可編輯背景、字體、字級、關閉按鈕及捲動區。
- 建立工具只補缺少的預製件，不會覆蓋美術調整：`Tools > Chess > Keywords > Create Missing Popup Prefab`。
- 驗證：`Tools > Chess > Keywords > Run Regression`。

初版全局名詞與屬性矩陣來自專案已保存的 `output/card-rules-current.md`（2026-10-09 Excel 摘錄）；指示物細節來自 2026-10-24 提供的轉職工作表截圖。本次沒有直接讀取或修改開啟中的 Excel。Excel 的後續編輯不會自動同步到遊戲字典。

## 簡述與 Token 資訊（2026-10-25）

轉職卡新增 `definition.shortDescription`，卡面、裝備預覽及卡組編輯器優先顯示這個欄位。留空時沿用 `description`。完整原文仍保留；不採用「城堡連結」這個新名詞。

目前依使用者提供的簡述整理護城河、電網、血腥瑪麗與聖女貞德。卡面以條件／效果／主動技能／遺言分行；護城河與電網的指示物詳細規則放在 Token 資訊內。

棋盤上的護城河／電網 Token 帶有 `BoardFieldTokenInfo`。將游標移到 Token 所在格，按「查看棋子資訊」快捷鍵（預設 I），即可閱讀其衍生說明；若啟用長按操作，也可長按查看。同格有兩種 Token 時並列顯示，若有一般棋子則優先查看該棋子。Token 沒有加入 Piece 或額外 Collider，仍不能作為操作或出牌目標。

底線點擊修正：卡片敘述位於子 Canvas，必須在該 Canvas 提供 GraphicRaycaster。綁定關鍵字時會補齊缺少的元件。回歸測試讀取 ChessScene 的實際手牌／裝備預覽樣板，驗證完整射線與點擊流程，並確認移除子 Canvas 射線元件能重現舊問題。
