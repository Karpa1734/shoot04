using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private static List<PlayerMove> _allPlayers = new List<PlayerMove>();
    public static IReadOnlyList<PlayerMove> AllPlayers => _allPlayers;

    // ★ エラー解消のための「後方互換性」プロパティ
    // これを復活させることで、既存の敵やアイテムのスクリプトが壊れなくなります
    public static PlayerMove Instance => (_allPlayers != null && _allPlayers.Count > 0) ? _allPlayers[0] : null;
    [Header("Player Settings")]
    public int playerId; // 1 または 2 をインスペクターで設定
    public PlayerMove Opponent => _allPlayers.Find(p => p != this);

    public static bool CanInput = true; // 移動・ポーズ等の基本操作
    public static bool CanShoot = true; // ショット・スキルの使用許可
    [Header("Energy State")]
    public float currentEnergy; // 現在のコスト残量[cite: 10]
    public float maxEnergy = 100f; // 最大値（Start時にDataから上書き）[cite: 10]
    [Header("Ultimate Energy")]
    public float ultimateEnergy = 0f;
    public const float MAX_ULTIMATE_ENERGY = 300f;

    // 💡[HideInInspector] を外し、PlayerStatusManagerから直接叩き込める正式な実数値プロパティとして一本化！
    // 💡ランク(rankMMPRegen)から逆算されたマナ秒間回復量がここに格納されます。
    public float energyRegenRate = 15f;

    [System.Serializable]
    public struct ReplayFrame // もし class で定義されている場合は public class ReplayFrame
    {
        public float h;
        public float v;
        public bool slow;
        public bool shotZ;
        public bool shotX;
        public bool shotC;
        public bool shotV;

        // ★ 追加：バリアとアルティメットの入力状態を受け取るためのフィールドを定義
        public bool barrier;
        public bool ultimate;
    }
    public float skillSpeedMultiplier = 1.0f; // デフォルトは等倍
    public enum ReplayMode { None, Recording, Playing }
    public ReplayMode currentMode = ReplayMode.None;
    public List<ReplayFrame> replayData = new List<ReplayFrame>();
    private int currentFrame = 0;
    public ReplayFrame currentFrameInput;
    [Header("Movement Constants")]
    public float normalSpeed = 5.0f; // 通常時の速度
    public float focusSpeed = 2.0f;  // 低速移動時の速度
    private float invincibleTimer = 0f;
    private float deathBombTimer = 0f;
    public bool IsInvincible => invincibleTimer > 0;
    public bool IsInDeathBombWindow => deathBombTimer > 0;
    private Vector2 _currentVelocity = Vector2.zero;
    private Rigidbody2D _rb2d;
    private SpriteRenderer sr;

    [Header("🌀 External Field Forces")]
    [Tooltip("相手の暴食領域などから受けている、現在のフレームの外部引力ベクトル（自動クリア型）")]
    public Vector2 externalPullVelocity = Vector2.zero;

    /// <summary>
    /// 🍰 外部の領域から、この機体に対して強制的な引力速度ベクトルを注入します。
    /// </summary>
    public void AddExternalPull(Vector2 pullForce)
    {
        externalPullVelocity += pullForce;
    }

    // UIや外部マネージャーが参照する、現在の実質的な「アルカナストック数（0〜3）」
    public int ArcanaStockCount => Mathf.FloorToInt(ultimateEnergy / 100f);

    // 現在蓄積中のストック内の残り％（0.0 ~ 1.0f）➔ UIのSliderのfillAmount等にそのままバインド可能
    public float CurrentArcanaGaugeRatio
    {
        get
        {
            if (ultimateEnergy >= MAX_ULTIMATE_ENERGY) return 1.0f;
            return (ultimateEnergy % 100f) / 100f;
        }
    }
    // 💡【新設：機体IDロック型・速度主権絶対防壁システム】
    public void SetSpeedFromRank(float speed)
    {
        // 🎯 核心：このコンポーネントがアタッチされている「自身の playerId (1 or 2)」を完全にロック！
        // 💡 外部（他プレイヤー）のリセットルーチンからの誤混入を完全に弾き返します。
        this.normalSpeed = speed;
        this.focusSpeed = speed * 0.4f;

        // 💡【上書き負け撲滅】：現在のフレームで速度倍率（skillSpeedMultiplier）が
        // 💡 他の干渉によって不当に変調されているリスクをパージするため、
        // 💡 固有の速度が確定したこの瞬間に、等速の「1.0f」を自身の体にガチッと上書き再溶接します！
        this.skillSpeedMultiplier = 1.0f;

        Debug.Log($"<color=cyan>🏃【敏捷同期・主権確定】Player {this.playerId} ➔ 高速速度: {this.normalSpeed} (倍率:{this.skillSpeedMultiplier}) / 低速速度: {this.focusSpeed} の独立バインドに成功！</color>");
    }
    void Awake()
    {
        Time.timeScale = 1f;
        // ★ 修正：スクリプトの有効・無効に関わらず、生存している限りリストに入れる
        if (!_allPlayers.Contains(this)) _allPlayers.Add(this);
        currentEnergy = maxEnergy; // 初期状態は満タン
    }
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;
        UpdateReplayLogic();
    }
    /// <summary>
    /// 超必殺技ゲージを指定量（パーセント単位）加算する
    /// </summary>
    /// <param name="amount">加算する量 (100 = 1ストック分)</param>
    public void AddUltimateEnergy(float amount)
    {
        // 🌟【最重要ガード】：もし現在、自身が聖少女領域（VJT）を展開中である場合は、
        // どんな外部要因やスキル使用・ヒットによるゲージ加算もすべて完全に無視（弾き返し）ます！
        PlayerStatusManager status = GetComponent<PlayerStatusManager>();
        if (status != null && status.isSpellCardActive)
        {
            return; // 領域中のULTゲージは外部干渉を一切受け付けません
        }

        // --- 従来のゲージ加算処理 ---
        ultimateEnergy = Mathf.Clamp(ultimateEnergy + amount, 0f, 300f);
    }
    private void UpdateReplayLogic()
    {
        // ★ 追加：入力がロックされている間は、入力を空（デフォルト値）にする
        if (!CanInput)
        {
            currentFrameInput = new ReplayFrame();
            return;
        }
        if (currentMode == ReplayMode.Playing && currentFrame < replayData.Count)
        {
            currentFrameInput = replayData[currentFrame];
            currentFrame++;
        }
        else if (currentMode == ReplayMode.Recording)
        {
            replayData.Add(currentFrameInput);
        }
    }
    void OnDestroy()
    {
        // ★ 追加：オブジェクトが完全に破棄された時だけリストから削除
        if (_allPlayers.Contains(this)) _allPlayers.Remove(this);
    }
    void LateUpdate()
    {
       // if (IsInvincible) UpdateInvincibleVisual();
        //else if (sr != null && sr.color != Color.white) ResetVisual();
    }
    void FixedUpdate()
    {

    }

    public void SetInvincible(float duration) => invincibleTimer = duration;
    public void StartDeathBombWindow(float duration) { if (!IsInvincible) deathBombTimer = duration; }

    private void UpdateInvincibleVisual()
    {
        if (sr == null) return;
        float pingPong = Mathf.PingPong(Time.time * 20f, 1f);
        sr.color = Color.Lerp(new Color(0.4f, 0.4f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.8f), pingPong);
    }
    private void ResetVisual() { if (sr != null) sr.color = Color.white; }
}