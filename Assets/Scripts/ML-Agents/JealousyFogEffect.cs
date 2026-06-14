// --- JealousyFogEffect.cs 【嫉妬の目隠し霧・名前空間エラー修正完全版】 ---
using UnityEngine;

public class JealousyFogEffect : MonoBehaviour
{
    [Header("Fog Animation Settings")]
    [Tooltip("黒い霧のアニメーションスプライト4枚を順番に登録してください")]
    public Sprite[] fogSprites;

    [Tooltip("1コマあたりの表示時間（秒）")]
    public float frameDuration = 0.1f;

    private SpriteRenderer _spriteRenderer;
    private int _currentFrame = 0;
    private float _timer = 0f;

    void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null) _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        // 💡 相手の弾幕や自機の手前に覆い被さるよう、レイヤー順（SortingOrder）を最前面(例: 100)に強制ロック
        _spriteRenderer.sortingOrder = 20010;

        // 霧が出現するたびにランダムで反転・角度をばらつかせて見た目のバリエーションを作ります
        _spriteRenderer.flipX = Random.value > 0.5f;
        _spriteRenderer.flipY = Random.value > 0.5f;
        transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        if (fogSprites != null && fogSprites.Length > 0)
        {
            _spriteRenderer.sprite = fogSprites[0];
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 黒い霧の画像（fogSprites）が1枚もアタッチされていません。");
            Destroy(gameObject);
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= frameDuration)
        {
            _timer -= frameDuration;
            _currentFrame++;

            // 1枚目〜3枚目まではスプライト画像を順番に切り替え
            if (_currentFrame < fogSprites.Length)
            {
                _spriteRenderer.sprite = fogSprites[_currentFrame];
            }

            // 4コマ目（インデックス3）、またはアニメーションが最後まで到達したらフェードアウトしながら消滅
            if (_currentFrame >= 3)
            {
                // 🌟【修正箇所】：正しい IEnumerator の名前空間でコルーチンをキック
                StartCoroutine(FadeOutAndDestroy());
                enabled = false; // 重複処理防止のためUpdateを即座にフリーズ
            }
        }
    }

    // 🌟【大修正】：名前空間から不要な '.Clone' を完全に除去し、正しい型に修復しました
    private System.Collections.IEnumerator FadeOutAndDestroy()
    {
        float elapsed = 0f;
        float fadeDuration = frameDuration; // 最後の1コマ分の時間をかけて消える
        Color startColor = _spriteRenderer.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            }
            yield return null;
        }

        Destroy(gameObject); // メモリから安全に完全パージ
    }
}