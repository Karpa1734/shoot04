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
    [System.Serializable]
    public struct ReplayFrame
    {
        public float h, v;
        public bool slow, shotZ, shotX, shotC, shotV;
        public bool shot => shotZ;
        public bool bomb => shotX;
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

    private SpriteRenderer sr;

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
        if (IsInvincible) UpdateInvincibleVisual();
        else if (sr != null && sr.color != Color.white) ResetVisual();
    }
    void FixedUpdate()
    {
        // 1. 基本速度（低速移動[cite: 8]判定を含む）
        float currentBaseSpeed = currentFrameInput.slow ? focusSpeed : normalSpeed;

        // 2. ★ 修正：倍率を掛けて最終速度を決定
        // ここで skillSpeedMultiplier が 0 なら、finalSpeed は確実に 0 になります[cite: 11]
        float finalSpeed = currentBaseSpeed * skillSpeedMultiplier;

        // 3. 入力方向を計算
        Vector3 moveDir = new Vector3(currentFrameInput.h, currentFrameInput.v, 0).normalized;

        // 4. 移動。finalSpeed が 0 なら transform.position は変化しません[cite: 11]
        transform.position += moveDir * finalSpeed * Time.fixedDeltaTime;
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