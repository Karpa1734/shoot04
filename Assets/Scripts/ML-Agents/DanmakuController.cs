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
        // 👑【新規追加：傲慢領域による高速・低速移動の完全反転ハッキング】
        // =========================================================================
        // 💡 目的：シフトキーホールド状態（input.slow）の論理フラグを内部で一時的にひっくり返します。
        bool isSlowMovementMode = input.slow;

        if (shell.Opponent != null)
        {
            // 対戦相手のステータスマネージャーをスキャン
            PlayerStatusManager oppStatus = shell.Opponent.GetComponent<PlayerStatusManager>();
            if (oppStatus != null && oppStatus.isSpellCardActive && oppStatus.characterData != null)
            {
                // 相手が現在「傲慢（PrideInversion）」の領域を展開している場合
                if (oppStatus.characterData.vjtEffectType == VJTEffectType.PrideInversion)
                {
                    // 🚨 例外救済盾：自分が「虚無パッシブ（NihilityFieldCancel）」を持っているなら、この反転デバフを完全シャットアウト！
                    if (hitHandler != null && hitHandler.GetComponentInParent<PlayerStatusManager>() != null &&
                        !hitHandler.GetComponentInParent<PlayerStatusManager>().HasPassiveSkill(PassiveSkillType.NihilityFieldCancel))
                    {
                        // 💡 核心：シフトキーを押していれば「false(高速速度)」、離していれば「true(低速速度)」へと論理反転！
                        isSlowMovementMode = !input.slow;
                    }
                }
            }
        }

        // =========================================================================
        // 🎯【敏捷ランク結合】：算出された速度（isSlowMovementMode）をダイレクトスキャン
        // =========================================================================
        // 💡 反転状態が適用されたフラグに基づいて、通常速度（normalSpeed）か低速クランプ（focusSpeed）かを選択
        float baseSpeed = isSlowMovementMode ? shell.focusSpeed : shell.normalSpeed;
        float finalSpeed = baseSpeed * shell.skillSpeedMultiplier;

        // 🌟【重力ブレンドアルゴリズム】：自身の純粋な移動入力ベクトル
        Vector2 moveVelocity = inputVec.normalized * finalSpeed;

        // 🍰【暴食の引力合算】：引力ベクトルとのクリーンな足し合わせ
        Vector2 finalCompositeVelocity = moveVelocity + shell.externalPullVelocity;

        // 4. 次の座標を計算
        Vector2 nextPosition = rb.position + finalCompositeVelocity * Time.fixedDeltaTime;

        // 5. 座標を更新する「前」にクランプ（画面外ハミ出し防止）
        nextPosition.x = Mathf.Clamp(nextPosition.x, minX, maxX);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minY, maxY);

        // 6. 物理的に正しい位置へ移動
        rb.MovePosition(nextPosition);

        // このフレームでかかった外部引力をクリアして、次のフレームの注入に備える
        shell.externalPullVelocity = Vector2.zero;
    }
}