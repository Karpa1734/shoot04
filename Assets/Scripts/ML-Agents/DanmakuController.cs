using System.Collections.Generic;
using UnityEngine;

// --- ロジック制御クラス：座標計算と移動の実行（Rigidbody2D） ---
public class DanmakuController : MonoBehaviour
{
    // 🛡️【パージ】：固定値の highSpeed, lowSpeed は評価ランクの不整合（バグ）の原因となるため破棄しました。

    [Header("Movement Bounds")]
    public float minX = -4.0f;
    public float maxX = 4.0f;
    public float minY = -4.5f;
    public float maxY = 4.5f;

    private Rigidbody2D rb;
    private PlayerMove shell;
    private PlayerHitHandler hitHandler;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        shell = GetComponent<PlayerMove>();
        hitHandler = GetComponentInChildren<PlayerHitHandler>();
    }

    void FixedUpdate()
    {
        if (shell == null) return;

        // 1. カウントダウン中や入力禁止時は停止
        if (!PlayerMove.CanInput)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 2. スタン中（Normal以外）は停止
        if (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 3. 入力と速度の計算
        var input = shell.currentFrameInput;
        Vector2 inputVec = new Vector2(input.h, input.v);

        // =========================================================================
        // 🎯【核心修正】：インスペクターの固定値をパージし、溶接されたランク速度へ直撃結合！
        // =========================================================================
        // 💡 シフト（低速移動）中であれば、shell側で「高速速度の0.4倍」に完全クランプされた focusSpeed を適用。
        // 💡 通常時であれば、敏捷ランクから算出された固有の normalSpeed をダイレクトスキャンします！
        float baseSpeed = input.slow ? shell.focusSpeed : shell.normalSpeed;

        // ★ 重要：PlayerMove のスキル倍率をここで掛け合わせる
        float finalSpeed = baseSpeed * shell.skillSpeedMultiplier;

        // 4. 次の座標を計算
        Vector2 velocity = inputVec.normalized * finalSpeed;
        Vector2 nextPosition = rb.position + velocity * Time.fixedDeltaTime;

        // 5. ★ 座標を更新する「前」にクランプする
        // これにより、画面外へ出ること自体を防ぎ、引き戻される挙動を解消します
        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);

        // 6. 物理的に正しい位置へ移動
        rb.MovePosition(nextPosition);
    }
}