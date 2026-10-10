# 卡牌資料與製作

## 正式資料

`Assets/Resources/Cards` 內有 36 個 `CardAsset`（ScriptableObject），各 12 張轉職、事件及場地卡。J13～J15 依要求排除。
名稱、條件文字、描述與標籤依 2026-10-09 的 `Chess!.xlsx` 更新；罪金之域依使用者更正為無屬性傷害。
遊戲自動讀取資料夾中的資產，依卡號排序；牌組與連線仍使用原卡號。
每次建立卡庫會深複製定義，對局層數、充能及剩餘回合保存在執行期物件，不寫回資產。

## 新增或修改

1. 在上述資料夾右鍵選 `Create > Chess RPG > Card`，或複製相近卡片。
2. 設定唯一 `definition.id`、名稱、類型、目標、圖片、描述。
3. 一般事件啟用 `useDataActions`，在 `playActions` 依順序加入傷害、回復、套用狀態或全場套用狀態。數值直接填入 `amount`。
4. 持續效果放在 `statusesToApply`，設定觸發時機、回合數、充能、效果類型與數值；轉職走法使用 `jobChangeDefinition.moveRules`。
5. `randomDamageTypes` 每次觸發選一個屬性。魔法師為火／電；煉獄沼原為火／毒，同一次範圍觸發共用抽選結果。
6. 儲存後重新進入對局以重建卡庫。雙方連線版本應使用相同資產。

`specialRuleId` 指向既有特殊規則。一般事件卡可留空並使用資料效果；複製特殊卡時不要誤以為改卡號就會創造新的規則。
血腥瑪麗／貞德的友方增傷、禁療、遺言傷害已提供獨立欄位。

## 特殊規則的邊界

ScriptableObject 儲存資料，並不自動解析中文描述。新增觸發種類、主動技能操作、場地合成、特殊勝負或棋盤 Token 行為仍需對應程式。
既有場地加成、合成及特殊棋盤規則仍由 `LogicManager`／`CardSkill` 執行，部分數值仍在專用規則中；只改描述不會改變這些規則。

本次已更新 J04 的新版資料，但電網的「主動關閉／下回合自動重啟」及新版通過 Token 傷害尚未接入操作與連線指令；現有電網仍使用舊版阻擋規則。此項不能視為已完成的新版玩法。
毒與詛咒的重複施放疊層規則仍待確認，目前沿用既有一層／刷新時間，並在資產 `ruleNotes` 標示。

## 遷移與驗證

- `Tools > Chess > Cards > Migrate Spreadsheet Cards`：只建立缺少的資產，保留既有資產，避免覆寫手動調整。
- `Tools > Chess > Cards > Verify Card Assets`：檢查本次 36 張基準卡的 Excel 快照、素材、副本隔離與隨機屬性。新增卡之後需同步調整此基準測試的數量。
- ChessCard Inspector 的「檢查卡牌定義與素材」檢查目前整個卡庫。
- `Assets/Editor/CardSpreadsheetSnapshot.json` 是此次 Excel 的快照，不是即時 Excel 匯入器。之後改 Excel 不會自動覆寫資產。
- 舊版卡牌工廠僅在 `UNITY_EDITOR` 下保留供遷移，遊戲正式載入路徑只使用資產。

驗證輸出：`output/card-assets-regression.txt`、`output/managed-rules-regression.txt`、`output/damage-panel-regression.txt`、`output/classic-chess-regression.txt`。
