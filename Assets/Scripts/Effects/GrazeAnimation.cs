using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GrazeAnimation : MonoBehaviour
{
    [Header("アニメーション設定")]
    [SerializeField, Tooltip("アニメーションさせるSpriteのアレイ（9枚をここにドラッグ＆ドロップ）")]
    private Sprite[] sprites;

    [SerializeField, Tooltip("1秒間に何枚のフレームを進めるか (FPS)")]
    private float frameRate = 12f;

    [SerializeField, Tooltip("アニメーションをループさせるかどうか")]
    private bool loop = false;

    private SpriteRenderer spriteRenderer;
    private int currentFrame = 0;
    private float timer = 0f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        // アニメーションを最初から再生
        currentFrame = 0;
        timer = 0f;
        UpdateSprite();
    }

    private void Update()
    {
        if (sprites == null || sprites.Length == 0 || frameRate <= 0) return;

        timer += Time.deltaTime;

        if (timer >= (1f / frameRate))
        {
            timer = 0f;
            currentFrame++;

            if (currentFrame >= sprites.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    // ★ 修正：アニメーション再生が終わったら即座に消去する
                    Destroy(gameObject);
                    return;
                }
            }
            UpdateSprite();
        }
    }

    private void UpdateSprite()
    {
        // 追加：sprites が未設定、または要素がない場合のチェック
        if (sprites == null || sprites.Length == 0) return;

        if (currentFrame >= 0 && currentFrame < sprites.Length)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprites[currentFrame];
            }
        }
    }
}