# 場景資產編輯位置

- `DMGSystem/FinalDMG`：固定傷害文字，沿用原本字級與排版。
- `DMGSystem/DamageResultFly`：飛向血條的數字。
- `DMGSystem/Templates/Damage calculation`：棋子頭上的來源圖示。
- `DMGSystem/Templates/Prefab — DamageCalcStep`：傷害步驟樣板。
- `SettingsUI/Panel`：設定主面板與分類頁；確認視窗也在 SettingsUI 下。
- `CardGameUI/Templates`：手牌、卡背、狀態圖示與卡牌提示樣板。
- `BoardEffects/Templates`：拖曳高亮、出牌特效、電網及護城河 Token。
- `AudioSystem/Templates`：一次性音效來源。
- `Systems/SceneTemplates`：樣板索引及無指定父物件時的生成位置。

新增的 Prefab 在 `Assets/Prefabs/SceneTemplates/<場景>/<分類>`；已有 Prefab 的物件保留原連結。
戰報 LogText、回合提示、警告與 UsingCard 也已連到 Prefab，場景位置不變。

可直接修改場景樣板；要跨場景共用變更時再 Apply 到 Prefab。Templates 容器預設關閉，避免樣板在遊戲裡顯示。預覽時可暫時開啟，完成後請關閉容器。
可變數量物件仍會在執行時複製，但都由可編輯樣板產生；棋盤與棋子沿用原本 Prefab 生成流程。

場景備份：`output/organization-backup`。
完整階層清單：`output/scene-organization.txt`。
重跑整理：`Tools > Chess > Presentation > Organize Editable Assets`。
NetworkManager 因套件限制保留在場景根目錄。巢狀 Canvas 的範圍與排序已調整，文字樣式不變。

傷害來源圖示播放時會掛在來源棋子（沒有來源則掛目標棋子）下，使用 World Space Canvas 並面向鏡頭。結束後回收至 DMGSystem。Damage calculation 樣板的 Piece anchored icon 欄位可調整偏移、世界尺寸、堆疊間距與上飄距離。棋子已不存在時使用保存的棋盤座標。
