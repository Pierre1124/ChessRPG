using UnityEngine;

public enum CommonBattleSound { CardUse, Damage, Heal, Draw, Recycle }

/// <summary>集中共用音效及狀態特效接口；不修改遊戲規則，空素材會自動略過。</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class BattleFeedback : MonoBehaviour
{
    [Header("共同音效")]
    public AudioClip cardUseSound;
    public AudioClip damageSound;
    public AudioClip healSound;
    public AudioClip drawSound;
    public AudioClip recycleSound;
    [Range(0f, 1f)] public float volume = 1f;
    [Header("類型呈現：圖示、數字、特效與音效")]
    public BattlePresentationStyle[] presentationStyles = new BattlePresentationStyle[0];
    public Transform typeEffectRoot;
    private static BattleFeedback instance;
    private AudioSource output;
    private bool hasHealth;
    private int whiteHealth, blackHealth;
    private readonly System.Collections.Generic.List<GameObject> typeEffects = new System.Collections.Generic.List<GameObject>();

    /// <summary>取得場景中指定類型的外觀，未設定時沿用原飄字。</summary>
    public static BattlePresentationStyle GetStyle(BattlePresentationType type)
    {
        if (instance == null) return null;
        foreach (var style in instance.presentationStyles)
            if (style != null && style.type == type) return style;
        return null;
    }

    /// <summary>於演出時播放類型回饋；不在數值試算或網路同步時重複觸發。</summary>
    public static void PlayType(BattlePresentationType type, Vector3 position)
    {
        var style = GetStyle(type);
        if (style == null) return;
        PlayClip(style.sound, style.soundVolume);
        if (style.effectPrefab == null) return;
        var effect = Instantiate(style.effectPrefab, position + style.effectOffset, Quaternion.identity);
        effect.transform.SetParent(instance.typeEffectRoot != null ? instance.typeEffectRoot : instance.transform, true);
        effect.SetActive(true);
        instance.typeEffects.Add(effect);
        instance.StartCoroutine(instance.RemoveTypeEffect(effect, style.effectLifetime));
    }

    /// <summary>使用真實時間清除一次性特效，暫停不延長其壽命。</summary>
    private System.Collections.IEnumerator RemoveTypeEffect(GameObject effect, float lifetime)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(.05f, lifetime));
        if (effect != null) Destroy(effect);
        typeEffects.Remove(effect);
    }

    /// <summary>取消演出或切換普通棋局時立即移除本接口建立的特效。</summary>
    public static void StopTypeEffects()
    {
        if (instance == null) return;
        foreach (var effect in instance.typeEffects)
            if (effect != null) { effect.SetActive(false); Destroy(effect); }
        instance.typeEffects.Clear();
    }

    /// <summary>播放器停用時清理特效，避免協程停止後殘留。</summary>
    private void OnDisable() { StopTypeEffects(); }

    /// <summary>登錄場景音效播放器，不建立額外的常駐物件。</summary>
    private void Awake()
    {
        instance = this; output = GetComponent<AudioSource>();
        output.playOnAwake = false; output.spatialBlend = 0f;
        RefreshAudioSettings();
    }

    /// <summary>離開場景時解除共用引用。</summary>
    private void OnDestroy() { if (instance == this) instance = null; }

    /// <summary>依事件選擇共同音效，失敗操作不應呼叫此接口。</summary>
    public static void Play(CommonBattleSound kind)
    {
        if (instance == null) return;
        AudioClip clip = kind == CommonBattleSound.CardUse ? instance.cardUseSound :
            kind == CommonBattleSound.Damage ? instance.damageSound :
            kind == CommonBattleSound.Heal ? instance.healSound :
            kind == CommonBattleSound.Draw ? instance.drawSound : instance.recycleSound;
        PlayClip(clip, 1f);
    }

    /// <summary>每次播放讀取目前音量偏好，與設定頁的靜音及音量一致。</summary>
    public static void PlayClip(AudioClip clip, float gain)
    {
        if (instance == null || clip == null) return;
        RefreshAudioSettings();
        if (!instance.output.mute) instance.output.PlayOneShot(clip, instance.volume * Mathf.Clamp01(gain));
    }

    /// <summary>同步設定頁偏好，也讓已播放中的音效立即套用靜音及音量。</summary>
    public static void RefreshAudioSettings()
    {
        if (instance == null || instance.output == null) return;
        float level = PlayerPrefs.GetFloat("SoundVolume", 0.5f);
        instance.output.volume = float.IsNaN(level) ? 0.5f : Mathf.Clamp01(level);
        instance.output.mute = PlayerPrefs.GetInt("SoundEnabled", 1) == 0;
    }

    /// <summary>只對實際生命變化播放聲音；首次同步及相同數值刷新保持安靜。</summary>
    public static void ObserveHealth(int white, int black)
    {
        if (instance == null) return;
        if (instance.hasHealth)
        {
            if (white < instance.whiteHealth || black < instance.blackHealth) Play(CommonBattleSound.Damage);
            if (white > instance.whiteHealth || black > instance.blackHealth) Play(CommonBattleSound.Heal);
        }
        instance.whiteHealth = white; instance.blackHealth = black; instance.hasHealth = true;
    }

    /// <summary>狀態套用後建立一份待機 Prefab，生命週期由狀態及棋子共同管理。</summary>
    public static void StartStatusIdle(StatusRuntime status)
    {
        if (status?.owner == null || status.definition?.idleEffectPrefab == null || status.idleEffectInstance != null) return;
        status.idleEffectInstance = Instantiate(status.definition.idleEffectPrefab, status.owner.transform, false);
        status.idleEffectInstance.transform.localPosition = status.definition.effectOffset;
        status.idleEffectInstance.SetActive(true);
    }

    /// <summary>狀態解除、到期、換裝或轉成普通西洋棋時清除待機特效。</summary>
    public static void StopStatusIdle(StatusRuntime status)
    {
        if (status?.idleEffectInstance == null) return;
        status.idleEffectInstance.SetActive(false);
        Destroy(status.idleEffectInstance); status.idleEffectInstance = null;
    }

    /// <summary>播放單次狀態觸發特效及聲音，持續時間可在各狀態中設定。</summary>
    public static void PlayStatusTrigger(StatusRuntime status)
    {
        if (status?.definition == null || status.owner == null) return;
        var data = status.definition;
        if (data.triggerEffectPrefab != null)
        {
            var effect = Instantiate(data.triggerEffectPrefab, status.owner.transform, false);
            effect.transform.localPosition = data.effectOffset; effect.SetActive(true);
            Destroy(effect, Mathf.Max(0.05f, data.triggerEffectLifetime));
        }
        PlayClip(data.triggerSound, data.triggerSoundVolume);
    }
}
