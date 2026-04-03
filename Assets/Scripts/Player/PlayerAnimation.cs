using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] allSprites; // 0-7:正面, 8-15:左, 16-23:右

    private int frameCount = 0;
    private int invincibilityFrame = 0;

    // 他のスクリプト（被弾処理など）から操作するための変数
    public bool isInvincible = false;

    void Update()
    {
        if (Time.timeScale <= 0) return;
        // --- 1. 入力状態と行(RectY)の決定 ---
        int rowOffset = 0;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            rowOffset = 8; // 左移動
            frameCount = 5; // 押し始めは 1枚飛ばす演出
        }
        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            rowOffset = 8;
            HandleHoldFrame(); // 押し続けている間のフレームジャンプ処理
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            rowOffset = 16; // 右移動
            frameCount = 5;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            rowOffset = 16;
            HandleHoldFrame();
        }
        else
        {
            rowOffset = 0; // 停止時
        }

        // --- 2. スプライトの更新 ---
        // 5フレームごとに 1枚進む (floor(FrameCount/5))
        int spriteIndex = rowOffset + (frameCount / 5);
        if (spriteIndex < allSprites.Length)
        {
            spriteRenderer.sprite = allSprites[spriteIndex];
        }

        // フレームカウントの更新
        frameCount++;
        if (frameCount >= 5 * 8) frameCount = 0;

        // --- 3. 無敵時の青色点滅演出 ---
        UpdateInvincibleEffect();
    }

    // KEY_HOLD 時の特殊なフレームジャンプ処理を再現
    void HandleHoldFrame()
    {
        if (frameCount == 0) frameCount = 20;
        else if (frameCount == 6) frameCount = 10;
        else if (frameCount == 11) frameCount = 15;
        else if (frameCount == 16) frameCount = 20;
    }

    // 無敵状態の時、3フレームに1回青くなる演出
    void UpdateInvincibleEffect()
    {
        invincibilityFrame++;
        if (isInvincible && invincibilityFrame % 3 == 2)
        {
            spriteRenderer.color = new Color(0, 0, 1, 1); // 青色
        }
        else
        {
            spriteRenderer.color = Color.white; // 通常色
        }
    }
}