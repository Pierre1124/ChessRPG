# 2026-09-21 實作與驗證紀錄

已完成四項實作：

1. 連線資料：私有手牌格式、對手張數、指定接收者傳送、新舊協定隔離；來源、封包型別/大小/座標、序號與開局牌組驗證；演出、升變與回合限制。
2. 測試：資料與核心規則測試 185 項通過；擴充後的本機 Play Mode 回歸全部通過，包括實際手牌套用、F12 三條觸發路徑、RPG UI 停用、吃子、升變、吃過路兵、易位、將死與回復 RPG。
3. 類別拆分：抽出 NetworkInputPolicy、PrivateCardState、FieldCardRules、CardNumericRules、CardPresentation，保留既有 MonoBehaviour 與 Inspector 綁定；新增方法附中文註解。
4. 製作與操作：ChessCard Inspector 定義/素材檢查、牌組操作提示與儲存結果、連線失敗提示、牌組同步等待文字、錯誤回報只送給操作發起人。

驗證方式與命令見 Tools/Verification/README.md。Runtime 與 Editor 組件編譯零錯誤。獨立命令列執行其中 175 項無場景測試；這 175 項包含在上述 185 項中，不能重複加總。

尚未執行 Photon 雙端驗收；未新增完整棋盤快照校正或斷線續局。大型類別已抽出上述獨立職責，回合/戰鬥演出協調仍保留原入口。
