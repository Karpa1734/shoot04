using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float highSpeed = 4.5f;
    [SerializeField] private float lowSpeed = 2.0f;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Vector2 inputVec;

    [System.Serializable]
    public struct ReplayFrame
    {
        public float h;     // 水平入力
        public float v;     // 垂直入力
        public bool slow;   // 低速(Shift)
        public bool bomb;   // ボム入力(仮)
    }
    public enum ReplayMode { None, Recording, Playing }
    public ReplayMode currentMode = ReplayMode.None;
    public List<ReplayFrame> replayData = new List<ReplayFrame>();
    private int currentFrame = 0;
    [Header("Movement Bounds")]
    public float minX = -4.0f;
    public float maxX = 4.0f;
    public float minY = -4.5f;
    public float maxY = 4.5f;

    [Header("Status Timers")]
    private float invincibleTimer = 0f;
    private float deathBombTimer = 0f;

    public bool IsInvincible => invincibleTimer > 0;
    public bool IsInDeathBombWindow => deathBombTimer > 0;

    public static PlayerMove Instance { get; private set; }

    void Awake()
    {
        Time.timeScale = 1f;
        //Application.targetFrameRate = 60;
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // スプライトの取得をより確実に
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        // --- デバッグ用操作 ---
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartRecording();
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            StartPlayback(this.replayData);
        }
        if (currentMode == ReplayMode.Playing) return; // 再生中は入力を受け付けない

        inputVec.x = Input.GetAxisRaw("Horizontal");
        inputVec.y = Input.GetAxisRaw("Vertical");

        // タイマー更新は共通
        if (invincibleTimer > 0) invincibleTimer -= Time.deltaTime;
        if (deathBombTimer > 0) deathBombTimer -= Time.deltaTime;
    }

    // 【重要】LateUpdate は Animator の処理が終わった後に呼ばれる
    // ここで色を塗ることで、Animatorによるリセットを上書きできる
    void LateUpdate()
    {
        if (IsInvincible)
        {
            UpdateInvincibleVisual();
        }
        else
        {
            // 無敵が終わった瞬間に一度だけ色を戻すための判定
            if (sr != null && sr.color != Color.white)
            {
                ResetVisual();
            }
        }
    }
    // 録画開始メソッド
    public void StartRecording()
    {
        replayData.Clear();
        currentFrame = 0;
        currentMode = ReplayMode.Recording;
        Debug.Log("<color=red>● リプレイ録画開始</color>");
    }
    void FixedUpdate()
    {
        ReplayFrame frame;

        if (currentMode == ReplayMode.Playing)
        {
            // --- 再生モード：リストから入力を読み出す ---
            if (currentFrame < replayData.Count)
            {
                frame = replayData[currentFrame];
                inputVec = new Vector2(frame.h, frame.v);
                currentFrame++;
            }
            else
            {
                frame = new ReplayFrame(); // データ終了
            }
        }
        else
        {
            // --- 通常・記録モード：現在の入力を取得 ---
            frame = new ReplayFrame
            {
                h = inputVec.x,
                v = inputVec.y,
                slow = Input.GetKey(KeyCode.LeftShift),
                bomb = Input.GetKeyDown(KeyCode.X) // ボムキーの例
            };

            if (currentMode == ReplayMode.Recording)
            {
                replayData.Add(frame);
            }
        }

        // 移動処理（入力元がキーかリプレイデータかに関わらず同じ計算を通す）
        float speed = frame.slow ? lowSpeed : highSpeed;
        Vector2 velocity = inputVec.normalized * speed;
        Vector2 nextPosition = rb.position + velocity * Time.fixedDeltaTime;

        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);
        rb.MovePosition(nextPosition);
    }

    // リプレイ開始時の初期化用
    public void StartPlayback(List<ReplayFrame> data)
    {
        replayData = data;
        currentFrame = 0;
        currentMode = ReplayMode.Playing;
    }

    public void SetInvincible(float duration)
    {
        invincibleTimer = duration;
        deathBombTimer = 0f;
    }

    public void StartDeathBombWindow(float duration)
    {
        if (!IsInvincible) deathBombTimer = duration;
    }

    private void UpdateInvincibleVisual()
    {
        if (sr == null) return;
        float pingPong = Mathf.PingPong(Time.time * 20f, 1f);
        float alpha = 0.3f + pingPong * 0.7f;
        // 青い点滅色を適用
        sr.color = Color.Lerp(new Color(0.4f, 0.4f, 1f, alpha), new Color(1f, 1f, 1f, alpha), pingPong);
    }

    private void ResetVisual()
    {
        if (sr == null) return;
        sr.color = Color.white;
    }
}