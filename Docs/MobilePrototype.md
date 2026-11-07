# Android 真機原型

## v3：直式與橫式自動配置

- 保留原有橫式介面，手機旋轉到直式時以 1080 寬度重新排列現有 UI；支援 9:16 與較長螢幕，套用安全區。
- 直式主選單加寬按鈕；牌組編輯改兩欄清單。棋局上方放回合與血量，中間完整呈現棋盤，下方放手牌、抽牌、回收及操作列。
- `MobilePortraitLayout` 保存並還原既有 UI 數值；`CameraController` 依螢幕比例調整直式視角；`HandCardToggle` 處理直式手牌動畫與多張牌的間距。
- 旋轉時取消未完成的點擊／選牌，保留棋局進度。桌面未開啟手機預覽時沿用原場景配置。
- 手機須允許自動旋轉。APK versionCode 3，可覆蓋 v2。實體手機安裝與操作仍待驗證。
- 編輯器觸控回歸從 1080×2340 主選單開始，驗證牌組、走棋、長按、選牌、回收，再切 2340×1080 驗證點棋，最後切 1080×1920。
- v3 建置零錯誤、簽章驗證通過，APK 164,698,056 bytes。獨立編輯器副本完成 29 項觸控檢查，另有 302 項規則檢查通過；紀錄在 `output/android/verification-v3.json` 與 `touch-regression-v3.txt`。
- 觸控測試由遊戲 Update 注入事件，暫停遊戲內的真實 Mouse／Pen 裝置，避免桌面滑鼠移動取代模擬手指；結束後恢復。未更動系統滑鼠。

## 範圍

共用現有棋局、卡牌與 Photon PUN 規則；加入觸控操作、直／橫向手機控制列與安全區。這是操作驗證用開發版，不是商店發行版。

- 點棋子，再點落點。
- 點手牌，再點合法棋子／場地／回收區；也可以拖曳。
- 長按棋子或卡牌約 0.55 秒查看資訊。
- 卡牌預覽點背景關閉；棋子資訊沿用關閉按鈕。
- 右側提供取消出牌、重設視角、操作說明。
- 同時觸碰第二指取消本次棋盤／卡牌操作；本版不提供雙指相機手勢。
- 背景切換取消未完成操作並保存偏好；尚未加入網路斷線續局。
- 手機以 30 FPS 為目標，使用專案既有 Android 品質設定。

## 建置

使用專案版本 Unity 6000.0.26f1，安裝 Android Build Support、Android SDK & NDK Tools、OpenJDK。新增模組後請重新開啟 Unity。

專案輸入已統一為 Input System；場地資訊、操作紀錄與測試卡快捷鍵也改為新輸入，避免 Android 使用 Both 模式。

Unity 選單：`Tools > Chess > Mobile > Build Android Prototype APK`。

輸出：`output/android/ChessRPG-MobilePrototype-v3.apk`。

建置採用 ARM64、IL2CPP、自動旋轉（直式與左右橫式）、Development Build、Android 預設測試簽章，套件名稱 `com.chessrpg.mobileprototype`。只包含 StartScene 和 ChessScene。工具會還原暫時修改的專案建置設定。

有 USB 偵錯且授權電腦的 Android 裝置，可使用 SDK platform-tools 的 adb：

```powershell
adb devices
adb install -r output/android/ChessRPG-MobilePrototype-v3.apk
```

也可以將 APK 傳到手機，開啟檔案並依 Android 提示允許安裝。

## 編輯器驗證

- `Tools > Chess > Mobile > Toggle Mobile Preview`：切換手機 UI 預覽，再進 Play Mode。這是本機 EditorPrefs，不改動場景資產。
- `Tools > Chess > Mobile > Run Touch Regression`：以虛擬 Touchscreen 對實際 ChessScene 注入觸控事件；輸出 `output/mobile-prototype-regression.txt`，結束時還原預覽與 Play Mode 起始場景。
- `dotnet run --project Tools/Verification/Verify.csproj`：既有編譯及規則檢查。

編輯器模擬觸控不能取代實機測試。請在 16:9 與較長螢幕確認字體、按鈕、手牌及棋盤遮擋；連續遊玩 15 分鐘觀察發熱與幀率。

## 真機驗收

1. 開啟主選單、編輯牌組、開始本機 RPG 對戰。
2. 雙方走棋、吃子、升變、抽牌、出牌、回收牌，完成一局並重新開始。
3. 長按閱讀；取消出牌；誤觸第二指；拖動手指後放開，確認不會誤走棋。
4. 開關設定、切換音效、返回主選單。
5. 兩台裝置用相同暗語連線，確認雙方回合與卡牌結果一致。
6. 測試鎖屏與切背景：本版尚不承諾恢復線上對局，需重新開房。

## 實作位置

- `GameplayPointer`：每幀共用滑鼠／觸控狀態及多指取消。
- `InputManager`、`CardDragHandler`：沿用原規則入口，接上觸控。
- `MobilePrototype`、`MobileSafeArea`：只在手機或明確啟用的 Editor 預覽中建立控制列及安全區包覆；保留桌面場景的設計來源。
- `MobilePrototypeBuild`：可重複執行的 Android APK 建置入口。

後續正式手機版仍需完整的響應式介面調整、效能量測、線上背景恢復及 iOS 適配。

## 首版建置紀錄（v1，已由 v2 取代）

- APK 已成功建立，Android 建置 0 錯誤，ARM64 / IL2CPP。
- APK 簽章 v1 / v2 驗證通過；實際大小 164,695,107 bytes（約 165 MB）。
- 套件識別碼 `com.chessrpg.mobileprototype`，手機桌面名稱目前沿用 `3D_Chess`。
- 最低 Android API 23，目標 API 35；仍需在目標手機驗證相容性與效能。
- 302 項既有規則檢查及觸控 Play Mode 測試通過，包含第二次觸碰回收卡牌只結算一次。
- 詳細紀錄：`output/android/verification.json`、`output/android/build-result.txt`、`output/mobile-prototype-regression.txt`。
- 本次未連接實體手機，尚未驗證安裝啟動、實機效能或兩台手機連線。
- 為避免干擾主編輯器，本次使用 `tmp/android-build` 的建置副本；APK 已複製到主專案 `output/android`。

## v2：修正主選單無法操作

第一版主選單 StartScene 遺留 StandaloneInputModule；在僅啟用 Input System 的 Android Player 中會每幀拋出 InvalidOperationException，阻止 UI 操作。已重現例外並將主選單換成與棋局相同的 InputSystemUIInputModule 及輸入動作設定。

- 手機主選單隱藏解析度／視窗化選項。
- 新增 MobileInputBuildValidation，檢查實際打包場景，發現舊輸入模組或缺少新輸入動作就停止建置。
- 觸控回歸測試改從 StartScene 起跑，透過手指事件開關牌組編輯及按開始遊戲，再完成棋局測試；任何執行期錯誤都會讓測試失敗。
- v2 使用相同套件識別碼與測試簽章，versionCode 提升為 2，供覆蓋安裝。
- v1 的 APK 不應繼續使用。

### v2 驗證結果

Android 建置 0 錯誤，APK 大小 164,698,964 bytes。簽章驗證通過，與 v1 相同，可覆蓋安裝。24 項從主選單起跑的觸控檢查及 302 項規則檢查通過；未連接實體手機，因此仍需手機覆蓋安裝驗證。詳見 `output/android/verification-v2.json` 與 `output/android/touch-regression-v2.txt`。

