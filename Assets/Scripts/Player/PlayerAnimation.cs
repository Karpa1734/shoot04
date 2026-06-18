// --- PlayerAnimation.cs 自機スプライト色完全同調・最適化版 ---
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [Header("🎯 2Dグラフィックコンポーネント")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("🔧 依存コンポーネント（空欄でも自動取得します）")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerStatusManager statusManager;

    [HideInInspector] public bool isInvincible = false;

    private float _blinkTimer = 0f;
    private Color _baseCharacterColor = Color.white;

    void Start()
    {
        // 同じGameObject、または親からコンポーネントをストレートに取得
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (playerMove == null) playerMove = GetComponent<PlayerMove>();
        if (playerMove == null) playerMove = GetComponentInParent<PlayerMove>();

        if (statusManager == null && playerMove != null) statusManager = playerMove.GetComponent<PlayerStatusManager>();
        if (statusManager == null) statusManager = GetComponent<PlayerStatusManager>();
        if (statusManager == null) statusManager = GetComponentInParent<PlayerStatusManager>();

        UpdateBaseCharacterColor();
    }

    void LateUpdate()
    {
        if (Time.timeScale <= 0 || spriteRenderer == null) return;

        // 保険のスキャン（コンポーネントが抜けていた場合）
        if (playerMove == null || statusManager == null)
        {
            if (playerMove == null) playerMove = GetComponent<PlayerMove>();
            if (statusManager == null && playerMove != null) statusManager = playerMove.GetComponent<PlayerStatusManager>();
        }

        // 📐 1. 【左右向き切り替え：ターゲット追従】
        if (playerMove != null && playerMove.Opponent != null)
        {
            Vector3 myPos = transform.position;
            Vector3 oppPos = playerMove.Opponent.transform.position;

            if (oppPos.x > myPos.x)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, -90f); // 右向き
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, 0f, 90f);  // 左向き
            }
        }

        // =========================================================================
        // 🎨 2. 【Managerと自機スプライトの色を無条件完全同期 ＆ 無敵点滅】
        // =========================================================================
        UpdateBaseCharacterColor();

        bool isCurrentlyInvincible = isInvincible ||
                                     (playerMove != null && playerMove.IsInvincible) ||
                                     (statusManager != null && statusManager.IsInvincible);

        if (isCurrentlyInvincible)
        {
            // 無敵時間中は同期した固有色（RGB）を維持したまま、透明度（Alpha）だけを高速ループ点滅
            _blinkTimer += Time.deltaTime * 25f;
            float alphaBlink = Mathf.PingPong(_blinkTimer, 1f);
            spriteRenderer.color = new Color(_baseCharacterColor.r, _baseCharacterColor.g, _baseCharacterColor.b, alphaBlink);
        }
        else
        {
            _blinkTimer = 0f;
            // 通常時は、確定した固有カラー（不透明度100%）をスプライトへ強制上書き適用
            _baseCharacterColor.a = 1.0f;
            spriteRenderer.color = _baseCharacterColor;
        }
    }

    /// <summary>
    /// 白フィルターを完全パージし、Manager内の確定カラーをダイレクトに吸い出す
    /// </summary>
    private void UpdateBaseCharacterColor()
    {
        // ① [最優先] 既に綺麗に着色されているプレイヤー名テキストの色を無条件同期
        if (statusManager != null && statusManager.characterNameText != null)
        {
            _baseCharacterColor = statusManager.characterNameText.color;
            return;
        }

        // ② もしテキストが準備中なら、Dataアセットのカラーを同期
        if (statusManager != null && statusManager.characterData != null)
        {
            _baseCharacterColor = statusManager.characterData.imageColor;
            return;
        }

        // ③ [最終フォールバック] どちらも取得ラグで抜けている場合は playerId から1P(黄)・2P(赤)を割り当て
        if (playerMove != null)
        {
            if (playerMove.playerId == 1)
                _baseCharacterColor = new Color(1.0f, 0.85f, 0.0f, 1.0f); // 🟡 1P: 黄色
            else
                _baseCharacterColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);   // 🔴 2P: 赤色
        }
    }
}