// --- PlayerStatusManager.cs 【VJTタイムベース・UIブロック・エラー完全解消版】 ---
using KanKikuchi.AudioManager;
using System.Collections;
using TMPro;
using UnityEngine;

public class PlayerStatusManager : MonoBehaviour
{
    [Header("Player Settings")]
    public int playerId = 1;
    public PlayerSkillData characterData;

    [Header("Resources")]
    public int life = 0;          // 対戦時の獲得ラウンド勝星数（0からスタート）
    public float currentHP = 100f; // 通常時のHP（インスペクター準拠で100）
    public float maxHP = 100f;     // 通常時の最大HP（インスペクター準拠で100）
    public int stockLives = 2;    // 通常時の残機

    [Header("Piece Settings")]
    public int lifePieces = 0;
    public int lifePiecesRequired = 3;

    [Header("Timers")]
    public float invincibleTimer = 0f;
    public float deathBombTimer = 0f;

    [Header("Statistics")]
    public int continueCount = 0;
    public TextMeshProUGUI countdownText;

    [Header("UI References")]
    public PlayerStatusUI lifeUI;
    public ExtendNotificationUI extendUI;

    [Header("Round Transition")]
    public CanvasGroup screenFader;

    [Header("Global References")]
    public PauseManager pauseManager;
    [Header("VJT Visual Effects")]
    public SpellBarrierEffect spellBarrier; // インスペクターからアタッチするバリアの参照
    private PlayerMove _playerMove;

    public bool IsInvincible => invincibleTimer > 0;
    public bool IsDeathBombWindow => deathBombTimer > 0;

    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI winText;
    public TextMeshProUGUI koText;
    public UnityEngine.UI.Slider hpBar;
    public UnityEngine.UI.Slider orangeBar;
    public UnityEngine.UI.Slider spellHpBar;
    public float lerpSpeed = 2.0f;

    // =========================================================================
    // 🌟 聖少女領域（VJT）独立上乗せライフ ＆ デバフ管理ステート
    // =========================================================================
    [Header("--- Spell Card (VJT) 上乗せライフシステム ---")]
    public bool isSpellCardActive = false;      // 聖少女領域（VJT）展開中フラグ
    public bool isOverheated = false;          // 術式焼き切れ（デバフ）中フラグ

    public float spellHP = 0f;
    public float spellMaxHP = 0f;

    [HideInInspector] public float preSpellHP;             // 発動直前の通常HPをロック固定して覚える用

    [Header("--- VJT Duration Settings (Seconds) ---")]
    public float minSpellDuration = 8.0f;       // ゲージ200%（最小）で発動した時の維持秒数
    public float maxSpellDuration = 15.0f;      // ゲージ300%（最大）で発動した時の維持秒数

    private float totalSpellDuration = 0f;      // 発動時に確定した今回の総維持秒数
    private float spellTimer = 0f;              // 残り時間をカウントするリアルタイムタイマー
    private float initialUltimateEnergy = 0f;   // 発動した瞬間のアルカナゲージ量を記憶する用

    public float overheatDuration = 5f;        // 術式焼き切れのデバフ持続時間（5秒）
    private float overheatTimer = 0f;

    private Vector3 originalColliderScale = Vector3.one;
    private Collider2D playerCollider;

    private float appearanceElapsed = 0f;
    private float animatedSpellHP = 0f;
    private bool isAnimatingSpellBar = false;
    private const float SPELL_BAR_ANIM_DURATION = 0.4f;

    void Awake()
    {
        _playerMove = GetComponent<PlayerMove>();

        if (BossPracticeManager.IsPracticeMode)
        {
            stockLives = 0; life = 0;
        }
        else if (GameModeManager.IsStoryMode)
        {
            life = 3;
            stockLives = 3;
        }
        else
        {
            life = 0;
            stockLives = 0;
        }

        playerCollider = GetComponentInChildren<Collider2D>();
        if (playerCollider != null)
        {
            originalColliderScale = playerCollider.transform.localScale;
        }
    }

    void Start()
    {
        currentHP = maxHP;
        ApplyCharacterSettings();
        StartCoroutine(SetupInitialUI());
        StartCoroutine(InitUIWithDelay());
    }

    private IEnumerator InitUIWithDelay()
    {
        yield return null;
        UpdateUI();
    }

    private IEnumerator SetupInitialUI()
    {
        yield return null;
        currentHP = maxHP;
        UpdateUI();

        if (orangeBar != null)
        {
            orangeBar.maxValue = maxHP;
            orangeBar.value = currentHP;
        }
    }

    private void ApplyCharacterSettings()
    {
        if (characterData != null)
        {
            if (characterNameText != null)
            {
                characterNameText.text = characterData.characterName;
                characterNameText.color = characterData.imageColor;
            }
        }
    }

    void Update()
    {
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;

        // 【VJT実行中のリアルタイム毎フレーム制御】
        if (isSpellCardActive)
        {
            if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.StopTimer();

            spellTimer -= Time.deltaTime;
            float timeRatio = Mathf.Clamp01(spellTimer / totalSpellDuration);

            _playerMove.ultimateEnergy = initialUltimateEnergy * timeRatio;

            if (spellTimer <= 0f)
            {
                spellTimer = 0f;
                _playerMove.ultimateEnergy = 0f;
                DeactivateSpellCard(false);
            }

            if (isAnimatingSpellBar)
            {
                appearanceElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(appearanceElapsed / SPELL_BAR_ANIM_DURATION);
                float easedT = t * t * (3f - 2f * t);
                animatedSpellHP = Mathf.Lerp(0f, spellHP, easedT);

                if (t >= 1f) isAnimatingSpellBar = false;
            }
            else
            {
                animatedSpellHP = spellHP;
            }
        }

        // 術式焼き切れデバフのタイマー処理
        if (isOverheated)
        {
            overheatTimer -= Time.deltaTime;
            if (overheatTimer <= 0f)
            {
                isOverheated = false;
                Debug.Log("<color=green>⏳【VJT】術式冷却完了。通常状態へ復帰しました。</color>");
            }
        }

        // ゲージの滑らかな減衰補間
        float targetSliderValue = isSpellCardActive ? animatedSpellHP : currentHP;
        if (orangeBar != null && orangeBar.value > targetSliderValue)
        {
            orangeBar.value = Mathf.Lerp(orangeBar.value, targetSliderValue, Time.deltaTime * lerpSpeed);
            if (orangeBar.value - targetSliderValue < 0.1f) orangeBar.value = targetSliderValue;
        }

        UpdateUI();
    }

    public void ActivateSpellCard()
    {
        if (isSpellCardActive || _playerMove.ultimateEnergy < 200f) return;

        if (SpellCardManager.Instance != null && !SpellCardManager.Instance.TryRequestVJT(this))
        {
            return;
        }

        Debug.Log($"<color=cyan>🔥【聖少女領域 - VJT展開】現在のゲージ残量: {_playerMove.ultimateEnergy}%</color>");

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.CARDCALL, 0.5f);

        isSpellCardActive = true;
        isOverheated = false;

        initialUltimateEnergy = _playerMove.ultimateEnergy;

        // 🌟通常HPを別腹ロック固定保存
        preSpellHP = currentHP;

        float fullArmorHP = maxHP * 30f;

        float t = Mathf.InverseLerp(200f, 300f, initialUltimateEnergy);
        totalSpellDuration = Mathf.Lerp(minSpellDuration, maxSpellDuration, t);
        spellTimer = totalSpellDuration;

        float spawnHPRatio = Mathf.Lerp(0.75f, 1.0f, t);

        spellMaxHP = fullArmorHP;
        spellHP = fullArmorHP * spawnHPRatio;

        isAnimatingSpellBar = true;
        appearanceElapsed = 0f;
        animatedSpellHP = 0f;

        if (playerCollider != null)
        {
            playerCollider.transform.localScale = originalColliderScale * 30f;
        }
        if (spellBarrier != null)
        {
            spellBarrier.SetBarrierActive(true);
        }
        UpdateUI();
        SyncBarsImmediately();
    }

    public void DeactivateSpellCard(bool isDefeatedByDamage)
    {
        if (!isSpellCardActive) return;
        isSpellCardActive = false;

        // 🌟 破砕時のみ1.0秒間の無敵保護を発動、時間切れやULT時はスキップしてクールダウンへ
        if (isDefeatedByDamage)
        {
            SetInvincible(1.0f);
        }

        if (spellBarrier != null)
        {
            spellBarrier.SetBarrierActive(false);
        }

        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SPELL_OFF, 0.5f);

        if (SpellCardManager.Instance != null)
        {
            SpellCardManager.Instance.ReleaseVJT(this);
        }

        if (playerCollider != null)
        {
            playerCollider.transform.localScale = originalColliderScale;
        }

        // 通常ライフを無傷復元
        currentHP = preSpellHP;

        spellHP = 0f;
        spellMaxHP = 0f;
        spellTimer = 0f;
        totalSpellDuration = 0f;
        initialUltimateEnergy = 0f;

        isOverheated = true;
        overheatTimer = overheatDuration;

        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.ResumeTimer();
        }

        UpdateUI();
        SyncBarsImmediately();
    }

    public bool ApplyDamage(int amount)
    {
        if (isSpellCardActive)
        {
            spellHP -= amount;
            UpdateUI();

            if (spellHP <= 0)
            {
                spellHP = 0;
                DeactivateSpellCard(true);
                return false;
            }
            return false;
        }

        currentHP -= amount;
        UpdateUI();

        if (currentHP <= 0)
        {
            currentHP = 0;
            return true;
        }
        return false;
    }

    public bool SubtractLifeAndCheckRebirth()
    {
        if (GameModeManager.IsStoryMode)
        {
            if (stockLives > 0)
            {
                stockLives--;
                life = stockLives;
                UpdateUI();
                return true;
            }
            return false;
        }
        else
        {
            if (_playerMove != null && _playerMove.Opponent != null)
            {
                PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
                if (oppStatus != null)
                {
                    oppStatus.life++;
                    oppStatus.UpdateUI();

                    if (oppStatus.life >= 2)
                    {
                        UpdateUI();
                        return false;
                    }
                }
            }

            UpdateUI();
            return true;
        }
    }

    public IEnumerator GradualHealthRecovery(float duration)
    {
        float startHP = currentHP;
        float elapsed = 0;

        if (orangeBar != null)
        {
            orangeBar.maxValue = maxHP;
            orangeBar.value = startHP;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentHP = Mathf.Lerp(startHP, maxHP, elapsed / duration);
            UpdateUI();
            yield return null;
        }
        currentHP = maxHP;
        UpdateUI();

        if (orangeBar != null) orangeBar.value = maxHP;
    }

    public IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (screenFader == null) yield break;
        float startAlpha = screenFader.alpha;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            screenFader.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        screenFader.alpha = targetAlpha;
    }

    private void UpdateUI()
    {
        if (winText != null && !winText.gameObject.activeSelf) winText.gameObject.SetActive(false);
        if (koText != null && !koText.gameObject.activeSelf) koText.gameObject.SetActive(false);

        bool isVs = !GameModeManager.IsStoryMode;

        if (lifeUI != null)
        {
            lifeUI.SetCountVsVariant(life, lifePieces, lifePiecesRequired, isVs);
        }

        if (isSpellCardActive)
        {
            if (spellHpBar != null)
            {
                spellHpBar.gameObject.SetActive(true);
                spellHpBar.maxValue = spellMaxHP;
                spellHpBar.value = animatedSpellHP;
            }

            if (hpBar != null)
            {
                hpBar.gameObject.SetActive(true);
                hpBar.maxValue = maxHP;
                hpBar.value = preSpellHP; // 🌟通常HPを満タン100の位置でフリーズ固定ロック！
                SetSliderAlpha(hpBar, 0.3f);
            }
        }
        else
        {
            if (hpBar != null)
            {
                hpBar.gameObject.SetActive(true);
                hpBar.maxValue = maxHP;
                hpBar.value = currentHP;
                SetSliderAlpha(hpBar, 1.0f);
            }

            if (spellHpBar != null)
            {
                spellHpBar.gameObject.SetActive(false);
            }
        }

        if (orangeBar != null)
        {
            orangeBar.maxValue = isSpellCardActive ? spellMaxHP : maxHP;
            float currentTargetValue = isSpellCardActive ? animatedSpellHP : currentHP;
            if (currentTargetValue >= (isSpellCardActive ? spellMaxHP : maxHP))
            {
                orangeBar.value = isSpellCardActive ? spellMaxHP : maxHP;
            }
        }
    }

    public void SyncBarsImmediately()
    {
        if (hpBar != null)
        {
            hpBar.maxValue = isSpellCardActive ? spellMaxHP : maxHP;
            hpBar.value = isSpellCardActive ? animatedSpellHP : currentHP;
        }
        if (orangeBar != null)
        {
            orangeBar.maxValue = isSpellCardActive ? spellMaxHP : maxHP;
            orangeBar.value = isSpellCardActive ? animatedSpellHP : currentHP;
        }
    }

    private void SetSliderAlpha(UnityEngine.UI.Slider slider, float alpha)
    {
        UnityEngine.UI.Image[] images = slider.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in images)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }

    // =========================================================================
    // 🌟【修復・完全溶接】：外部クラスから呼び出される中枢関数群
    // =========================================================================

    /// <summary>
    /// 一時停止メニュー等からリトライ（コンティニュー）を確定した際の初期化処理
    /// </summary>
    public void PerformContinue()
    {
        continueCount++;
        currentHP = maxHP;
        isSpellCardActive = false;
        isOverheated = false;
        spellHP = 0f;
        spellMaxHP = 0f;
        spellTimer = 0f;
        totalSpellDuration = 0f;
        initialUltimateEnergy = 0f;
        UpdateUI();

        PlayerHitHandler hitHandler = GetComponentInChildren<PlayerHitHandler>();
        if (hitHandler != null) hitHandler.StartRebirthFromContinue();
    }

    /// <summary>
    /// コンティニュー回数のカウンタを完全にリセットする
    /// </summary>
    public void ResetContinueCount()
    {
        continueCount = 0;
    }

    /// <summary>
    /// 完全に勝敗が決したリザルト画面（ゲームオーバー画面）をトリガーする
    /// </summary>
    public void TriggerGameOver()
    {
        if (pauseManager == null) return;

        if (BossPracticeManager.IsPracticeMode)
        {
            pauseManager.SetPracticeResultMode(true, false);
        }
        else
        {
            pauseManager.SetGameOverMode(true);
            pauseManager.PauseGame();
        }
    }

    public void AddLife(int amount)
    {
        life = Mathf.Min(life + amount, 8);
        UpdateUI();
        if (extendUI != null) extendUI.Show("Extend!!", new Color(1f, 0.4f, 0.7f));
    }

    public void AddLifePiece(int amount)
    {
        lifePieces += amount;
        if (lifePieces >= lifePiecesRequired)
        {
            lifePieces -= lifePiecesRequired;
            AddLife(1);
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SE_EXTEND2);
        }
        UpdateUI();
    }
    public IEnumerator PlayKOAnimation()
    {
        if (koText == null) yield break;
        koText.text = "Game Set !!";
        koText.gameObject.SetActive(true);

        koText.transform.localScale = Vector3.zero;
        float elapsed = 0;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float scale = 0;
            if (t < 0.7f) scale = Mathf.Lerp(0, 1.5f, t / 0.7f);
            else scale = Mathf.Lerp(1.5f, 1.0f, (t - 0.7f) / 0.3f);

            koText.transform.localScale = Vector3.one * scale;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        koText.transform.localScale = Vector3.one;
    }

    public IEnumerator FadeOutKOAnimation(float duration)
    {
        if (koText == null) yield break;
        Color startColor = koText.color;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(1, 0, elapsed / duration);
            koText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
        koText.gameObject.SetActive(false);
        koText.color = startColor;
    }

    public void SetInvincible(float duration)
    {
        invincibleTimer = duration;
        deathBombTimer = 0;
        if (_playerMove != null) _playerMove.SetInvincible(duration);
    }
}