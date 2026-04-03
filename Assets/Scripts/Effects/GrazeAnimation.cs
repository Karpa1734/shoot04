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
        // Spriteが設定されていない、またはframeRateが0以下の場合は何もしない
        if (sprites == null || sprites.Length == 0 || frameRate <= 0) return;

        timer += Time.deltaTime;

        // 次のフレームに進む時間かチェック
        if (timer >= (1f / frameRate))
        {
            timer = 0f;
            currentFrame++;

            // 最後のフレームを超えた場合の処理
            if (currentFrame >= sprites.Length)
            {
                if (loop)
                {
                    currentFrame = 0; // 最初に戻る（ループ）
                }
                else
                {
                    // ループしない場合は、自分自身を非アクティブにするか、破棄する
                    currentFrame = sprites.Length - 1; // 最後のフレームで止める
                    // gameObject.SetActive(false); // グレイズエフェクトなら再生後に消すのが一般的
                    // Destroy(gameObject); // 破棄する場合
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