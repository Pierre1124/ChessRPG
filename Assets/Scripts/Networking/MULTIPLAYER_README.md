# 多人連線架構說明

這份架構先把遊戲拆成「玩家操作命令」和「主機套用結果」。目前不會改壞單機流程，之後接 Unity Netcode for GameObjects 時，只要把命令送到主機驗證即可。

## 現在新增了什麼

- `NetworkCommandModels.cs`：定義可以被傳送的命令資料，例如移動、抽牌、出牌、場地卡、回收卡。
- `MultiplayerGameController.cs`：所有多人操作的入口。現在已經是 `NetworkBehaviour`，移動棋子會透過 RPC 送到 Host。
- `NetworkSessionLauncher.cs`：StartScene 按鈕可用的啟動器，先支援 Host / Client / Shutdown 的直連測試。
- `NetworkGameSnapshot`：之後主機可以用它同步回合、HP、手牌、場地卡等狀態。

## 核心觀念

多人連線不要讓兩台電腦各自計算遊戲結果。正確流程是：

1. 玩家點棋子、拖卡片、抽牌。
2. 客戶端只送出「我想做什麼」。
3. 主機檢查是否合法。
4. 主機使用既有 `LogicManager`、`InputManager`、`CardHandManager` 套用結果。
5. 主機把結果同步給雙方。
6. 雙方只播放動畫和 UI，不自行重新計算傷害。

## 之後要怎麼接

第一步先安裝套件：

- `com.unity.netcode.gameobjects`
- `com.unity.transport`
- Relay / Lobby 可用 `com.unity.services.multiplayer`

第二步建立 NetworkManager：

- StartScene 放一個 `NetworkManager`
- 掛 `UnityTransport`
- 做三個按鈕：Host、Join、Local
- Host 建立 Relay Code
- Client 輸入 Relay Code 加入

先不要急著做 Relay。第一次測試建議用直連：

- 同一台電腦測試：Client Address 填 `127.0.0.1`
- 同一個 Wi-Fi / 區網測試：Client Address 填 Host 電腦的區網 IP
- Port 先用 `7777`

第三步改操作入口：

- `InputManager.MoveSelectedPiece` 不要在 Client 直接移棋。
- Client 呼叫 `MultiplayerGameController.CreateMoveCommand(...)`
- 再把命令送到 Host。
- Host 驗證合法後，才呼叫原本的 `MoveSelectedPiece` 流程。

範例概念：

```csharp
NetworkGameCommand command =
    multiplayerGameController.CreateMoveCommand(selectedPiece, targetCoordinates);

if (multiplayerGameController.IsOnline)
{
    multiplayerGameController.SubmitCommand(command);
    return;
}

// Local 模式才繼續走原本 MoveSelectedPiece 的內容。
```

第四步改卡牌入口：

- `CardHandManager.DrawForCurrentPlayer`
- `CardHandManager.TryApplyCardToPiece`
- `CardHandManager.TryApplyFieldCardToPlace`
- `CardHandManager.TryRecycleCard`

這四個入口在連線模式下都應該改成「送命令給 Host」，不要讓 Client 自己改牌堆或棋盤。

範例概念：

```csharp
NetworkGameCommand command =
    multiplayerGameController.CreateDrawCardCommand();

if (multiplayerGameController.IsOnline)
{
    multiplayerGameController.SubmitCommand(command);
    return;
}

// Local 模式才真的 DrawCards。
```

## Inspector 要怎麼掛

在 `ChessScene`：

1. 找到 `MultiplayerGameController` 物件。
2. 確認它有 `NetworkObject`。
3. 確認它有 `MultiplayerGameController.cs`。
4. `Mode` 單機時放 `Local`。
5. Host 測試時放 `Host`，`Local Side` 放 `White`。
6. Client 測試時放 `Client`，`Local Side` 放 `Black`。
7. `Host Side` 先放 `White`。
8. `First Remote Client Side` 先放 `Black`。
9. `Logic Manager` 拖入場上的 `LogicManager` 物件。
10. `Card Hand Manager` 拖入 `CardGameUI` 上的 `CardHandManager`。

如果沒有手動拖引用，腳本會在 `Awake` 嘗試自己找，但正式製作時建議手動接好，場景會比較清楚。

在 `StartScene`：

1. 建立或選一個物件掛 `NetworkSessionLauncher.cs`。
2. `NetworkSessionLauncher` 的 `Game Scene Name` 填 `ChessScene`。
3. Host 按鈕的 OnClick 建議接 `StartMenuController.StartHostGame`。
4. Client / Join 按鈕的 OnClick 建議接 `StartMenuController.StartClientGame`。
5. 如果有 IP 輸入框，OnValueChanged 接 `NetworkSessionLauncher.SetAddress`。
6. 離線/返回按鈕可接 `NetworkSessionLauncher.Shutdown`。

`StartHostGame` 會做三件事：

- 確認牌組資料存在
- 啟動 Host
- 用 NGO 的 SceneManager 載入 `ChessScene`

`StartClientGame` 只會啟動 Client。Client 不要自己 `LoadScene("ChessScene")`，要等 Host 帶它進場。

在任一需要連線的 Scene：

1. 放一個 `NetworkManager`。
2. `NetworkManager` 物件上掛 `UnityTransport`。
3. `NetworkConfig` 的 `Network Transport` 指向該 `UnityTransport`。
4. 勾選或啟用 Scene Management，之後才比較好同步換場景。

## Host 要驗證什麼

移動棋子：

- 現在是不是該玩家的回合
- 來源格是不是他的棋子
- 目標格是不是合法走法
- 是否正在升變、傷害結算、遊戲結束

出轉職卡：

- 卡片是否真的在該玩家手牌
- 目標是否符合 `card.CanApplyTo(...)`
- 裝備後是否要刷新場地效果、CheckMap

出事件卡：

- 卡片是否真的在手牌
- 目標是否合法
- 代價是否支付得起
- 效果由 Host 執行一次

出場地卡：

- 卡片是否真的在手牌
- 是否拖到 `FieldCardPlace` 或 `FieldCardPlace2`
- 場地卡上限與合成條件由 Host 判斷

## 哪些資料需要同步

- 現在誰的回合
- 白方 HP、黑方 HP
- 每個棋子的座標、種類、顏色、裝備卡、狀態、ATK、Value
- 場地卡槽 1、場地卡槽 2
- 白方手牌 ID、黑方手牌 ID
- 牌堆順序或抽牌結果

## 哪些資料不用同步

- DamageCalcStep 物件
- 卡片拖拉中的 UI
- 棋子移動動畫
- 吃子特效
- 音效
- StatusInfo 浮窗

這些都應該由收到結果的本機自己播放。

## 新增卡片時要注意

卡片技能不要直接讀「本機玩家是誰」。技能只應該看：

- `LogicManager.isWhiteTurn`
- 目標棋子
- 來源棋子
- 卡片 ID
- 傷害類型

因為多人連線時，真正的計算會在 Host 上發生。

## 建議實作順序

1. 先維持 Local 模式，把 `MultiplayerGameController` 掛進 ChessScene。
2. 把移動棋子的入口改成可送命令，但 Local 模式仍走舊流程。
3. 把抽牌、出牌、回收卡改成可送命令。
4. 安裝 NGO / Transport / Relay。
5. 把卡牌命令補成 Host 真正驗證與執行。
6. Host 套用結果後，用 RPC 或 NetworkVariable 同步畫面。
7. 最後再補 Lobby、Relay 房間碼、斷線處理。
