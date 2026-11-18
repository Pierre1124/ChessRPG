# SceneRoot 與進階場地外觀副本

SceneRoot 是 ChessScene 裡房間模型的父物件，包含牆、地板、柱子、書櫃、桌椅、燭台與燈光，不是單張背景圖。模型與 Prefab 在 `Assets/Room/models`、`Assets/Room/prefabs`；材質在 `Assets/Room/Materials`；貼圖在 `Assets/Room/textures`。

Mesh 的 UV 決定貼圖的位置，MeshRenderer 的材質決定顏色與表面反應。例如 `marble_1_basecolor.mat` 使用 URP 材質，含 Base Map、Normal Map、Height Map 與 Specular Map，Base Map 的 Tiling 為 3 × 3。實際凹凸細節來自這些貼圖與光照，不只是底色。

## 這次產物

`Assets/Room/AdvancedFieldVariants` 下每個主題都有獨立材質、SceneRoot Prefab、以及可單獨開啟的 `Fxx_RoomPreview.unity`。

| 卡片 | 名稱 | 外觀方向 |
|---|---|---|
| F02 | 熔岩高原 | 焦褐石材、橘紅發光 |
| F04 | 永淤沼地 | 苔綠房間、綠色發光 |
| F06 | 光之帝國 | 象牙暖白、金色發光 |
| F08 | 暗之帝國 | 深紫房間、紫色發光 |
| F10 | 罪金之域 | 古金棕色、琥珀發光 |
| F11 | 煉獄沼原 | 泥褐房間、火橘發光 |
| F12 | 宿命終結之地 | 冷灰藍色、冰藍發光 |

這是同一房間的配色與燈光變體，保留模型、UV、原貼圖與 Tiling，加上地面四周的細發光飾條；不是七張重新繪製的環境貼圖，也還不是完整的火山、沼澤等新地形。發光材質本身不保證有 Bloom 光暈。

## 預覽與使用

1. 先儲存自己手上的場景工作，再開啟任一主題資料夾的預覽場景；不用進 Play Mode。
2. 若要在正式場景試用，把對應 SceneRoot Prefab 拖入，並停用原 SceneRoot，避免重疊。Prefab 保留原房間的位置、旋轉與比例。
3. 修改主題資料夾內的材質即可獨立調整。底圖仍共用來源貼圖，所以若直接改來源圖片，所有使用者都會受到影響。

預覽場景只有房間與預覽攝影機／補光，不含棋盤玩法或 UI。

## 遊戲中的自動切換

進階場地合成動畫完成後，`LogicManager` 會透過 `FieldRoomAppearance` 停用原 SceneRoot，顯示對應 Prefab。一般場地不換房間；直接放置進階場地也會切換。進階場地佔兩個共用欄位，一次只有一種主題。棋盤、攝影機及 UI 不在 SceneRoot 內，因此保持原狀。

重新初始化對局或切換普通西洋棋模式會移除主題房間並還原原房間。F12 宿命終結之地在合成後顯示 1.5 秒（期間維持合成操作鎖），再照既有規則重新載入普通西洋棋對局。

`Assets/Resources/FieldRoomCatalog.asset` 保存七種 Prefab 的引用，確保遊戲打包時也包含外觀。美術調整直接修改各主題 Prefab 即可；需要更新對照時使用 `Tools > Chess > Presentation > Register Field Room Appearances`。

驗證選單：`Tools > Chess > Presentation > Test Field Room Fusion`，在 ChessScene 的 Play Mode 副本實際放置七組卡片，驗證合成、房間唯一性、重置及普通棋模式還原；不儲存編輯中的場景。報告在 `output/advanced-field-rooms/runtime-verification.txt`。

產生器：`Tools > Chess > Presentation > Build Advanced Field Room Variants`。它會複製已載入 ChessScene 的 SceneRoot（含當下未儲存的房間編輯），不儲存或修改原場景，並跳過已存在的主題資料夾，以免覆寫美術調整。PNG 預覽與檢查報告位於 `output/advanced-field-rooms`。
