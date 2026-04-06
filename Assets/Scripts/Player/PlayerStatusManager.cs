using UnityEngine;
using KanKikuchi.AudioManager;

public class PlayerStatusManager : MonoBehaviour
{
    public static PlayerStatusManager Instance;

    [Header("Resources")]
    public int life = 2;          // から移行
    public int bomb = 3;          // から移行
    public int power = 0;         // から移行
    public int maxPower = 128;    // から移行
    public int initialLife = 2;   //
    public int initialSpell = 3;  //

    [Header("Piece Settings")]
    public int lifePieces = 0;   // 現在の残機のかけら
    public int bombPieces = 0;   // 現在のボムのかけら

    // ★個別の要求数（インスペクターで調整可能に）
    public int lifePiecesRequired = 3;
    public int bombPiecesRequired = 3;
    [Header("Replay Settings")]
    public int replaySeed; // 記録したシード値
    public bool isReplaying = false;


    [Header("Timers")]
    public float invincibleTimer = 0f; //
    public float deathBombTimer = 0f;  //

    [Header("Statistics")]
    public int continueCount = 0;

    [Header("UI References")]
    public PlayerStatusUI lifeUI;
    public PlayerStatusUI spellUI;
    public PauseManager pauseManager;
    [Header("Extended UI Reference")]
    public ExtendNotificationUI extendUI;
    public bool IsInvincible => invincibleTimer > 0;
    public bool IsDeathBombWindow => deathBombTimer > 0;
    [Header("Debug Settings")] private bool isDebugInvincible = false; // デバッグ状態を覚える変数
    void Awake()
    {
        if (Instance == null) Instance = this;

        // --- リプレイの再現性を確保するために乱数を固定 ---
        if (isReplaying)
        {
            Random.InitState(replaySeed);
        }
        else
        {
            // 新規プレイ時は現在の時間などからシードを作成して保存
            replaySeed = System.DateTime.Now.Millisecond;
            Random.InitState(replaySeed);
        }

        // 練習モードの判定などはそのまま
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
        // Start内で直接呼ぶのではなく、コルーチンを開始する
        StartCoroutine(SetupInitialUI());
    }

    private System.Collections.IEnumerator SetupInitialUI()
    {
        // 他の全てのオブジェクト（UIなど）の準備が整うのを1フレーム待機
        yield return null;

        // ここで最新の数値をUIに反映させる
        UpdateUI();
        Debug.Log("[Manager] 初期UIの同期が完了しました。要求数: " + lifePiecesRequired);
    }
    void Update()
    {

        // タイマー処理の集約
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;

        // ★追加：エディタ上のみ、キー入力でデバッグ無敵を切り替える
#if UNITY_EDITOR
        // Iキーで有効、Uキーで無効
        if (Input.GetKeyDown(KeyCode.I))
        {
            PlayerMove.Instance.SetInvincible(3.0f);
            isDebugInvincible = true;
            Debug.Log("<color=cyan>[Debug] 無敵固定: ON</color>");
        }
        if (Input.GetKeyDown(KeyCode.U))
        {
            PlayerMove.Instance.SetInvincible(0);
            isDebugInvincible = false;
            Debug.Log("<color=yellow>[Debug] 無敵固定: OFF</color>");
        }
        if (isDebugInvincible && PlayerMove.Instance != null)
        {
            // StatusManager ではなく、PlayerMove 側のタイマーを直接固定する
            PlayerMove.Instance.SetInvincible(3.0f);
        }
#endif
    }

    // --- アイテム効果などで使うメソッド ---

    public bool AddPower(int amount)
    {
        if (power >= maxPower) return false;
        power = Mathf.Min(power + amount, maxPower);
        // パワーのUI更新が必要ならここに追加
        return true;
    }

    public void AddLife(int amount)
    {
        life = Mathf.Min(life + amount, 8);
        UpdateUI();

        // ★チェック1：このログがコンソールに出るか？
        Debug.Log($"[Manager] AddLifeが呼ばれました。現在の残機: {life}");

        if (extendUI != null)
        {
            // ★チェック2：ここが呼ばれているか？
            extendUI.Show("Extend!!", new Color(1f, 0.4f, 0.7f));
        }
        else
        {
            // ★チェック3：もしこれがコンソールに出たら、インスペクターでの紐付け忘れです
            Debug.LogError("[Manager] extendUI がインスペクターでセットされていません！");
        }
    }


    // ★今回のエラーを解決するメソッド：コンティニュー回数をリセットする
    public void ResetContinueCount()
    {
        continueCount = 0;
    }

    public void AddBomb(int amount)
    {
        bomb = Mathf.Min(bomb + amount, 8);
        // ★追加：ピンク色で通知を表示
        if (extendUI != null) extendUI.Show("Extend!!", new Color(0.5f, 1f, 0.5f)); // ピンク
        UpdateUI();
    }

    // --- ゲーム進行管理 ---

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
            bomb = initialSpell; // 復活時はボム補充
            UpdateUI();
            return true;
        }
        return false;
    }

    public void PerformContinue()
    {
        continueCount++;
        life = initialLife;
        bomb = initialSpell;
        UpdateUI();

        PlayerHitHandler hitHandler = Object.FindFirstObjectByType<PlayerHitHandler>();
        if (hitHandler != null) hitHandler.StartRebirthFromContinue();
    }
    public void AddLifePiece(int amount)
    {
        lifePieces += amount;
        // ★ライフ用の要求数で判定
        if (lifePieces >= lifePiecesRequired)
        {
            lifePieces -= lifePiecesRequired;
            AddLife(1);
            SEManager.Instance.Play(SEPath.SE_EXTEND2);
        }
        UpdateUI();
    }

    public void AddBombPiece(int amount)
    {
        bombPieces += amount;
        // ★ボム用の要求数で判定
        if (bombPieces >= bombPiecesRequired)
        {
            bombPieces -= bombPiecesRequired;
            AddBomb(1);
            SEManager.Instance.Play(SEPath.GETSPELLCARD);
        }
        UpdateUI();
    }

    private void UpdateUI()
    {
        Debug.Log("!!"); // ★このログが出るか？（UI更新の確認）
                         // Life用のUIを更新（要求数も渡す）
        if (lifeUI != null)
        {
            Debug.Log("!!1"); // ★このログが出るか？（UI更新の確認）
            lifeUI.SetCount(life, lifePieces, lifePiecesRequired);
        }

        // Bomb用のUIを更新（要求数も渡す）
        if (spellUI != null)
        {
            Debug.Log("!!2"); // ★このログが出るか？（UI更新の確認）
            spellUI.SetCount(bomb, bombPieces, bombPiecesRequired);
        }
    }

    public void TriggerGameOver()
    {
        if (pauseManager == null) return;

        // ★修正：練習モード中なら「専用リザルト（敗北）」を表示
        if (BossPracticeManager.IsPracticeMode)
        {
            // 敗北（isWin = false）としてメニューを表示
            pauseManager.SetPracticeResultMode(true, false);
        }
        else
        {
            // 通常プレイ時は既存のゲームオーバー処理
            pauseManager.SetGameOverMode(true);
            pauseManager.PauseGame();
        }
    }

    // 無敵設定
    public void SetInvincible(float duration)
    {
        invincibleTimer = duration;
        deathBombTimer = 0;
    }

    public void StartDeathBombWindow(float duration)
    {
        if (!IsInvincible) deathBombTimer = duration;
    }
}