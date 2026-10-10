# 場景介面與字型編輯

已整理 `StartScene`、`ChessScene`、`CardScene`。固定 UI 不再於啟動時重新建立；棋盤與棋子仍沿用原本生成流程。

## 編輯位置

- 設定：場景既有設定面板下的 `Settings content`。`操作／音效／顯示／卡牌外觀／對局` 各自是獨立物件，可在編輯時切換啟用狀態查看。執行時會開啟操作分類。
- 棋子資訊：原資訊面板內的 `Close piece info`、`Piece active skill`、`Active skill availability`。位置、尺寸、字級都已保存。
- 主選單訊息：Canvas 下的 `MenuStatus`。
- 可變項目：場景根目錄 `Editable Scene Templates — 可變數量物件樣板`。根物件預設停用，避免樣板在遊戲中直接顯示。不要刪除樣板庫或移除它的引用。
- 原本引用的 UI Prefab 以保留連結的實例放進樣板庫，名稱以 `Prefab —` 開頭；管理器已指向場景實例。修改 CardImage Prefab 並套用後，場景與執行時手牌會繼承設定。本場景專用調整可編輯場景實例；其 Overrides 會優先於 Prefab。

樣板包括傷害浮窗、状态圖示、手牌光暈、不可用原因、拖曳提示、合法目標圈、出牌結果圈、卡牌特效及一次性音效。出牌結果圈及合法目標提示的顏色、粗細等可在對應元件的 Inspector 修改。

手牌、狀態、操作紀錄及同時發生的傷害數量不固定，因此執行時仍會複製場景樣板及回收副本。Unity 的 TMP 子網格、下拉選單等內部物件也仍由套件管理；這次改動的目的是將遊戲介面的設計來源放進場景，並非禁止一切 Instantiate。

UI 樣板若位於停用的樣板库而沒有上層 Canvas，可暫時將一份副本放到 Canvas 下預覽，編輯後將數值套回樣板。固定介面則可直接在原 Canvas 內編輯。執行時的資料文字、傷害數字、合法性顏色及顯示時機仍由規則更新。

## 思源黑體

預設 TMP 字型為 `Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/思源黑體-Medium.asset`。

- 已引用專案內的 `SourceHanSansTC-Medium.otf`，不依賴玩家電腦安裝字型。
- 啟用 Dynamic 及 Multi Atlas，新增中文字可在需要時補進圖集。
- TMP 全域預設與後備字型都已設定，正式場景與遊戲 UI Prefab 的既有文字也已統一。
- 保留缺字警告，避免用關閉警告掩蓋問題。現有專案中文已驗證；字型本身不涵蓋的特殊 Unicode 或 emoji 仍需另外提供合適字型或圖示。

## 維護工具

- `Tools > Chess > Presentation > Verify Saved Scene Objects`：重新載入並檢查保存的介面引用、字型與中文字。
- `Tools > Chess > Presentation > Bake Editable Scene Objects`：初次遷移／建立缺少樣板。已有樣板與固定版面不會重新生成；此工具會再次統一字型，但同字型的自訂材質會保留，平常編輯介面不需要重跑。
- 初次場景備份：`output/scene-presentation-backup`。
- 遷移與驗證紀錄：`output/scene-presentation-migration.txt`、`output/scene-presentation-verification.txt`。
