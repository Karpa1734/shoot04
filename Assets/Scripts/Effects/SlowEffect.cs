// --- SlowEffect.cs 入力デバイス・AI完全連動修正版 ---
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

    // 🌟【追加】自分の機体のPlayerMoveを参照するためのキャッシュ用変数
    private PlayerMove cachedPlayerMove;

    void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        // 最初は非表示
        spriteRenderer.color = new Color(1, 1, 1, 0);
        transform.localScale = new Vector3(baseScale, baseScale, 1);

        // 🌟【追加】親オブジェクトの階層から、この機体をコントロールしている本物のPlayerMoveを取得
        cachedPlayerMove = GetComponentInParent<PlayerMove>();

        if (cachedPlayerMove == null)
        {
            Debug.LogWarning($"[{gameObject.name}] SlowEffectの親階層に PlayerMove が見つかりませんでした。");
        }
    }

    void Update()
    {
        if (Time.timeScale <= 0) return;

        // 🌟【完全同期修正】：PlayerMoveの構造体内の最新入力パケット（currentFrameInput.slow）を直接覗き込む！
        // これにより、キーボード、コントローラー（ゲームパッド）、AI自動回避のどれが低速になっても完全に100%連動します。
        bool isSlow = false;

        if (cachedPlayerMove != null)
        {
            isSlow = cachedPlayerMove.currentFrameInput.slow;
        }

        // アルファ値のフェード処理
        currentAlpha = Mathf.MoveTowards(currentAlpha, isSlow ? maxAlpha : 0f, fadeSpeed * Time.deltaTime);
        spriteRenderer.color = new Color(1, 1, 1, currentAlpha);

        if (currentAlpha > 0)
        {
            // 回転処理
            float dir = isCounterClockwise ? 1f : -1f;
            transform.Rotate(0, 0, rotationSpeed * dir);

            // 演出としてわずかにスケールを拍動させるとより再現度が高まります
            float pulse = isSlow ? Mathf.Sin(Time.time * 10f) * 0.02f : 0f;
            transform.localScale = new Vector3(baseScale + pulse, baseScale + pulse, 1);
        }
    }
}