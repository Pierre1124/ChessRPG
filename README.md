### 3D_Chess

Simple 3D Chess game built with Unity Engine. Includes realistic piece movements, turn-based logic, and a minimalistic interface.
This project is a fully functional, basic implementation of standard chess in 3D using the Unity game engine. It was developed as a personal project and learning experience, with all core rules implemented from scratch without relying on external implementations.
The game is currently designed for **local two-player gameplay only**, with no AI opponent or online multiplayer functionality. It is a minimal implementation focused on correctness and rule coverage rather than advanced features.

---
### Download
You can download the latest stable build here:

➡[Download 3D_Chess v1.0 (Windows .zip)](https://github.com/mzabron/3D_Chess/releases/download/v1.0/3D_Chess.zip)

How to Play:
Download and unpack the archive.
Run '3D_Chess.exe' directly from the extracted folder.
Do not move the '.exe' file out of its folder, as it relies on accompanying data files.

---

### Rule Support
The game includes support for all standard chess rules, including:

- **Pawn promotion** (to queen, rook, bishop, or knight)
- **En passant** captures
- **Castling** (both kingside and queenside, with all conditions enforced)
- **Check, checkmate**, and **stalemate**
- **Draw detection** in recognized endgame conditions (e.g., King vs. King, King + Bishop vs. King, King + Knight vs. King)

---

### Implementation Notes

All game logic, including move validation, rule enforcement, and game state management, was implemented manually. The goal was to develop a working rule-based chess system from the ground. While the logic is functionally correct, it is written in a basic and straightforward manner and may not reflect best practices in performance or software design.
This project is intended as a foundation for future expansion (e.g., AI, UI improvements, online multiplayer).

---
### Development Notes

If you're opening the project in Unity, make sure to manually load the main scene:
'Assets/Scenes/ChessScene.unity'

---

## 卡牌系統開發指南

目前卡牌系統不使用 `ScriptableObject`。卡片資料與技能規則分成兩個入口：

- `Assets/Scripts/ChessCard.cs`：卡號、名稱、目標棋種、說明、圖片資源與卡池。
- `Assets/Scripts/CardSkill.cs`：狀態、通用數值效果、特殊走法、動畫時機與特殊規則。
- `CardGameUI/ChessCard`：在 Inspector 掛卡面、技能／狀態圖、特效 Prefab 與音效。

`CardDefinition`、`StatusDefinition`、`CardEffectData` 等類別雖然仍存在，但只是定義在
`ChessCard.cs` 裡的一般 C# 資料，不是獨立資產。

### 目前不需要 subclass 或 override

`CardSkill` 是單一 `sealed class`，所有卡片共用 `CardSkill.Shared`。

新增技能時不要建立：

```csharp
public class J13Skill : CardSkill // 不要這樣做
```

也不需要 `override`。特殊技能直接在 `CardSkill.cs` 對應函數裡加入：

```csharp
case "J13":
    // J13 的特殊規則
    break;
```

### 新增卡片完整流程

以下以 `J13` 為例。

#### 1. 宣告 Inspector 圖片

在 `ChessCard` 類別的圖片欄位區新增：

```csharp
[Header("J13 Card Name")]
[SerializeField] public Sprite j13CardImage;
[SerializeField] public Sprite j13SkillImage;
```

若技能需要特效或音效，也在這裡宣告：

```csharp
[SerializeField] public GameObject j13EffectPrefab;
[SerializeField] public AudioClip j13Sound;
```

回到 `ChessScene`，選取 `CardGameUI`，在 `ChessCard` 元件掛上引用。

#### 2. 建立卡片資料

在 `ChessCard.cs` 新增 `BuildJ13()`：

```csharp
private CardDefinition BuildJ13()
{
    return NewCard(
        "J13",
        "卡片名稱",
        CardTargetType.Pawn,
        "無",
        "卡片效果說明。",
        j13CardImage,
        j13SkillImage,
        "轉職",
        "強化"
    );
}
```

`NewCard` 參數依序是：

1. 唯一卡號。
2. 顯示名稱。
3. 可裝備棋種。
4. 條件／代價文字。
5. 卡片描述。
6. 卡面圖片。
7. 技能／狀態圖片。
8. 後續參數全部是標籤。

可裝備棋種使用 `CardTargetType`：

```csharp
CardTargetType.Pawn
CardTargetType.Bishop
CardTargetType.King
CardTargetType.Knight
CardTargetType.Queen
CardTargetType.Rook
CardTargetType.Pawn | CardTargetType.Knight
CardTargetType.All
```

#### 3. 在 CardSkill 設定技能

在 `CardSkill.ConfigureCard()` 加入卡號：

```csharp
case "J13": ConfigureJ13(card); break;
```

狀態、效果、特殊走法與動畫全部在 `CardSkill.cs` 建立。不要把這些規則寫回
`ChessCard.BuildJ13()`。

### 事件卡

事件卡使用 `NewEventCard()` 建立，拖到合法目標後會立即呼叫
`CardSkill.ResolveEvent()`，結算成功才從手牌消耗。事件卡不會呼叫
`Piece.ApplyCard()`，因此不會取代棋子原本的轉職卡。

```csharp
private CardDefinition BuildE13()
{
    return NewEventCard(
        "E13",
        "事件名稱",
        CardTargetType.All,
        "無",
        "指定一個棋子並立即結算。",
        e13CardImage,
        e13SkillImage,
        "事件"
    );
}
```

在 `CardSkill.ResolveEvent()` 的 `switch` 增加卡號與效果。`CardTargetType.All`
代表所有棋種；目前事件卡可以指定白方或黑方棋子。

需要在棋子移動後移除的狀態，可設定：

```csharp
status.removeOnTrigger = true;
status.removeTrigger = CardEffectTrigger.AfterOwnerMoves;
```

棋盤事件可呼叫 `ApplyEventStatusToBoard()`，它會對目前棋盤上的白方與黑方
所有棋子套用狀態。同一卡再次使用時會刷新該狀態，不會疊加同一個卡號。

有限回合狀態若在持有者目前回合套用，該次回合結束不會立刻扣除持續時間；
之後每次持有者完成回合才減少一回合。

### 造成傷害修正順序

傷害分成以下規則：

- `ATK` 只在主動吃子時加入，並套用來源的 `ModifyDamageDealt`。
- `Value` 只在棋子被吃時加入，並套用目標的 `ModifyDamageTaken`。
- 魔法師等技能使用技能基礎傷害，不加入 ATK 或 Value。
- 火球等事件使用事件基礎傷害，不加入 ATK 或 Value。
- 非吃子傷害仍會套用來源的 `ModifyDamageDealt` 與目標的 `ModifyDamageTaken`。

結算順序固定為：

1. 輸出方的 `Add`／`Multiply`。
2. 目標棋子的承傷效果。
3. 陣營與玩家減傷。
4. 輸出方的 `Set`。

因此 E08 休戰的 `Set 0` 永遠最後執行，其他增傷或易傷無法把傷害重新加回。
休戰是全域規則，即使火球沒有來源棋子，最終傷害仍會歸零。

### 傷害分類

每筆傷害會建立 `DamageContext`，其中 `tags` 使用可複選的 `DamageTag`。
屬性與來源可以同時存在，例如火球是 `Fire | Event`，魔法師是
`Fire | Skill`。

目前分類：

- 一般吃子：`Physical | Capture`
- J04 電網破壞：`Electric | Skill`
- J07 魔法師：`Fire | Skill`
- J10 聖女貞德被吃反擊：`LastWill | Skill`
- E04 火球：`Fire | Event`
- E05 詛咒：`Curse | Status | Event`
- E09 毒蛇：`Poison | Status | Event`
- E10 回春的立即傷害：`Event`
- E03 強奪的 HP 支付：`Cost | Event`
- E11 血契反噬：`Cost | Status | Event`

卡牌的預設分類寫在 `CardSkill.ConfigureJXX/ConfigureEXX()`：

```csharp
card.damageTags = DamageTag.Fire | DamageTag.Skill;
```

會造成傷害的個別效果也要設定分類。`AddEffect()` 最後一個參數可直接傳入：

```csharp
AddEffect(
    status,
    CardEffectTrigger.TurnStarted,
    CardEffectType.DamagePlayer,
    CardEffectTarget.RandomEnemyPiece,
    1,
    DamageTag.Fire | DamageTag.Skill
);
```

特殊傷害呼叫可直接指定分類：

```csharp
logic.DealFixedDamageToPiece(
    source,
    target,
    1,
    CardEffectTrigger.OnOwnerDestroyed,
    DamageTag.LastWill | DamageTag.Skill
);
```

傷害修正需要判斷分類時，可從計算序列讀取
`sequence.damageContext.HasTag(DamageTag.Fire)`。HP 代價使用
`PayHealthCost()`，會建立帶有 `Cost` 標籤的 `DamageContext`，但仍直接支付，
不套用一般增傷或減傷。

#### 4. 加入卡池

目前沒有獨立卡池資產。`ChessCard.BuildCards()` 回傳的清單就是完整卡池。

```csharp
private List<CardDefinition> BuildCards()
{
    return new List<CardDefinition>
    {
        BuildJ01(),
        // ...
        BuildJ12(),
        BuildJ13()
    };
}
```

`CardHandManager.ResetHands()` 會把這份清單分別複製給黑白雙方並洗牌。

- 要讓卡片可以抽到：加入 `BuildCards()`。
- 暫時停用卡片：從 `BuildCards()` 移除，但可保留 `BuildJXX()`。
- 目前黑白雙方使用相同卡池。

### 通用數值效果

單純加減數值優先寫在 `StatusDefinition.effects`，不要寫特殊技能分支。

例如「攻擊力 +2」：

```csharp
private static void ConfigureJ13(CardDefinition card)
{
    StatusDefinition status = AddStatus(card, StatusKind.Buff);
    AddEffect(
        status,
        CardEffectTrigger.OnApplied,
        CardEffectType.ModifyAttack,
        CardEffectTarget.OwnerPiece,
        2
    );
}
```

`CardValueOperation` 預設是 `Add`。需要指定或倍率時：

```csharp
CardEffectData effect = new CardEffectData(
    CardEffectTrigger.OnApplied,
    CardEffectType.ModifyAttack,
    CardEffectTarget.OwnerPiece,
    2
);

effect.operation = CardValueOperation.Set;
// effect.operation = CardValueOperation.Multiply;
```

### Effect Trigger

`CardEffectTrigger` 決定效果何時觸發：

| Trigger | 時機 |
|---|---|
| `OnApplied` | 卡片裝備後的常駐數值 |
| `TurnStarted` | 持有者陣營回合開始 |
| `BeforeOwnerMoves` | 預留；目前沒有通用效果入口 |
| `BeforeOwnerCaptured` | 持有者即將被吃 |
| `AfterOwnerMoves` | 持有者移動後 |
| `AfterOwnerCaptures` | `CardSkill.OnOwnerCaptures` 已接上；通用效果資料尚未接上 |
| `OnOwnerDestroyed` | `CardSkill.OnOwnerCaptured` 與場地卡流程已接上；通用效果資料尚未接上 |
| `BeforeOwnerTakesDamage` | 持有者受到任何來源傷害前 |

### Effect Type

常用的 `CardEffectType`：

| Type | 用途 |
|---|---|
| `ModifyAttack` | 修改單一棋子的攻擊力 |
| `ModifyFriendlyAttack` | 修改我方全體攻擊力 |
| `ModifyValue` | 修改單一棋子的 Value，只在被吃時使用 |
| `ModifyFriendlyValue` | 修改我方全體 Value，只在被吃時使用 |
| `ModifyDamageDealt` | 修改來源造成的所有傷害 |
| `ModifyDamageTaken` | 修改棋子受到的傷害 |
| `ModifyOwnerPlayerDamage` | 修改該陣營玩家受到的 HP 傷害 |
| `HealPlayer` | 恢復玩家 HP |
| `DamagePlayer` | 造成玩家／棋子傷害流程 |
| `PreventMovement` | 禁止移動 |

常用的 `CardEffectTarget`：

| Target | 對象 |
|---|---|
| `OwnerPiece` | 裝備卡片的棋子 |
| `OwnerPlayer` | 我方玩家 |
| `OpponentPlayer` | 對方玩家 |
| `AdjacentFriendlyPieces` | 鄰近友軍；需要特殊距離規則時仍應寫 `CardSkill` |
| `CapturingPiece` | 吃掉持有者的棋子 |
| `RandomEnemyPiece` | 隨機敵方棋子 |

### 充能狀態

魔法師、聖職者使用狀態充能。設定方式：

```csharp
StatusDefinition status = card.statusesToApply[0];
status.maxCharges = 1;
status.initialCharges = 1;
status.consumeChargeOnTrigger = true;
status.rechargeTrigger = CardEffectTrigger.AfterOwnerMoves;
status.rechargeAmount = 1;

status.effects.Add(new CardEffectData(
    CardEffectTrigger.TurnStarted,
    CardEffectType.DamagePlayer,
    CardEffectTarget.RandomEnemyPiece,
    1
));
```

- `maxCharges = 0`：不使用充能。
- `consumeChargeOnTrigger = true`：成功觸發後消耗一次。
- `rechargeTrigger`：何時恢復充能。
- `durationTurns = 0`：永久存在，直到換卡或棋子死亡。

### 特殊技能應寫在哪個函數

所有技能設定都寫進 `CardSkill.cs`。能用 `CardEffectData` 表達的規則放在
`ConfigureJXX()`；無法資料化的特殊判斷，再放進下列事件或修正函數。

| 函數 | 目前用途／觸發狀態 |
|---|---|
| `CanApply` | 打出卡片前的特殊條件，已接上 |
| `OnEquip` | 裝備瞬間，例如移除國王、自動裝備另一城堡，已接上 |
| `OnUnequip` | 換卡或移除卡片時，已接上 |
| `OnOwnerCaptured` | 持有者被吃，已接上 |
| `OnOwnerCaptures` | 持有者吃子，已接上 |
| `OnFriendlyPieceCaptured` | 同陣營棋子被吃，已接上 |
| `ModifyHealAmount` | 修改我方回血量，已接上 |
| `ModifyFriendlyAttack` | 修改我方攻擊加成，已接上 |
| `ModifyDamageTaken` | 特殊範圍減傷，例如聖騎士，已接上 |
| `GetMoveDefinition` | 特殊走法，例如長槍兵，已接上 |
| `OnTurnStarted` | 預留；目前請使用狀態的 `TurnStarted` |
| `OnOwnerMoved` | 預留；目前請使用狀態的 `AfterOwnerMoves` |
| `OnOwnerDealsDamage` | 預留，尚未直接呼叫 |
| `ModifyAttack` | 預留；目前請使用 `ModifyAttack` 狀態效果 |
| `ModifyOwnerPlayerDamage` | 預留；目前請使用同名狀態效果 |

例如增加特殊裝備條件：

```csharp
public bool CanApply(CardSkillContext context)
{
    switch (GetId(context))
    {
        case "J13":
            return context.owner.HasMoved == 0;

        // 保留原有 case
        default:
            return true;
    }
}
```

不要建立第二個 `CanApply`。應把新的 `case` 加進現有函數。

例如裝備後立即執行特殊行為：

```csharp
case "J13":
    context.logic.HealPlayer(context.owner.IsWhite, 3);
    break;
```

例如特殊範圍減傷：

```csharp
public int ModifyDamageTaken(CardSkillContext context, int currentValue)
{
    if (GetId(context) == "J13" && YourRangeCheck(context))
    {
        return Mathf.Max(0, currentValue - 1);
    }

    // 必須保留其他卡片既有邏輯
    return currentValue;
}
```

### 特殊走法

在該卡的 `ConfigureJXX()` 建立 `PieceDefinition` 並加入 `PieceMoveRule`：

```csharp
card.jobChangeDefinition = new PieceDefinition
{
    displayName = "Custom Pawn",
    moveRules = new List<PieceMoveRule>
    {
        new PieceMoveRule
        {
            label = "Move Forward 1",
            mode = PieceRuleMode.Single,
            direction = Vector2Int.up,
            distance = 1,
            dependsOnFacing = true,
            canMoveToEmpty = true,
            canCaptureEnemy = false,
            canJump = false
        }
    }
};
```

並在 `CardSkill.GetMoveDefinition()` 讓該卡回傳定義：

```csharp
if (GetId(context) == "J13")
{
    return context.sourceCard.jobChangeDefinition;
}
```

### 卡片動畫與音效

在卡片的 `animations` 加入：

```csharp
card.animations.Add(new CardAnimationData
{
    timing = CardAnimationTiming.OnDamageDealt,
    recipient = CardAnimationRecipient.Target,
    effectPrefab = card.skillEffectPrefab,
    effectPositionOffset = new Vector3(0f, 0.5f, 0f),
    effectRotationEuler = Vector3.zero,
    effectScale = Vector3.one,
    effectLifetime = 1f,
    attachEffectToReceiver = false,
    sound = card.skillSound,
    soundVolume = 1f
});
```

`CardAnimationTiming` 可使用：

- `OnCardApplied`
- `OnOwnerMoved`
- `OnTurnStarted`
- `OnEffectTriggered`
- `OnDamageDealt`
- `OnHealed`

### 新卡檢查清單

1. 卡號是否唯一。
2. 是否宣告卡面與技能圖片欄位。
3. 是否在 `CardGameUI/ChessCard` Inspector 掛圖。
4. 是否新增 `BuildJXX()`。
5. 是否在 `CardSkill.ConfigureCard()` 加入卡號與 `ConfigureJXX()`。
6. 是否加入 `BuildCards()` 卡池。
7. `ChessCard.BuildJXX()` 是否只保留基本資料與 Inspector 資源。
8. 單純數值是否優先使用 `StatusDefinition.effects`。
9. 特殊規則是否加到現有 `CardSkill` 函數，而不是新增 override。
10. 範圍技能的傷害判定與 `CardInfoUI` 狀態顯示範圍是否一致。
11. 進入 Play Mode 後確認 Console 沒有空白卡片、重複觸發或 Missing Reference。

---
### Visual Overview

<p float="left">
  <img src="Assets/images/s1.png" width="400"/>
  <img src="Assets/images/s2.png" width="400"/>
  <img src="Assets/images/s3.png" width="400"/>
  <img src="Assets/images/s4.png" width="400"/>
</p>
