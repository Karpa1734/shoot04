// --- SlowEffect.cs 【パッシブ0.8倍 × 色欲領域1.5倍・色干渉完全パージ版】 ---
using UnityEngine;

public class SlowEffect : MonoBehaviour
{
    [Header("Settings")]
    public SpriteRenderer spriteRenderer;
    public float rotationSpeed = 3.0f;
    public float maxAlpha = 1.0f;
    public float fadeSpeed = 5.0f;

    [Header("Scaling")]
    public float baseScale = 0.25f;
    public bool isCounterClockwise = false;

    private float currentAlpha = 0f;
    private PlayerMove cachedPlayerMove;

    void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        spriteRenderer.color = new Color(1, 1, 1, 0);
        transform.localScale = new Vector3(baseScale, baseScale, 1);

        cachedPlayerMove = GetComponentInParent<PlayerMove>();

        if (cachedPlayerMove == null)
        {
            Debug.LogWarning($"[{gameObject.name}] SlowEffectの親階層に PlayerMove が見つかりませんでした。");
        }
    }

    void Update()
    {
        if (Time.timeScale <= 0) return;

        // 🌟 最新の低速移動（フォーカス）入力をデコード
        bool isSlow = false;
        if (cachedPlayerMove != null)
        {
            isSlow = cachedPlayerMove.currentFrameInput.slow;
        }

        // =========================================================================
        // 🛡️🔮【パッシブ縮小（0.8倍） ＆ 色欲領域デバフ（1.5倍）の自己自律型・掛け算インフラ】
        // =========================================================================
        float finalTargetScale = baseScale;
        float sizeUpMultiplier = 1.0f;

        if (cachedPlayerMove != null)
        {
            PlayerStatusManager myStatus = cachedPlayerMove.GetComponent<PlayerStatusManager>();

            // 🛡️ A. [パッシブチェック]：自身が SmallHitbox を持っていたら、ベーススケールを0.8倍に変調
            if (myStatus != null && myStatus.HasPassiveSkill(PassiveSkillType.LustSmall))
            {
                finalTargetScale = baseScale * 0.8f;
            }

            // 🔮 B. [領域デバフチェック]：対戦相手が色欲（SizeUp）を展開しているかリアルタイム逆算感知
            if (cachedPlayerMove.Opponent != null)
            {
                PlayerStatusManager oppStatus = cachedPlayerMove.Opponent.GetComponent<PlayerStatusManager>();
                if (oppStatus != null && oppStatus.isSpellCardActive && oppStatus.characterData != null && oppStatus.characterData.vjtEffectType == VJTEffectType.LustHit)
                {
                    // 相手のScriptableObjectにアタッチされている巨大化倍率（1.5倍など）をダイレクトに吸い上げる
                    sizeUpMultiplier = 1.5f;
                }
            }
        }

        // 💡 通常通り「低速移動ボタンを押している時（isSlow == true）」のみ魔法陣が表示されるフェード処理
        float targetAlpha = isSlow ? maxAlpha : 0f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = currentAlpha;
            spriteRenderer.color = c;
        }

        // 魔法陣が表示されている時（低速時）のみ、回転と精密なスケール計算を反映
        if (currentAlpha > 0)
        {
            float dir = isCounterClockwise ? 1f : -1f;
            transform.Rotate(0, 0, rotationSpeed * dir);

            // 🛡️パッシブの縮小（等倍 or 0.8）に、🔮色欲デバフの拡大（等倍 or 1.5）を数学的に美しく結合乗算！
            // 例：パッシブで0.8倍、色欲で1.5倍なら、0.8 * 1.5 = 通常時の「1.2倍」の魔法陣がジャストサイズで実体化します。
            float dynamicBaseScale = finalTargetScale * sizeUpMultiplier;

            // 魔法陣のドクンドクンという鼓動（拍動アニメーション）の振幅幅も、巨大化スケールに綺麗に追従させます
            float pulse = Mathf.Sin(Time.time * 10f) * (0.02f * sizeUpMultiplier);

            transform.localScale = new Vector3(dynamicBaseScale + pulse, dynamicBaseScale + pulse, 1);
        }
    }
}