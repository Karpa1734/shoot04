// --- PlayerAnimation.cs 【移動・Idle切り替え完全修正版】 ---
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [Header("🎯 2Dグラフィックコンポーネント")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [Header("🔧 依存コンポーネント（空欄でも自動取得します）")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerStatusManager statusManager;
    [SerializeField] private Rigidbody2D rb2d;

    // 🌟 エラー解消：外部から参照・変更できるように変数を定義
    [HideInInspector] public bool isInvincible = false;

    private float _blinkTimer = 0f;
    private Vector2 _lastPosition;

    void Awake()
    {
        // コンポーネントの初期取得
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (playerMove == null) playerMove = GetComponent<PlayerMove>();
        if (playerMove == null) playerMove = GetComponentInParent<PlayerMove>();
        if (statusManager == null && playerMove != null) statusManager = playerMove.GetComponent<PlayerStatusManager>();
        if (statusManager == null) statusManager = GetComponent<PlayerStatusManager>();
        if (statusManager == null) statusManager = GetComponentInParent<PlayerStatusManager>();

        // =========================================================================
        // 🌟【固有アニメーション自動アタッチ】
        // PlayerSkillData に登録された専用 Animator Controller を動的に適用する
        // =========================================================================
        if (statusManager != null && statusManager.characterData != null)
        {
            RuntimeAnimatorController charController = statusManager.characterData.characterAnimatorController;
            if (charController != null && animator != null)
            {
                animator.runtimeAnimatorController = charController;
                Debug.Log($"<color=lime>🎬 [PlayerAnimation] {statusManager.characterData.characterName} 専用のアニメーションコントローラーを正常にアタッチしました。</color>");
            }
        }
    }

    void Start()
    {
        if (rb2d == null) rb2d = GetComponent<Rigidbody2D>();
        _lastPosition = transform.position;
    }

    void Update()
    {
        if (Time.timeScale <= 0) return;

        // 📐 1. 【X/Yの移動速度を計算してAnimatorへ送信】
        UpdateAnimatorMovementParameters();
    }

    void LateUpdate()
    {
        if (Time.timeScale <= 0 || spriteRenderer == null) return;

        // 保険のスキャン
        if (playerMove == null || statusManager == null)
        {
            if (playerMove == null) playerMove = GetComponent<PlayerMove>();
            if (statusManager == null && playerMove != null) statusManager = playerMove.GetComponent<PlayerStatusManager>();
        }

        // 🔄 2. 【回転させず、SpriteRenderer.flipX で左右のみを敵位置に合わせて反転】
        if (playerMove != null && playerMove.Opponent != null)
        {
            Vector3 myPos = transform.position;
            Vector3 oppPos = playerMove.Opponent.transform.position;

            if (oppPos.x > myPos.x)
            {
                spriteRenderer.flipX = false; // 右向き
            }
            else
            {
                spriteRenderer.flipX = true;  // 左向き（反転）
            }
        }

        // =========================================================================
        // 🎨 3. 【無敵時の点滅表現 ＆ 通常時の色維持】
        // =========================================================================
        bool isCurrentlyInvincible = isInvincible ||
                                     (playerMove != null && playerMove.IsInvincible) ||
                                     (statusManager != null && statusManager.IsInvincible);

        if (isCurrentlyInvincible)
        {
            // 無敵時間中は透明度（Alpha）だけを高速ループ点滅させる
            _blinkTimer += Time.deltaTime * 25f;
            float alphaBlink = Mathf.PingPong(_blinkTimer, 1f);
            spriteRenderer.color = new Color(1f, 1f, 1f, alphaBlink);
        }
        else
        {
            _blinkTimer = 0f;
            // 通常時は完全に不透明な元の白に戻す
            spriteRenderer.color = Color.white;
        }
    }

    /// <summary>
    /// 🌟 被弾時に外部（PlayerHitHandler等）から呼び出して、専用アニメーションを即座に再生させる
    /// </summary>
    public void TriggerDamageAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("Damage");
        }
    }

    private void UpdateAnimatorMovementParameters()
    {
        if (animator == null) return;

        // 1. プレイヤーの入力値（リプレイ入力またはキーボード等の直接入力）を取得
        float hInput = 0f;
        float vInput = 0f;

        if (playerMove != null)
        {
            hInput = playerMove.currentFrameInput.h;
            vInput = playerMove.currentFrameInput.v;
        }

        // もしPlayerMove側が未取得の場合は通常のInputをフォールバックとして使用
        if (Mathf.Approximately(hInput, 0f) && Mathf.Approximately(vInput, 0f))
        {
            hInput = Input.GetAxisRaw("Horizontal");
            vInput = Input.GetAxisRaw("Vertical");
        }

        // 🌟 ごく小さな入力をカットするデッドゾーン処理（スティックのわずかな傾きによる誤作動防止）
        if (Mathf.Abs(hInput) < 0.1f) hInput = 0f;
        if (Mathf.Abs(vInput) < 0.1f) vInput = 0f;

        // 2. 左右反転している場合は、Animatorに送るXの数値を反転させる
        float finalXSpeed = hInput;
        if (spriteRenderer != null && spriteRenderer.flipX)
        {
            finalXSpeed = -finalXSpeed;
        }

        // Animatorへ数値（XSpeed / YSpeed）を送信
        animator.SetFloat("XSpeed", finalXSpeed);
        animator.SetFloat("YSpeed", vInput);

        // =========================================================================
        // 🌟【完全入力判定】：速度ではなく「キーが押されているかどうか」だけでBoolを決定
        // =========================================================================
        bool isInputting = (hInput != 0f) || (vInput != 0f);

        // Animatorの bool パラメータへ反映
        animator.SetBool("IsMoving", isInputting);
    }

    /// <summary>
    /// 🌟 スキル使用時に外部（PlayerDanmakuEmitter等）から呼び出して、対応するスキルモーションを再生させる
    /// </summary>
    public void TriggerSkillAnimation(string skillName)
    {
        if (animator == null || statusManager == null || statusManager.characterData == null) return;

        var data = statusManager.characterData;

        // スタック防止のため、すべてのスキル・EX関連のトリガーを一度リセット
        animator.ResetTrigger("SkillZ");
        animator.ResetTrigger("SkillZ2");
        animator.ResetTrigger("SkillX");
        animator.ResetTrigger("SkillX2");
        animator.ResetTrigger("SkillC");
        animator.ResetTrigger("SkillV");
        animator.ResetTrigger("SkillV2");
        animator.ResetTrigger("SkillEX");
        animator.ResetTrigger("SkillEX2");

        // どのスキルボタンが押されたかを判定して、対応するAnimatorのトリガーを引く
        if (skillName == data.skillZ.skillName)
        {
            animator.SetTrigger("SkillZ");
        }
        else if (skillName == data.skillX.skillName)
        {
            animator.SetTrigger("SkillX");
        }
        else if (skillName == data.skillC.skillName)
        {
            animator.SetTrigger("SkillC");
        }
        else if (skillName == data.skillV.skillName)
        {
            animator.SetTrigger("SkillV");
        }
    }

    public void TriggerEXSkillAnimation()
    {
        if (animator != null)
        {
            animator.ResetTrigger("SkillEX");
            animator.ResetTrigger("SkillEX2");
            animator.SetTrigger("SkillEX");
        }
    }
}