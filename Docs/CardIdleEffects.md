# 卡牌待機特效（Unity URP 原型）

資產位置：`Assets/Art/VFX/CardIdle/`。

| 卡牌 | Prefab | 呈現 |
| --- | --- | --- |
| J10 聖女貞德 | SacredAegis | 金色刻印、浮動光環與光點 |
| J09 血腥瑪麗 | BloodVow | 紅色旋轉流帶與微粒 |
| J04 電網 | IonCage | 紫色電弧與充能光點 |
| E09 毒蛇 | VenomBloom | 綠色環繞流線與上升氣泡 |

四張 CardAsset 的第一個狀態 `idleEffectPrefab` 已接上對應 Prefab。沿用 `BattleFeedback.StartStatusIdle/StopStatusIdle`，不改卡牌數值、目標限制或連線規則。電網效果在持有狀態的城堡上，沒有改動棋盤電網 Token 的特效。

## 使用及調整

- 重開對局讓卡庫重新載入，再正常使用對應卡片。
- 獨立預覽：`Tools > Chess > Presentation > Idle Effects > Play Preview`。退出 Play Mode 會回到原本編輯的場景。預覽不在 Build Settings。
- 預覽場景：`Assets/Scenes/Previews/CardIdleFXPreview.unity`，使用正式棋子模型副本、展示專用 URP 材質，停用規則腳本；透過真正的 `Piece.ApplyStatus` 建立特效。
- Prefab 的 `CardIdleEffectMotion` 可調整 `World Radius`（預設 0.42 格）、`Loop Seconds`（4 秒）、旋轉與浮動。使用遊戲時間，遊戲暫停時停住。
- `Aura geometry` 子物件可調整 LineRenderer 顏色、寬度及粒子發射量。每款最多 32 粒子，Local 模擬，不留移動拖尾；無 Collider，不攔截滑鼠。
- 半徑不受棋子根物件縮放影響，高度依 MeshRenderer 邊界調整。若更換模型，檢查視覺比例並按需要修改 Prefab。
- `CardIdleAdditive.shader` 是 URP 加色 Shader。沒有 Bloom 仍可見；預覽場景有獨立 Bloom，沒有更改正式場景的 Volume。
- 要取消某款：清空對應狀態的 `Idle Effect Prefab` 欄位。`Create and Assign` 只建立缺少資產、填入空欄位，不覆蓋既有自訂效果。

## 驗證

- `Verify Assets`：四張卡牌引用、Shader、循環端點、Local 粒子、粒子數上限與無 Collider。
- 先 `Play Preview`，再 `Verify Live Preview`：狀態建立、重複啟動、跟隨棋子、縮放補償、狀態移除、普通模式清理及延遲 Destroy。
- 結果：`output/card-idle-assets.txt`、`output/card-idle-live.txt`。

這是將 Blender 設計重建為 Unity 原生特效，沒有匯入 Blender 的驅動器或合成器。尚未做雙端 Photon 實機對戰驗收或目標裝置效能測量。
