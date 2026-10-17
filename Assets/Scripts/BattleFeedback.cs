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
    private static BattleFeedback instance;
    private AudioSource output;
    private bool hasHealth;
    private int whiteHealth, blackHealth;

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
