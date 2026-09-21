# 多人連線架構

目前使用 Photon PUN。`NetworkSessionLauncher` 設定 `PhotonNetwork`、建立固定雙人房間並同步場景；輸入的位址實際是房間名稱。需要有效的 PhotonServerSettings AppIdRealtime；不是 NGO、Relay 或 IP 直連。

## 命令與驗證

`MultiplayerGameController` 接收客戶端命令，主機透過 `LogicManager`、`CardHandManager` 執行規則。`NetworkInputPolicy` 集中驗證事件來源、資料型別、長度、棋盤座標、每位玩家遞增序號與牌組限制。

- 操作要求只有主機處理；權威結果及狀態只接受目前主機發送。
- 主機檢查陣營、回合、等待狀態、升變、場地合成及演出鎖。
- 升變要求走獨立入口，避免被升變自身的操作鎖阻擋，並共用玩家序號去重。
- 牌組必須非空、所有卡號存在、每種最多兩張。整副驗證成功後才取代既有資料；遠端玩家每局只能成功提交一次。
- 拒絕命令只通知提出操作的玩家。

## 私有資料

`PrivateCardState` 的 version 1 格式只含接收者陣營、該玩家手牌及對手手牌張數。牌堆順序和對手卡號不送出；含手牌的狀態/移動結果只發給指定對手。客戶端用公開張數畫牌背，不需要建立對手 CardDefinition 清單。

Photon GameVersion 使用 `Application.version + "-private-cards-v1"`，新舊協定不可互連。F12 使用房間模式屬性同步普通棋局；此模式不傳卡牌資料，也不執行 RPG 步驟。

## 現有限制

主機是受信任權威，持有完整資料。此版本不提供獨立伺服器層反作弊。`NetworkGameSnapshot` 仍是簡要模型；完整棋盤快照校正、斷線續局尚未實作。部分卡牌效果仍由客戶端重播，含隨機效果的雙端一致性需要實機驗收。

## 驗證

參考 [驗證工具](../../../Tools/Verification/README.md)。測試雙方主機黑白陣營、正常卡牌操作、偽造來源、重複序號、非法牌組、私有手牌及 F12 場景同步。Unity 本機測試不取代 Photon 雙端驗收。
