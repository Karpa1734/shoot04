// --- シェルクラス：データ保持と外部スクリプト用インターフェースのみを残す ---
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    // ★ 複数プレイヤー対応のための管理リスト
    private static List<PlayerMove> _allPlayers = new List<PlayerMove>();

    // ★ 既存スクリプトとの整合性を保つための Singleton プロパティ
    // リストの最初に見つかったプレイヤーを返すことで、既存の敵の弾やUIがエラーにならないようにします
    public static PlayerMove Instance => _allPlayers.Count > 0 ? _allPlayers[0] : null;

    // ★ 全プレイヤーにアクセスするための公開プロパティ
    public static IReadOnlyList<PlayerMove> AllPlayers => _allPlayers;

    [System.Serializable]
    public struct ReplayFrame
    {
        public float h;
        public float v;
        public bool slow;
        public bool shotZ;
        public bool shotX;
        public bool shotC;
        public bool shotV;

        // ★ 既存スクリプト（PlayerShotManager等）との互換性を保つためのプロパティ
        // .shot を参照すると自動的に .shotZ の値を返します
        public bool shot => shotZ;
        // .bomb を参照すると自動的に .shotX の値を返します
        public bool bomb => shotX;
    }

    public enum ReplayMode { None, Recording, Playing }
    public ReplayMode currentMode = ReplayMode.None;
    public List<ReplayFrame> replayData = new List<ReplayFrame>();
    private int currentFrame = 0;

    // 各インスタンスごとに保持されるため、プレイヤー間でデータが混ざることはありません
    public ReplayFrame currentFrameInput;

    private float invincibleTimer = 0f;
    private float deathBombTimer = 0f;

    public bool IsInvincible => invincibleTimer > 0;
    public bool IsInDeathBombWindow => deathBombTimer > 0;

    private SpriteRenderer sr;

    void Awake()
    {
        Time.timeScale = 1f;
    }

    // ★ 有効化・無効化時にリストを更新する
    void OnEnable()
    {
        if (!_allPlayers.Contains(this)) _allPlayers.Add(this);
    }

    void OnDisable()
    {
        _allPlayers.Remove(this);
    }

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        // 状態タイマーの更新
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;

        // リプレイ処理（データの出し入れのみ担当）
        UpdateReplayLogic();
    }

    private void UpdateReplayLogic()
    {
        if (currentMode == ReplayMode.Playing)
        {
            if (currentFrame < replayData.Count)
            {
                currentFrameInput = replayData[currentFrame];
                currentFrame++;
            }
        }
        else if (currentMode == ReplayMode.Recording)
        {
            replayData.Add(currentFrameInput);
        }
    }

    void LateUpdate()
    {
        if (IsInvincible) UpdateInvincibleVisual();
        else if (sr != null && sr.color != Color.white) ResetVisual();
    }

    public void SetInvincible(float duration) => invincibleTimer = duration;
    public void StartDeathBombWindow(float duration) { if (!IsInvincible) deathBombTimer = duration; }

    private void UpdateInvincibleVisual()
    {
        if (sr == null) return;
        float pingPong = Mathf.PingPong(Time.time * 20f, 1f);
        float alpha = 0.3f + pingPong * 0.7f;
        sr.color = Color.Lerp(new Color(0.4f, 0.4f, 1f, alpha), new Color(1f, 1f, 1f, alpha), pingPong);
    }

    private void ResetVisual() { if (sr != null) sr.color = Color.white; }
}