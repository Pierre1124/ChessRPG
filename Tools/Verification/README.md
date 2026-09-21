# 程式驗證

從專案根目錄執行：

```powershell
dotnet run --project Tools/Verification/Verify.csproj
```

需要 .NET 10 SDK、Unity 產生的 Assembly-CSharp.csproj / Assembly-CSharp-Editor.csproj，以及已匯入專案的 Library/ScriptAssemblies。工具使用 SDK 內附的 Roslyn，不下載 NuGet 套件；直接編譯現有來源及執行實際遊戲組件中的無場景測試。結果寫入 output/managed-rules-regression.txt。這不是 Unity Player 建置。

Unity 選單：

- Tools > Chess > Run Core Rules Regression：連線來源、封包、牌組、數值、狀態充能、場地合成、私有手牌 JSON。
- Tools > Chess > Run Classic Chess Regression：切換 Play Mode，驗證 F12 直接使用、合成及延後演出、RPG UI 隱藏、重新開局、吃子、升變、吃過路兵、易位與將死。請從未連線的 Edit Mode 執行；測試結束還原 Play Mode 起始場景設定，不儲存場景。

## 製作與架構

選取 ChessCard 元件，在 Inspector 底部按「檢查卡牌定義與素材」。工具列出卡號重複、缺少名稱/說明/圖片/職業定義，以及合成配方的缺失引用。素材缺少只列警示，不自動修改。卡牌技能仍使用 CardSkill.Shared。

- NetworkInputPolicy：封包型別、來源、序號與牌組驗證。
- PrivateCardState：接收者手牌及對手張數，不包含任一牌堆順序。
- FieldCardRules：場地配方與進階場地分類。
- CardNumericRules：数值運算。
- CardPresentation：卡片圖片、文字及標籤。

既有 MonoBehaviour 檔名、GUID 與 Inspector 欄位保留。此次抽出明確的獨立職責；LogicManager 的回合/棋盤協調、CardBattleSystem 的戰鬥演出及 CardHandManager 的動畫仍保留原有入口。

## 連線相容性與驗收

Photon GameVersion 加上 private-cards-v1 後綴，避免新舊協定互連。建立房間固定雙人。主機仍可讀取全部對局資料；此修改防止一般對手讀取私有手牌及偽造主機事件，不提供獨立伺服器層的反作弊。完整棋盤快照復原及斷線續局不在此次實作範圍。

雙端測試仍需實際 Photon 環境驗收：交換主機黑白方，測試抽牌/回收/裝備/場地/升變，確認只顯示自己的手牌、對手牌背張數一致，F12 兩端同時重載且無 RPG UI；確認重送命令不重複執行、非法牌組不覆寫既有資料。這部分不能以本機回歸測試取代。
