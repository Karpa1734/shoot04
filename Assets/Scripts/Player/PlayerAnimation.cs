using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] allSprites; // 0-7:正面, 8-15:左, 16-23:右

    private PlayerMove playerMove;
    private int frameCount = 0;
    private int invincibilityFrame = 0;

    public bool isInvincible = false;

    void Start()
    {
        playerMove = GetComponentInParent<PlayerMove>();
    }

    void Update()
    {
        if (Time.timeScale <= 0 || playerMove == null) return;

        // --- 1. PlayerMoveの入力データから向きを決定 ---
        int rowOffset = 0;
        float h = playerMove.currentFrameInput.h;

        if (h < 0) // 左入力
        {
            rowOffset = 8;
            HandleHoldFrame();
        }
        else if (h > 0) // 右入力
        {
            rowOffset = 16;
            HandleHoldFrame();
        }
        else
        {
            rowOffset = 0; // 停止
        }

        // --- 2. スプライトの更新 ---
        int spriteIndex = rowOffset + (frameCount / 5);
        if (spriteIndex < allSprites.Length)
        {
            spriteRenderer.sprite = allSprites[spriteIndex];
        }

        frameCount++;
        if (frameCount >= 5 * 8) frameCount = 0;

        UpdateInvincibleEffect();
    }

    void HandleHoldFrame()
    {
        // 簡易化：一定以上のフレームならループさせる
        if (frameCount > 35) frameCount = 20;
    }

    void UpdateInvincibleEffect()
    {
        invincibilityFrame++;
        if (isInvincible && invincibilityFrame % 3 == 2)
        {
            spriteRenderer.color = new Color(0, 0, 1, 1);
        }
        else
        {
            spriteRenderer.color = Color.white;
        }
    }
}