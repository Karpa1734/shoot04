// --- PlayerStatusManager.cs 【VS勝星バグ・2勝リーサル完全決着版】 ---
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
    public int life = 2;
    public int bomb = 3;
    public int power = 0;
    public int maxPower = 128;
    public int initialLife = 2;
    public int initialSpell = 3;
    public float currentHP = 50f;
    public float maxHP = 50f;
    public int stockLives = 2;

    [Header("Piece Settings")]
    public int lifePieces = 0;
    public int bombPieces = 0;
    public int lifePiecesRequired = 3;
    public int bombPiecesRequired = 3;

    [Header("Timers")]
    public float invincibleTimer = 0f;
    public float deathBombTimer = 0f;

    [Header("Statistics")]
    public int continueCount = 0;
    public TextMeshProUGUI countdownText;

    [Header("UI References")]
    public PlayerStatusUI lifeUI;
    public PlayerStatusUI spellUI;
    public ExtendNotificationUI extendUI;

    [Header("Round Transition")]
    public CanvasGroup screenFader;

    [Header("Global References")]
    public PauseManager pauseManager;

    private PlayerMove _playerMove;

    public bool IsInvincible => invincibleTimer > 0;
    public bool IsDeathBombWindow => deathBombTimer > 0;

    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI winText;
    public TextMeshProUGUI koText;
    public UnityEngine.UI.Slider hpBar;
    public UnityEngine.UI.Slider orangeBar;
    public float lerpSpeed = 2.0f;

    void Awake()
    {
        _playerMove = GetComponent<PlayerMove>();

        if (BossPracticeManager.IsPracticeMode)
        {
            stockLives = 0; life = 0; bomb = 0;
        }
        else if (GameModeManager.IsStoryMode)
        {
            initialLife = 3;
            stockLives = 3;
            life = 3;
            bomb = initialSpell;
        }
        else
        {
            // 🌟 VSモード：初期値設定
            initialLife = 2;   // 2マッチ先取（2回撃破でゲームセット）
            stockLives = 2;    // 残り体力（2回死んだら終わり）
            life = 0;          // 獲得した勝星（最初は0個点灯）
            bomb = initialSpell;
        }
    }

    void Start()
    {
        currentHP = maxHP;
        bomb = initialSpell;

        // 🌟 修正の核心：Start() での life = initialLife; の誤上書きを完全撤廃！
        // これにより、VSモード時に0点灯から正常スタートするようになります。
        if (GameModeManager.IsStoryMode)
        {
            life = initialLife;
        }

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

    /// <summary>
    /// 🌟 修正の核心：VSモードの2本先取リーサル判定を完全組み込み
    /// </summary>
    public bool SubtractLifeAndCheckRebirth()
    {
        if (stockLives > 0)
        {
            stockLives--;

            if (GameModeManager.IsStoryMode)
            {
                life = stockLives;
                UpdateUI();
                return true; // ストーリーは残機がある限り復活可能
            }
            else
            {
                // =========================================================================
                // 🌟【VSモード：勝星加算 ＆ 2勝先取の完全決着ジャッジ】
                // =========================================================================
                if (_playerMove != null && _playerMove.Opponent != null)
                {
                    PlayerStatusManager oppStatus = _playerMove.Opponent.GetComponent<PlayerStatusManager>();
                    if (oppStatus != null)
                    {
                        // 1. 勝った側（対戦相手）の星を増やす
                        oppStatus.life++;
                        oppStatus.UpdateUI();

                        // 2. 🚨【超重要】：もし相手が「2勝」に達したら、この瞬間に試合完全決着！
                        if (oppStatus.life >= 2)
                        {
                            UpdateUI(); // 自分のUIも最終同期（自分の全敗が確定）
                            return false; // ❌ 復活を「拒否」して、完全なる爆散ゲームセットへ落とす
                        }
                    }
                }

                UpdateUI();
                return true; // まだ相手が2勝未満（1勝目など）なら、次のラウンドへ仕切り直し（復活）
            }
        }
        return false;
    }

    public IEnumerator GradualHealthRecovery(float duration)
    {
        float startHP = currentHP;
        float elapsed = 0;

        // 🌟【最重要】：回復が始まる瞬間に、背面のオレンジバーを現在の低いHP（startHP）にガチッと合わせる！
        // これにより、減少補完ロジックのバッティングや取り残しを完璧に防ぎます。
        if (orangeBar != null)
        {
            orangeBar.maxValue = maxHP;
            orangeBar.value = startHP;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentHP = Mathf.Lerp(startHP, maxHP, elapsed / duration);

            // 毎フレームUIを更新し、裏のバーも一緒に引き上げます
            UpdateUI();
            yield return null;
        }

        currentHP = maxHP;
        UpdateUI();

        // 🌟【念押し】：回復完了時に、双方のバーが完全に満タン（maxHP）で一致するように完全ホールド
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

    void Update()
    {
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;

        if (orangeBar != null && orangeBar.value > currentHP)
        {
            orangeBar.value = Mathf.Lerp(orangeBar.value, currentHP, Time.deltaTime * lerpSpeed);
            if (orangeBar.value - currentHP < 0.1f) orangeBar.value = currentHP;
        }
    }

    public void SyncBarsImmediately()
    {
        currentHP = maxHP;
        if (hpBar != null)
        {
            hpBar.maxValue = maxHP;
            hpBar.value = currentHP;
        }
        if (orangeBar != null)
        {
            orangeBar.maxValue = maxHP;
            orangeBar.value = currentHP;
        }
    }

    public bool ApplyDamage(int amount)
    {
        currentHP -= amount;
        UpdateUI();

        if (currentHP <= 0)
        {
            currentHP = 0;
            return true;
        }
        return false;
    }

    public void PerformContinue()
    {
        continueCount++;
        currentHP = maxHP;
        stockLives = initialLife;
        bomb = initialSpell;
        UpdateUI();

        PlayerHitHandler hitHandler = GetComponentInChildren<PlayerHitHandler>();
        if (hitHandler != null) hitHandler.StartRebirthFromContinue();
    }

    public void ResetContinueCount() => continueCount = 0;

    public bool AddPower(int amount)
    {
        if (power >= maxPower) return false;
        power = Mathf.Min(power + amount, maxPower);
        return true;
    }

    public void AddLife(int amount)
    {
        life = Mathf.Min(life + amount, 8);
        UpdateUI();
        if (extendUI != null) extendUI.Show("Extend!!", new Color(1f, 0.4f, 0.7f));
    }

    public void AddBomb(int amount)
    {
        bomb = Mathf.Min(bomb + amount, 8);
        if (extendUI != null) extendUI.Show("Bomb Up!!", new Color(0.5f, 1f, 0.5f));
        UpdateUI();
    }

    public bool UseSpell()
    {
        if (bomb > 0)
        {
            bomb--;
            UpdateUI();
            return true;
        }
        return false;
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

    public void AddBombPiece(int amount)
    {
        bombPieces += amount;
        if (bombPieces >= bombPiecesRequired)
        {
            bombPieces -= bombPiecesRequired;
            AddBomb(1);
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.GETSPELLCARD);
        }
        UpdateUI();
    }

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

    private void UpdateUI()
    {
        if (winText != null) winText.gameObject.SetActive(false);
        if (koText != null) koText.gameObject.SetActive(false);

        bool isVs = !GameModeManager.IsStoryMode;

        if (lifeUI != null)
        {
            lifeUI.SetCountVsVariant(life, lifePieces, lifePiecesRequired, isVs);
        }

        if (spellUI != null)
        {
            spellUI.SetCountVsVariant(bomb, bombPieces, bombPiecesRequired, false);
        }

        if (hpBar != null)
        {
            hpBar.maxValue = maxHP;
            hpBar.value = currentHP;
        }

        // 🌟【修正】：裏のオレンジバーの最大値を保証しつつ、
        // 🌟もし現在値が満タンをオーバーしていたり、回復完了直後であれば安全弁として上限クリップします。
        if (orangeBar != null)
        {
            orangeBar.maxValue = maxHP;
            if (currentHP >= maxHP)
            {
                orangeBar.value = maxHP;
            }
        }
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