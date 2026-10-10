using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardType { JobChange, Event, Field }

[Flags]
public enum DamageTag
{
    None = 0,

    Physical = 1 << 0,
    Fire = 1 << 1,
    Poison = 1 << 2,
    Curse = 1 << 3,
    Electric = 1 << 4,
    Cost = 1 << 5,
    True = 1 << 6,

    Capture = 1 << 8,
    Event = 1 << 9,
    Skill = 1 << 10,
    Status = 1 << 11,
    LastWill = 1 << 12,
    Field = 1 << 13
}

[Flags]
public enum CardTargetType
{
    None = 0,
    Pawn = 1 << 0,
    Bishop = 1 << 1,
    King = 1 << 2,
    Knight = 1 << 3,
    Queen = 1 << 4,
    Rook = 1 << 5,
    All = Pawn | Bishop | King | Knight | Queen | Rook
}

public enum CardEffectTrigger
{
    OnApplied,
    TurnStarted,
    BeforeOwnerMoves,
    BeforeOwnerCaptured,
    AfterOwnerMoves,
    AfterOwnerCaptures,
    OnOwnerDestroyed,
    BeforeOwnerTakesDamage,
    TurnEnded,
    AfterOwnerCastles
}

public enum CardEffectType
{
    ModifyOwnerPlayerDamage,
    ModifyDamageDealt,
    ModifyDamageTaken,
    HealPlayer,
    DamagePlayer,
    PreventMovement,
    IgnoreDeathEffect,
    ModifyAttack,
    ModifyFriendlyAttack,
    ModifyValue,
    ModifyFriendlyValue
}

public enum CardValueOperation { Add, Set, Multiply }

public enum CardEffectTarget
{
    OwnerPiece,
    OwnerPlayer,
    OpponentPlayer,
    AdjacentFriendlyPieces,
    NearbyPieces,
    CapturingPiece,
    RandomEnemyPiece
}

public enum CardConditionType
{
    Always,
    OwnerHasNotMoved,
    OwnerHasMoved,
    OwnerPlayerHealthFull,
    OwnerHasNoCard
}

public enum CardConditionTiming { OnPlay, WhileActive }
public enum StatusKind { Buff, Debuff, Aura }

public enum CardAnimationTiming
{
    OnCardApplied,
    OnOwnerMoved,
    OnTurnStarted,
    OnEffectTriggered,
    OnDamageDealt,
    OnHealed,
    OnTurnEnded,
    OnOwnerCastled,
    OnDamageModifier
}

public enum CardAnimationRecipient { Source, Target, Both }
public enum PieceRuleMode { Single, Ray }

[Serializable]
public class CardConditionData
{
    public CardConditionTiming timing = CardConditionTiming.OnPlay;
    public CardConditionType condition = CardConditionType.Always;
    public bool invert;
    public int value;
}

[Serializable]
public class CardEffectData
{
    public CardEffectTrigger trigger;
    public CardEffectType effectType;
    public CardEffectTarget target = CardEffectTarget.OwnerPiece;
    public CardValueOperation operation = CardValueOperation.Add;
    public DamageTag damageTags;
    public int value;
    public int duration;
    [Min(0)] public int range;

    /// <summary>
    /// 建立卡牌效果資料，供序列化或以指定觸發、目標與數值初始化。
    /// </summary>
    public CardEffectData() { }

    /// <summary>
    /// 建立卡牌效果資料，供序列化或以指定觸發、目標與數值初始化。
    /// </summary>
    public CardEffectData(
        CardEffectTrigger trigger,
        CardEffectType effectType,
        CardEffectTarget target,
        int value
    )
    {
        this.trigger = trigger;
        this.effectType = effectType;
        this.target = target;
        this.value = value;
    }
}

[Serializable]
public class StatusDefinition
{
    public string statusName;
    public string sourceCardId;
    public StatusKind kind;
    public Sprite icon;
    [Min(0)] public int maxCharges;
    [Min(0)] public int initialCharges;
    public bool consumeChargeOnTrigger;
    public CardEffectTrigger rechargeTrigger = CardEffectTrigger.AfterOwnerMoves;
    [Min(0)] public int rechargeAmount;
    [Min(0)] public int durationTurns;
    public bool removeOnTrigger;
    public CardEffectTrigger removeTrigger = CardEffectTrigger.AfterOwnerMoves;
    public List<CardEffectData> effects = new List<CardEffectData>();

    /// <summary>
    /// 判斷狀態定義是否包含指定觸發時機的效果。
    /// </summary>
    public bool HasTrigger(CardEffectTrigger trigger)
    {
        return effects.Exists(effect => effect != null && effect.trigger == trigger);
    }
}

[Serializable]
public class CardAnimationData
{
    public CardAnimationTiming timing;
    public CardAnimationRecipient recipient;
    public string animatorTrigger;
    public string animatorState;
    public GameObject effectPrefab;
    public Vector3 effectPositionOffset;
    public Vector3 effectRotationEuler;
    public Vector3 effectScale = Vector3.one;
    [Min(0f)] public float effectLifetime = 2f;
    public bool attachEffectToReceiver;
    public AudioClip sound;
    [Range(0f, 1f)] public float soundVolume = 1f;
}

[Serializable]
public class PieceMoveRule
{
    public string label;
    public PieceRuleMode mode = PieceRuleMode.Single;
    public Vector2Int direction = Vector2Int.up;
    [Min(1)] public int distance = 1;
    public bool dependsOnFacing = true;
    public bool canMoveToEmpty = true;
    public bool canCaptureEnemy = true;
    public bool canJump;
}

[Serializable]
public class PieceDefinition
{
    public string displayName;
    public List<PieceMoveRule> moveRules = new List<PieceMoveRule>();

    /// <summary>Unity 會將空的內嵌定義序列化成物件；只有實際走法才取代棋子原走法。</summary>
    public bool HasMoveRules => moveRules != null && moveRules.Exists(rule => rule != null);
}

[Serializable]
public class CardDefinition
{
    public string id;
    [Tooltip("特殊規則的識別碼；一般數值卡使用資料效果即可。")]
    public string specialRuleId;
    public bool useDataActions;
    public List<CardPlayAction> playActions = new List<CardPlayAction>();
    [Min(0)] public int healthCost;
    [Min(0)] public int friendlyDamageBonus;
    public bool damageBonusUsesCaptureStacks;
    public bool blocksHealing;
    public bool requiresNoBoardCaptures;
    [Min(0)] public int lastWillDamage;
    public DamageTag lastWillDamageTags;
    public int minimumTargetHealth = -1;
    public int maximumTargetHealth = -1;
    [TextArea] public string ruleNotes;
    public CardType cardType;
    public string cardName;
    public CardTargetType targetTypes = CardTargetType.All;
    public string conditionCost;
    public DamageTag damageTags;
    public DamageTag[] randomDamageTypes = new DamageTag[0];
    public bool pawnCanPromote;
    public Sprite cardImage;
    public Sprite skillImage;
    public GameObject skillEffectPrefab;
    public AudioClip skillSound;
    [TextArea(3, 5)] public string description;
    public List<string> tags = new List<string>();
    public List<CardConditionData> conditions = new List<CardConditionData>();
    public List<CardEffectData> effects = new List<CardEffectData>();
    public List<StatusDefinition> statusesToApply = new List<StatusDefinition>();
    public List<CardAnimationData> animations = new List<CardAnimationData>();
    public PieceDefinition jobChangeDefinition;

    /// <summary>每次觸發只選一種屬性，保留事件／技能等來源標籤。</summary>
    public DamageTag ResolveDamageTags(DamageTag original)
    {
        if (randomDamageTypes == null || randomDamageTypes.Length == 0) return original;
        const DamageTag elements = DamageTag.Physical | DamageTag.Fire | DamageTag.Poison | DamageTag.Curse | DamageTag.Electric;
        return (original & ~elements) | randomDamageTypes[UnityEngine.Random.Range(0, randomDamageTypes.Length)];
    }

    public CardSkill skill { get { return CardSkill.Shared; } }
    public CardDefinition Api { get { return this; } }

    /// <summary>
    /// 檢查卡牌基本目標限制與特殊技能條件。
    /// </summary>
    public bool CanApplyTo(Piece target)
    {
        return CanApplyTo(target, UnityEngine.Object.FindFirstObjectByType<LogicManager>());
    }

    /// <summary>
    /// 檢查卡牌基本目標限制與特殊技能條件。
    /// </summary>
    public bool CanApplyTo(Piece target, LogicManager logicManager)
    {
        if (target == null || !MatchesTarget(target)) return false;
        if (!CardBattleSystem.ConditionsPass(
            this, target, logicManager, CardConditionTiming.OnPlay)) return false;
        return skill.CanApply(new CardSkillContext(logicManager, target, this));
    }

    /// <summary>
    /// 檢查卡牌類型、棋子種類與目標分類的基本限制。
    /// </summary>
    public bool BaseCanApplyTo(Piece target, LogicManager logicManager)
    {
        return target != null && MatchesTarget(target) &&
            CardBattleSystem.ConditionsPass(
                this, target, logicManager, CardConditionTiming.OnPlay);
    }

    /// <summary>
    /// 判斷卡牌是否允許指定棋子轉職。
    /// </summary>
    public bool CanPromote(Piece target)
    {
        return pawnCanPromote && target is Pawn && CanApplyTo(target);
    }

    /// <summary>
    /// 判斷棋子是否符合卡牌的目標種類。
    /// </summary>
    private bool MatchesTarget(Piece target)
    {
        CardTargetType type = CardTargetType.None;
        if (target is Pawn) type = CardTargetType.Pawn;
        else if (target is Bishop) type = CardTargetType.Bishop;
        else if (target is King) type = CardTargetType.King;
        else if (target is Knight) type = CardTargetType.Knight;
        else if (target is Queen) type = CardTargetType.Queen;
        else if (target is Rook) type = CardTargetType.Rook;
        return type != CardTargetType.None && (targetTypes & type) != 0;
    }
}

public class DamageContext
{
    public int baseDamage;
    public int resolvedDamage;
    public Piece source;
    public Piece target;
    public CardDefinition sourceCard;
    public CardEffectData sourceEffect;
    public StatusRuntime sourceStatus;
    public CardEffectTrigger trigger;
    public DamageTag tags;
    public Sprite visualIcon;

    /// <summary>
    /// 判斷目前傷害情境是否含有指定標籤。
    /// </summary>
    public bool HasTag(DamageTag tag)
    {
        return (tags & tag) == tag;
    }
}

[Serializable]
public class CardRuntimeState
{
    public CardDefinition definition;
    public int turnsActive;
    public bool hasTriggered;
    public int skillCounterA;
    public int skillCounterB;
    public bool activeSkillDisabled;

    /// <summary>
    /// 建立指定卡牌的執行期狀態。
    /// </summary>
    public CardRuntimeState(CardDefinition card) { definition = card; }
}

[Serializable]
public class StatusRuntime
{
    public StatusDefinition definition;
    public Piece owner;
    public Piece source;
    public int remainingTurns;
    public int charges;
    public bool fromCard;
    public bool skipNextDurationAdvance;
    public bool hasSourcePlayer;
    public bool sourcePlayerIsWhite;

    /// <summary>
    /// 依狀態定義初始化擁有者、來源、持續回合與使用次數。
    /// </summary>
    public StatusRuntime(StatusDefinition definition, Piece owner, Piece source, bool fromCard)
    {
        this.definition = definition;
        this.owner = owner;
        this.source = source;
        this.fromCard = fromCard;
        remainingTurns = definition != null ? definition.durationTurns : 0;
        charges = definition != null && definition.maxCharges > 0
            ? Mathf.Clamp(definition.initialCharges, 0, definition.maxCharges)
            : 0;
    }

    public bool UsesCharges
    {
        get { return definition != null && definition.maxCharges > 0; }
    }

    public bool HasCharges { get { return !UsesCharges || charges > 0; } }

    /// <summary>
    /// 依狀態定義補充可用次數。
    /// </summary>
    public void Recharge(CardEffectTrigger trigger)
    {
        if (definition == null || !UsesCharges ||
            definition.rechargeTrigger != trigger || definition.rechargeAmount <= 0) return;
        charges = Mathf.Min(definition.maxCharges, charges + definition.rechargeAmount);
    }

    /// <summary>
    /// 依狀態的次數限制消耗一次效果使用額度。
    /// </summary>
    public bool ConsumeIfNeeded()
    {
        if (definition == null || !definition.consumeChargeOnTrigger || !UsesCharges)
            return true;
        if (charges <= 0) return false;
        charges--;
        return true;
    }
}

public class CardAnimationContext
{
    public CardAnimationTiming timing;
    public CardDefinition card;
    public CardEffectData effect;
    public Piece source;
    public Piece target;
    public Piece receiver;
    public CardAnimationRecipient recipient;
    public int amount;
}

public interface ICardAnimationReceiver
{
    /// <summary>
    /// 接收卡牌動畫情境並依事件時機與接收對象處理演出。
    /// </summary>
    void PlayCardAnimation(CardAnimationContext context);
}

/// <summary>
/// Scene-owned card catalogue. Images are assigned in Inspector; no card assets are used.
/// </summary>
public class ChessCard : MonoBehaviour
{
    [Header("J01 Long Spearman")]
    [SerializeField] public Sprite j01CardImage;
    [SerializeField] public Sprite j01SkillImage;

    [Header("J02 Shield Bearer")]
    [SerializeField] public Sprite j02CardImage;
    [SerializeField] public Sprite j02SkillImage;

    [Header("J03 Moat")]
    [SerializeField] public Sprite j03CardImage;
    [SerializeField] public Sprite j03SkillImage;

    [Header("J04 Power Grid")]
    [SerializeField] public Sprite j04CardImage;
    [SerializeField] public Sprite j04SkillImage;

    [Header("J05 Commando Captain")]
    [SerializeField] public Sprite j05CardImage;
    [SerializeField] public Sprite j05SkillImage;
    [SerializeField] public GameObject j05EffectPrefab;
    [SerializeField] public AudioClip j05Sound;

    [Header("J06 Paladin")]
    [SerializeField] public Sprite j06CardImage;
    [SerializeField] public Sprite j06SkillImage;

    [Header("J07 Mage")]
    [SerializeField] public Sprite j07CardImage;
    [SerializeField] public Sprite j07SkillImage;
    [SerializeField] public GameObject j07EffectPrefab;
    [SerializeField] public AudioClip j07Sound;

    [Header("J08 Cleric")]
    [SerializeField] public Sprite j08CardImage;
    [SerializeField] public Sprite j08SkillImage;

    [Header("J09 Bloody Mary")]
    [SerializeField] public Sprite j09CardImage;
    [SerializeField] public Sprite j09SkillImage;

    [Header("J10 Joan of Arc")]
    [SerializeField] public Sprite j10CardImage;
    [SerializeField] public Sprite j10SkillImage;

    [Header("J11 Wise King")]
    [SerializeField] public Sprite j11CardImage;
    [SerializeField] public Sprite j11SkillImage;

    [Header("J12 Tyrant")]
    [SerializeField] public Sprite j12CardImage;
    [SerializeField] public Sprite j12SkillImage;

    [Header("E01 Dismissal")]
    [SerializeField] public Sprite e01CardImage;
    [SerializeField] public Sprite e01SkillImage;

    [Header("E02 Blessing")]
    [SerializeField] public Sprite e02CardImage;
    [SerializeField] public Sprite e02SkillImage;

    [Header("E03 Seize")]
    [SerializeField] public Sprite e03CardImage;
    [SerializeField] public Sprite e03SkillImage;

    [Header("E04 Fireball")]
    [SerializeField] public Sprite e04CardImage;
    [SerializeField] public Sprite e04SkillImage;

    [Header("E05 Curse")]
    [SerializeField] public Sprite e05CardImage;
    [SerializeField] public Sprite e05SkillImage;

    [Header("E06 Weakness")]
    [SerializeField] public Sprite e06CardImage;
    [SerializeField] public Sprite e06SkillImage;

    [Header("E07 Berserker")]
    [SerializeField] public Sprite e07CardImage;
    [SerializeField] public Sprite e07SkillImage;

    [Header("E08 Truce")]
    [SerializeField] public Sprite e08CardImage;
    [SerializeField] public Sprite e08SkillImage;

    [Header("E09 Venomous Snake")]
    [SerializeField] public Sprite e09CardImage;
    [SerializeField] public Sprite e09SkillImage;

    [Header("E10 Rejuvenation")]
    [SerializeField] public Sprite e10CardImage;
    [SerializeField] public Sprite e10SkillImage;

    [Header("E11 Blood Pact")]
    [SerializeField] public Sprite e11CardImage;
    [SerializeField] public Sprite e11SkillImage;

    [Header("E12 Hold Position")]
    [SerializeField] public Sprite e12CardImage;
    [SerializeField] public Sprite e12SkillImage;

    [Header("F01 Volcano")]
    [SerializeField] public Sprite f01CardImage;
    [SerializeField] public Sprite f01SkillImage;

    [Header("F02 Lava Plateau")]
    [SerializeField] public Sprite f02CardImage;
    [SerializeField] public Sprite f02SkillImage;

    [Header("F03 Swamp")]
    [SerializeField] public Sprite f03CardImage;
    [SerializeField] public Sprite f03SkillImage;

    [Header("F04 Eternal Swamp")]
    [SerializeField] public Sprite f04CardImage;
    [SerializeField] public Sprite f04SkillImage;

    [Header("F05 Saint Seren Kingdom")]
    [SerializeField] public Sprite f05CardImage;
    [SerializeField] public Sprite f05SkillImage;

    [Header("F06 Empire of Light")]
    [SerializeField] public Sprite f06CardImage;
    [SerializeField] public Sprite f06SkillImage;

    [Header("F07 Maliet Ancient Castle")]
    [SerializeField] public Sprite f07CardImage;
    [SerializeField] public Sprite f07SkillImage;

    [Header("F08 Empire of Darkness")]
    [SerializeField] public Sprite f08CardImage;
    [SerializeField] public Sprite f08SkillImage;

    [Header("F09 Black Market")]
    [SerializeField] public Sprite f09CardImage;
    [SerializeField] public Sprite f09SkillImage;

    [Header("F10 Gilded Sin Domain")]
    [SerializeField] public Sprite f10CardImage;
    [SerializeField] public Sprite f10SkillImage;

    [Header("F11 Infernal Swamp")]
    [SerializeField] public Sprite f11CardImage;
    [SerializeField] public Sprite f11SkillImage;

    [Header("F12 Fated Endland")]
    [SerializeField] public Sprite f12CardImage;
    [SerializeField] public Sprite f12SkillImage;

    [Header("White Piece Images")]
    [SerializeField] public Sprite whiteKingImage;
    [SerializeField] public Sprite whiteQueenImage;
    [SerializeField] public Sprite whiteRookImage;
    [SerializeField] public Sprite whiteBishopImage;
    [SerializeField] public Sprite whiteKnightImage;
    [SerializeField] public Sprite whitePawnImage;

    [Header("Black Piece Images")]
    [SerializeField] public Sprite blackKingImage;
    [SerializeField] public Sprite blackQueenImage;
    [SerializeField] public Sprite blackRookImage;
    [SerializeField] public Sprite blackBishopImage;
    [SerializeField] public Sprite blackKnightImage;
    [SerializeField] public Sprite blackPawnImage;

    [Header("Fallback Images")]
    [SerializeField] public Sprite defaultStatusSprite;

    private List<CardDefinition> cards;

    public IReadOnlyList<CardDefinition> Cards
    {
        get
        {
            if (cards == null) cards = CardAssetLibrary.LoadDefinitions();
            return cards;
        }
    }

    /// <summary>
    /// 依唯一卡號取得卡牌定義，找不到時回傳空值。
    /// </summary>
    public CardDefinition GetCard(string id)
    {
        foreach (CardDefinition card in Cards)
            if (card.id == id) return card;
        return null;
    }

    public Sprite DefaultStatusSprite
    {
        get { return defaultStatusSprite; }
    }

    /// <summary>
    /// 依棋子種類與陣營取得顯示圖像。
    /// </summary>
    public Sprite GetPieceSprite(Piece piece)
    {
        if (piece == null) return null;

        if (piece.IsWhite)
        {
            if (piece is King) return whiteKingImage;
            if (piece is Queen) return whiteQueenImage;
            if (piece is Rook) return whiteRookImage;
            if (piece is Bishop) return whiteBishopImage;
            if (piece is Knight) return whiteKnightImage;
            if (piece is Pawn) return whitePawnImage;
        }
        else
        {
            if (piece is King) return blackKingImage;
            if (piece is Queen) return blackQueenImage;
            if (piece is Rook) return blackRookImage;
            if (piece is Bishop) return blackBishopImage;
            if (piece is Knight) return blackKnightImage;
            if (piece is Pawn) return blackPawnImage;
        }

        return null;
    }

    /// <summary>
    /// Inspector 資料變更時清除卡牌快取，讓下次存取重新建立定義。
    /// </summary>
    private void OnValidate() { cards = null; }

    /// <summary>製作工具修改素材後清除定義快取，下次讀取時重新建立。</summary>
    public void InvalidateCardCache() { cards = null; }

    /// <summary>
    /// 建立卡牌庫並依既有順序登錄轉職、事件與場地卡。
    /// </summary>
    #if UNITY_EDITOR
    public List<CardDefinition> BuildLegacyCardsForMigration()
    {
        return new List<CardDefinition>
        {
            BuildJ01(), BuildJ02(), BuildJ03(), BuildJ04(),
            BuildJ05(), BuildJ06(), BuildJ07(), BuildJ08(),
            BuildJ09(), BuildJ10(), BuildJ11(), BuildJ12(),
            BuildE01(), BuildE02(), BuildE03(), BuildE04(),
            BuildE05(), BuildE06(), BuildE07(), BuildE08(),
            BuildE09(), BuildE10(), BuildE11(), BuildE12(),
            BuildF01(), BuildF02(), BuildF03(), BuildF04(),
            BuildF05(), BuildF06(), BuildF07(), BuildF08(),
            BuildF09(), BuildF10(), BuildF11(), BuildF12()
        };
    }

    /// <summary>
    /// 建立轉職卡的基本資料與資源設定。
    /// </summary>
    private CardDefinition NewCard(
        string id,
        string name,
        CardTargetType target,
        string condition,
        string description,
        Sprite cardImage,
        Sprite skillImage,
        params string[] tags
    )
    {
        CardDefinition card = new CardDefinition
        {
            id = id,
            cardType = CardType.JobChange,
            cardName = name,
            targetTypes = target,
            conditionCost = condition,
            cardImage = cardImage,
            skillImage = skillImage,
            description = description,
            tags = new List<string>(tags)
        };
        CardSkill.Shared.ConfigureCard(card);
        return card;
    }

    /// <summary>
    /// 建立含技能圖像、特效與音效資源的卡牌定義。
    /// </summary>
    private CardDefinition NewCardWithSkillResources(
        string id,
        string name,
        CardTargetType target,
        string condition,
        string description,
        Sprite cardImage,
        Sprite skillImage,
        GameObject effectPrefab,
        AudioClip sound,
        params string[] tags
    )
    {
        CardDefinition card = new CardDefinition
        {
            id = id,
            cardType = CardType.JobChange,
            cardName = name,
            targetTypes = target,
            conditionCost = condition,
            cardImage = cardImage,
            skillImage = skillImage,
            skillEffectPrefab = effectPrefab,
            skillSound = sound,
            description = description,
            tags = new List<string>(tags)
        };
        CardSkill.Shared.ConfigureCard(card);
        return card;
    }

    /// <summary>
    /// 建立事件卡的基本資料與資源設定。
    /// </summary>
    private CardDefinition NewEventCard(
        string id,
        string name,
        CardTargetType target,
        string condition,
        string description,
        Sprite cardImage,
        Sprite skillImage,
        params string[] tags
    )
    {
        CardDefinition card = new CardDefinition
        {
            id = id,
            cardType = CardType.Event,
            cardName = name,
            targetTypes = target,
            conditionCost = condition,
            cardImage = cardImage,
            skillImage = skillImage,
            description = description,
            tags = new List<string>(tags)
        };
        CardSkill.Shared.ConfigureCard(card);
        return card;
    }

    /// <summary>
    /// 建立場地卡的基本資料與資源設定。
    /// </summary>
    private CardDefinition NewFieldCard(
        string id,
        string name,
        string condition,
        string description,
        Sprite cardImage,
        Sprite skillImage,
        params string[] tags
    )
    {
        CardDefinition card = new CardDefinition
        {
            id = id,
            cardType = CardType.Field,
            cardName = name,
            targetTypes = CardTargetType.None,
            conditionCost = condition,
            cardImage = cardImage,
            skillImage = skillImage,
            description = description,
            tags = new List<string>(tags)
        };
        CardSkill.Shared.ConfigureCard(card);
        return card;
    }

    /// <summary>
    /// 建立 J01 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ01()
    {
        CardDefinition card = NewCard(
            "J01", "長槍兵", CardTargetType.Pawn,
            "無",
            "吃子方式變為前方第二格，移動方式為前方一格。",
            j01CardImage, j01SkillImage,
            "轉職", "變幻"
        );
        card.pawnCanPromote = true;
        return card;
    }

    /// <summary>
    /// 建立 J02 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ02()
    {
        CardDefinition card = NewCard(
            "J02", "盾牌兵", CardTargetType.Pawn,
            "無",
            "價值-1",
            j02CardImage, j02SkillImage,
            "轉職", "強化"
        );
        card.pawnCanPromote = true;
        return card;
    }

    /// <summary>
    /// 建立 J03 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ03()
    {
        return NewCard(
            "J03", "護城河", CardTargetType.Rook,
            "兩個同方城堡同 X 或同 Y，且中間沒有棋子。",
            "兩個城堡之間形成護城河；護城河被破壞時我方恢復5點HP。",
            j03CardImage, j03SkillImage,
            "轉職", "變幻", "干擾", "遺言"
        );
    }

    /// <summary>
    /// 建立 J04 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ04()
    {
        return NewCard(
            "J04", "電網", CardTargetType.Rook,
            "兩個同方城堡同 X 或同 Y，且中間沒有棋子。",
            "兩個城堡之間形成電網；電網被破壞時我方受到5點傷害。",
            j04CardImage, j04SkillImage,
            "轉職", "變幻", "干擾", "遺言"
        );
    }

    /// <summary>
    /// 建立 J05 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ05()
    {
        return NewCardWithSkillResources(
            "J05", "突擊隊長", CardTargetType.Knight,
            "無",
            "攻擊力+2",
            j05CardImage, j05SkillImage, j05EffectPrefab, j05Sound,
            "轉職", "強化"
        );
    }

    /// <summary>
    /// 建立 J06 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ06()
    {
        return NewCard(
            "J06", "聖騎士", CardTargetType.Knight,
            "無",
            "聖騎士周圍一格的我方棋子受到傷害-1。",
            j06CardImage, j06SkillImage,
            "轉職", "強化"
        );
    }

    /// <summary>
    /// 建立 J07 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ07()
    {
        return NewCardWithSkillResources(
            "J07", "魔法師", CardTargetType.Bishop,
            "無",
            "回合開始時對隨機敵方棋子造成1點傷害；魔法師移動後重新充能。",
            j07CardImage, j07SkillImage, j07EffectPrefab, j07Sound,
            "轉職", "傷害"
        );
    }

    /// <summary>
    /// 建立 J08 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ08()
    {
        return NewCard(
            "J08", "聖職者", CardTargetType.Bishop,
            "無",
            "回合開始時我方恢復1點HP；聖職者移動後重新充能。",
            j08CardImage, j08SkillImage,
            "轉職", "恢復"
        );
    }

    /// <summary>
    /// 建立 J09 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ09()
    {
        return NewCard(
            "J09", "血腥瑪麗", CardTargetType.Queen,
            "我方HP全滿且沒有任何棋子被吃。",
            "移除我方國王；我方所有棋子攻擊力+2且無法恢復HP；血腥瑪麗被吃後遊戲結束。",
            j09CardImage, j09SkillImage,
            "轉職", "變幻", "強化"
        );
    }

    /// <summary>
    /// 建立 J10 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ10()
    {
        return NewCard(
            "J10", "聖女貞德", CardTargetType.Queen,
            "我方沒有任何棋子被吃。",
            "我方所有棋子攻擊力+1；貞德每次吃子再+1，我方每次被吃子則-1；貞德被吃時攻擊者受到1點傷害。",
            j10CardImage, j10SkillImage,
            "轉職", "強化", "遺言"
        );
    }

    /// <summary>
    /// 建立 J11 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ11()
    {
        return NewCard(
            "J11", "明君", CardTargetType.King,
            "無",
            "我方所有棋子攻擊力+1。",
            j11CardImage, j11SkillImage,
            "轉職", "強化"
        );
    }

    /// <summary>
    /// 建立 J12 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildJ12()
    {
        return NewCard(
            "J12", "昏君", CardTargetType.King,
            "無",
            "我方所有棋子價值-1。",
            j12CardImage, j12SkillImage,
            "轉職", "強化"
        );
    }

    /// <summary>
    /// 建立 E01 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE01()
    {
        return NewEventCard(
            "E01", "革職", CardTargetType.All,
            "無", "指定一個棋子，破壞其轉職卡。",
            e01CardImage, e01SkillImage,
            "事件", "干擾"
        );
    }

    /// <summary>
    /// 建立 E02 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE02()
    {
        return NewEventCard(
            "E02", "祝福", CardTargetType.King,
            "無", "指定一個國王，回復5點HP。",
            e02CardImage, e02SkillImage,
            "事件", "恢復"
        );
    }

    /// <summary>
    /// 建立 E03 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE03()
    {
        return NewEventCard(
            "E03", "強奪", CardTargetType.All,
            "HP-5",
            "指定一個棋子，奪走其轉職卡，該轉職卡可在下次自己的回合裝備。",
            e03CardImage, e03SkillImage,
            "事件", "干擾"
        );
    }

    /// <summary>
    /// 建立 E04 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE04()
    {
        return NewEventCard(
            "E04", "火球", CardTargetType.All,
            "無", "指定一個棋子，造成5點火屬性傷害。",
            e04CardImage, e04SkillImage,
            "事件", "傷害"
        );
    }

    /// <summary>
    /// 建立 E05 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE05()
    {
        return NewEventCard(
            "E05", "詛咒", CardTargetType.All,
            "無",
            "指定一個棋子，每次回合結束時受到1點傷害，持續3回合。該棋子被吃時，詛咒轉移給吃子者並重設持續時間。",
            e05CardImage, e05SkillImage,
            "事件", "傷害", "干擾", "遺言"
        );
    }

    /// <summary>
    /// 建立 E06 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE06()
    {
        return NewEventCard(
            "E06", "虛弱", CardTargetType.All,
            "無", "指定一個棋子，造成傷害-1，受到傷害+1，持續2回合。",
            e06CardImage, e06SkillImage,
            "事件", "干擾"
        );
    }

    /// <summary>
    /// 建立 E07 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE07()
    {
        return NewEventCard(
            "E07", "狂戰士", CardTargetType.All,
            "無", "棋盤上雙方所有棋子造成傷害+1，持續2回合。",
            e07CardImage, e07SkillImage,
            "事件", "強化"
        );
    }

    /// <summary>
    /// 建立 E08 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE08()
    {
        return NewEventCard(
            "E08", "休戰", CardTargetType.All,
            "無", "棋盤上雙方所有棋子造成傷害歸零，持續2回合。",
            e08CardImage, e08SkillImage,
            "事件", "變幻"
        );
    }

    /// <summary>
    /// 建立 E09 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE09()
    {
        return NewEventCard(
            "E09", "毒蛇", CardTargetType.All,
            "無",
            "指定一個棋子，使其中毒：每次回合結束時受到1點傷害，移動後再受到1點傷害，持續3回合。",
            e09CardImage, e09SkillImage,
            "事件", "傷害", "干擾"
        );
    }

    /// <summary>
    /// 建立 E10 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE10()
    {
        return NewEventCard(
            "E10", "回春", CardTargetType.King,
            "指定對象HP>=50",
            "指定一個國王，造成10點HP傷害，其所屬玩家每次回合結束時恢復3點HP，持續5回合。",
            e10CardImage, e10SkillImage,
            "事件", "傷害", "恢復"
        );
    }

    /// <summary>
    /// 建立 E11 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE11()
    {
        return NewEventCard(
            "E11", "血契", CardTargetType.King,
            "指定對象HP<=50",
            "指定一個國王，其所屬玩家恢復40點HP，之後每次回合結束時受到5點傷害，持續10回合。",
            e11CardImage, e11SkillImage,
            "事件", "傷害", "恢復"
        );
    }

    /// <summary>
    /// 建立 E12 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildE12()
    {
        return NewEventCard(
            "E12", "堅守陣地", CardTargetType.All,
            "無", "指定一個棋子，受到傷害-5，移動後移除效果。",
            e12CardImage, e12SkillImage,
            "事件", "強化"
        );
    }

    /// <summary>
    /// 建立 F01 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF01()
    {
        return NewFieldCard(
            "F01", "火山",
            "無", "火屬性傷害+1。",
            f01CardImage, f01SkillImage,
            "場地", "強化"
        );
    }

    /// <summary>
    /// 建立 F02 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF02()
    {
        return NewFieldCard(
            "F02", "熔岩高原",
            "火山+火山",
            "火屬性傷害+1；雙方回合結束時，對全體造成1點火屬性傷害。",
            f02CardImage, f02SkillImage,
            "場地", "強化", "傷害"
        );
    }

    /// <summary>
    /// 建立 F03 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF03()
    {
        return NewFieldCard(
            "F03", "沼澤",
            "無", "毒屬性傷害+1。",
            f03CardImage, f03SkillImage,
            "場地", "強化"
        );
    }

    /// <summary>
    /// 建立 F04 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF04()
    {
        return NewFieldCard(
            "F04", "永泳沼地",
            "沼澤+沼澤",
            "毒屬性傷害+5；全體價值-1。",
            f04CardImage, f04SkillImage,
            "場地", "強化"
        );
    }

    /// <summary>
    /// 建立 F05 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF05()
    {
        return NewFieldCard(
            "F05", "聖森恩王國",
            "無", "全體受到傷害-1。",
            f05CardImage, f05SkillImage,
            "場地", "強化"
        );
    }

    /// <summary>
    /// 建立 F06 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF06()
    {
        return NewFieldCard(
            "F06", "光之帝國",
            "聖森恩王國+聖森恩王國",
            "全體受到傷害-1；回復效果+5。",
            f06CardImage, f06SkillImage,
            "場地", "強化"
        );
    }

    /// <summary>
    /// 建立 F07 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF07()
    {
        return NewFieldCard(
            "F07", "瑪莉埃古堡",
            "無", "全體攻擊力+1。",
            f07CardImage, f07SkillImage,
            "場地", "強化"
        );
    }

    /// <summary>
    /// 建立 F08 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF08()
    {
        return NewFieldCard(
            "F08", "暗之帝國",
            "瑪莉埃古堡+瑪莉埃古堡",
            "全體攻擊力+5。",
            f08CardImage, f08SkillImage,
            "場地", "強化"
        );
    }

    /// <summary>
    /// 建立 F09 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF09()
    {
        return NewFieldCard(
            "F09", "黑市",
            "無", "回收卡片時，隨機我方1個棋子攻擊力+1，價值-1。",
            f09CardImage, f09SkillImage,
            "場地", "強化"
        );
    }

    /// <summary>
    /// 建立 F10 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF10()
    {
        return NewFieldCard(
            "F10", "罪金之域",
            "黑市+黑市",
            "抽卡時，隨機挑選場上1~6個棋子造成1~6點詛咒傷害。",
            f10CardImage, f10SkillImage,
            "場地", "傷害"
        );
    }

    /// <summary>
    /// 建立 F11 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF11()
    {
        return NewFieldCard(
            "F11", "煉獄沼原",
            "火山+沼澤",
            "火屬性傷害+1；毒屬性傷害+1；回合結束時，對敵方隨機1個棋子及其周圍造成1點火毒傷害。",
            f11CardImage, f11SkillImage,
            "場地", "強化", "傷害"
        );
    }

    /// <summary>
    /// 建立 F12 卡牌的名稱、說明與資源設定。
    /// </summary>
    private CardDefinition BuildF12()
    {
        return NewFieldCard(
            "F12", "宿命終結之地",
            "聖森恩王國+瑪莉埃古堡",
            "重啟棋局，移除傷害步驟以及卡池，進行完全正式的西洋棋。",
            f12CardImage, f12SkillImage,
            "場地", "變幻"
        );
    }
    #endif
}
