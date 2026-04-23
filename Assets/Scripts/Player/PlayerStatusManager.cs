using KanKikuchi.AudioManager;
using System.Collections;
using TMPro;
using UnityEngine;

public class PlayerStatusManager : MonoBehaviour
{
    // ★ Instanceを廃止したため、 playerId で個体識別します
    [Header("Player Settings")]
    public int playerId = 1;
    public PlayerSkillData characterData; // ★追加：自身のキャラデータ(ScriptableObject)
    [Header("Resources")]
    public int life = 2;
    public int bomb = 3;
    public int power = 0;
    public int maxPower = 128;
    public int initialLife = 2;
    public int initialSpell = 3;
    public float currentHP = 50f; // ★ ライフ数(int)からHP(float)へ
    public float maxHP = 50f;
    public int stockLives = 2;      // 従来のライフは「残機（ストック）」として保持

    [Header("Piece Settings")]
    public int lifePieces = 0;
    public int bombPieces = 0;
    public int lifePiecesRequired = 3;
    public int bombPiecesRequired = 3;

    [Header("Timers")]
    public float invincibleTimer = 0f;
    public float deathBombTimer = 0f;

    [Header("Statistics")]
    public int continueCount = 0; // コンティニュー回数
    public TextMeshProUGUI countdownText; // ★追加：カウントダウン表示用のTMP

    [Header("UI References")]
    public PlayerStatusUI lifeUI;
    public PlayerStatusUI spellUI;
    public ExtendNotificationUI extendUI;
    // --- PlayerStatusManager.cs 修正箇所 ---
    [Header("Round Transition")]
    public CanvasGroup screenFader; // 画面全体を覆う黒いパネル（CanvasGroup）
    [Header("Global References")]
    public PauseManager pauseManager;

    private PlayerMove _playerMove;

    public bool IsInvincible => invincibleTimer > 0;
    public bool IsDeathBombWindow => deathBombTimer > 0;
    // ★追加：HPバー（Slider）の参照
    public TextMeshProUGUI characterNameText; // ★追加：名前表示用UI
    public TextMeshProUGUI winText; // ★追加：画面中央に大きく「Wins!」と出す用のテキスト
    public TextMeshProUGUI koText; // ★追加：「K.O.」表示用テキスト
    public UnityEngine.UI.Slider hpBar;
    public UnityEngine.UI.Slider orangeBar; // ★追加：背面の減少用バー（オレンジ）
    public float lerpSpeed = 2.0f;          // ★追加：オレンジバーが追いつくスピード
    void Awake()
    {
        _playerMove = GetComponent<PlayerMove>();

        if (BossPracticeManager.IsPracticeMode)
        {
            stockLives = 0; life = 0; bomb = 0;
        }
        // ★ ストーリーモード：3機（初期ストック2）
        else if (GameModeManager.IsStoryMode)
        {
            initialLife = 2;
            stockLives = 2;
            life = 2;
            bomb = initialSpell;
        }
        // ★ 対戦モード：1ストック（初期ライフ1）
        // ※ 2マッチ先取（2回負けたら終わり）を想定した設定
        else
        {
            initialLife = 1;
            stockLives = 1;
            life = 1;
            bomb = initialSpell;
        }
    }

    void Start()
    {
        ApplyCharacterSettings();
        StartCoroutine(SetupInitialUI());
    }
    /// <summary>
    /// 指定した時間をかけてHPを最大まで回復させる
    /// </summary>
  
    private IEnumerator SetupInitialUI()
    {
        yield return null;

        // 現在のHPを最大値にリセット（念のため）
        currentHP = maxHP;

        UpdateUI();

        // ★ 追加：ゲーム開始時にオレンジのバーを現在のHP（満タン）に同期させる
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
/*
            // お好みで：HPバーの色もイメージカラーに合わせる場合
            if (hpBar != null)
            {
                var fill = hpBar.fillRect.GetComponent<UnityEngine.UI.Image>();
                if (fill != null) fill.color = characterData.imageColor;
            }
  */      }
    }
    public bool SubtractLifeAndCheckRebirth()
    {
        if (stockLives > 0)
        {
            stockLives--;
            life = stockLives;

            // ★ 削除：ここにあった currentHP = maxHP; を消す。
            // これにより、敗者はHP 0（空のバー）のままスタン時間を迎えます。

            UpdateUI();
            return true;
        }
        return false;
    }

    // 敗者復活時の回復演出（0から1秒かけて回復）
    public IEnumerator GradualHealthRecovery(float duration)
    {
        float startHP = currentHP;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentHP = Mathf.Lerp(startHP, maxHP, elapsed / duration);
            UpdateUI();
            yield return null;
        }
        currentHP = maxHP;
        UpdateUI();
    }
    // 画面をフェードさせるコルーチン
    public IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (screenFader == null) yield break;
        float startAlpha = screenFader.alpha;
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // スロー中でも動くように実時間を使用
            screenFader.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }
        screenFader.alpha = targetAlpha;
    }
    void Update()
    {
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;
        // ★追加：オレンジ色のバーを現在のHPへ向かって滑らかに減少させる
        if (orangeBar != null && orangeBar.value > currentHP)
        {
            // Mathf.Lerp を使うと、最初は速く、徐々にゆっくりと減ります
            orangeBar.value = Mathf.Lerp(orangeBar.value, currentHP, Time.deltaTime * lerpSpeed);

            // 差がごくわずかになったら値を同期させる
            if (orangeBar.value - currentHP < 0.1f) orangeBar.value = currentHP;
        }
    }
    // 念のため、復活（リスタート）時にも呼べる同期メソッド
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
    // --- コンティニュー関連の復活 ---

    // ★ ダメージを受けるメソッドを新設
    // ダメージ適用メソッド
    // --- PlayerStatusManager.cs 修正箇所 ---
    public bool ApplyDamage(int amount)
    {
        currentHP -= amount;
        UpdateUI(); // HPバー（Slider）を更新

        if (currentHP <= 0)
        {
            currentHP = 0;
            return true; // 撃墜（ダウン）確定
        }
        return false;
    }


    public void PerformContinue()
    {
        continueCount++;
        currentHP = maxHP; // HP全快
        stockLives = initialLife;
        bomb = initialSpell;
        UpdateUI();
        continueCount++;
        life = initialLife;
        bomb = initialSpell;
        UpdateUI();

        // 復活処理を呼ぶ（HitHandlerが自身の子にある前提）
        PlayerHitHandler hitHandler = GetComponentInChildren<PlayerHitHandler>();
        if (hitHandler != null) hitHandler.StartRebirthFromContinue();
    }

    public void ResetContinueCount()
    {
        continueCount = 0;
    }

    // --- ステータス操作メソッド ---

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

        // 練習モード中なら専用のリザルトを表示
        if (BossPracticeManager.IsPracticeMode)
        {
            pauseManager.SetPracticeResultMode(true, false);
        }
        else
        {
            // 通常プレイ時はゲームオーバー画面を表示してポーズ
            pauseManager.SetGameOverMode(true);
            pauseManager.PauseGame();
        }
    }
    private void UpdateUI()
    {
        // ★UIには現在のストック数（stockLives）を表示するように統一
        if (winText != null) winText.gameObject.SetActive(false); // 初期状態は非表示
        if (koText != null) koText.gameObject.SetActive(false); // ★初期状態は非表示
        if (lifeUI != null) lifeUI.SetCount(life, lifePieces, lifePiecesRequired);
        if (spellUI != null) spellUI.SetCount(bomb, bombPieces, bombPiecesRequired);
        if (hpBar != null)
        {
            hpBar.maxValue = maxHP;
            hpBar.value = currentHP;
        }
        if (orangeBar != null) orangeBar.maxValue = maxHP;
    }
    // ★追加：K.O.演出用のコルーチン
    public IEnumerator PlayKOAnimation()
    {
        if (koText == null) yield break;

        koText.text = "Game Set !!";
        koText.gameObject.SetActive(true);

        // K.O.時のSEを鳴らすとより良いです
        // SEManager.Instance.Play(SEPath.SE_KO); 

        // 簡易的なパンチ演出（スケールを大きくして戻す）
        koText.transform.localScale = Vector3.zero;
        float elapsed = 0;
        float duration = 0.5f;

        // ★スロー中も動くようにRealtime（実時間）を使用
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // 0 -> 1.5 -> 1.0 へとスケールを変化させる（飛び出すような演出）
            float scale = 0;
            if (t < 0.7f) scale = Mathf.Lerp(0, 1.5f, t / 0.7f);
            else scale = Mathf.Lerp(1.5f, 1.0f, (t - 0.7f) / 0.3f);

            koText.transform.localScale = Vector3.one * scale;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        koText.transform.localScale = Vector3.one;
    }
    /// <summary>
    /// K.O.テキストを滑らかにフェードアウトさせる
    /// </summary>
    public IEnumerator FadeOutKOAnimation(float duration)
    {
        if (koText == null) yield break;

        Color startColor = koText.color;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // スロー中でも一定速度で消えるように実時間を使用
            float alpha = Mathf.Lerp(1, 0, elapsed / duration);
            koText.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        koText.gameObject.SetActive(false);

        // 次回表示のために色を元に戻しておく
        koText.color = startColor;
    }

    public void SetInvincible(float duration)
    {
        invincibleTimer = duration;
        deathBombTimer = 0;
        if (_playerMove != null) _playerMove.SetInvincible(duration);
    }
}