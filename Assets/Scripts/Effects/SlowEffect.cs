using UnityEngine;

public class SlowEffect : MonoBehaviour
{
    [Header("Settings")]
    public SpriteRenderer spriteRenderer;
    public float rotationSpeed = 3.0f; // 元コードの Zangle += 3 相当
    public float maxAlpha = 1.0f;
    public float fadeSpeed = 5.0f;

    [Header("Scaling")]
    public float baseScale = 0.25f; // 元コードの Scale = 0.25 相当
    public bool isCounterClockwise = false; // 逆回転用フラグ

    private float currentAlpha = 0f;

    void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        // 最初は非表示
        spriteRenderer.color = new Color(1, 1, 1, 0);
        transform.localScale = new Vector3(baseScale, baseScale, 1);
    }

    void Update()
    {
        if (Time.timeScale <= 0) return;
        // Shiftキー（低速移動）が押されているか判定
        bool isSlow = Input.GetKey(KeyCode.LeftShift);

        // アルファ値のフェード処理
        currentAlpha = Mathf.MoveTowards(currentAlpha, isSlow ? maxAlpha : 0f, fadeSpeed * Time.deltaTime);
        spriteRenderer.color = new Color(1, 1, 1, currentAlpha);

        if (currentAlpha > 0)
        {
            // 回転処理
            float dir = isCounterClockwise ? 1f : -1f;
            transform.Rotate(0, 0, rotationSpeed * dir);

            // 演出としてわずかにスケールを拍動させるとより再現度が高まります
            // 元コードの Scale += 0.0125 等の複雑な動きの簡易再現
            float pulse = isSlow ? Mathf.Sin(Time.time * 10f) * 0.02f : 0f;
            transform.localScale = new Vector3(baseScale , baseScale, 1);
        }
    }
}