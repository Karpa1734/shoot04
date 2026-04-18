using UnityEngine;
using System.Collections;
using KanKikuchi.AudioManager;

public class PlayerStatusManager : MonoBehaviour
{
    // ★ Instanceを廃止したため、 playerId で個体識別します
    [Header("Player Settings")]
    public int playerId = 1;

    [Header("Resources")]
    public int life = 2;
    public int bomb = 3;
    public int power = 0;
    public int maxPower = 128;
    public int initialLife = 2;
    public int initialSpell = 3;

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

    [Header("UI References")]
    public PlayerStatusUI lifeUI;
    public PlayerStatusUI spellUI;
    public ExtendNotificationUI extendUI;

    [Header("Global References")]
    public PauseManager pauseManager;

    private PlayerMove _playerMove;

    public bool IsInvincible => invincibleTimer > 0;
    public bool IsDeathBombWindow => deathBombTimer > 0;

    void Awake()
    {
        _playerMove = GetComponent<PlayerMove>();

        // 練習モードの判定
        if (BossPracticeManager.IsPracticeMode)
        {
            life = 0; bomb = 0;
        }
        else
        {
            life = initialLife; bomb = initialSpell;
        }
    }

    void Start()
    {
        StartCoroutine(SetupInitialUI());
    }

    private IEnumerator SetupInitialUI()
    {
        yield return null;
        UpdateUI();
    }

    void Update()
    {
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;
    }

    // --- コンティニュー関連の復活 ---

    public void PerformContinue()
    {
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

    public bool SubtractLifeAndCheckRebirth()
    {
        if (life > 0)
        {
            life--;
            bomb = initialSpell;
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
        if (lifeUI != null) lifeUI.SetCount(life, lifePieces, lifePiecesRequired);
        if (spellUI != null) spellUI.SetCount(bomb, bombPieces, bombPiecesRequired);
    }

    public void SetInvincible(float duration)
    {
        invincibleTimer = duration;
        deathBombTimer = 0;
        if (_playerMove != null) _playerMove.SetInvincible(duration);
    }
}