using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

/// <summary>
/// シャル 第1スペルカード：欲符「ゴールド・ディガー」
/// 画面上部をゆらゆら往復移動しながら、全方位スピン弾と自機狙いN-Wayを交互に放つ複合弾幕
/// </summary>
public class Spell_Charlotte_01 : SpellCardPattern
{
    [Header("弾データアセット")]
    public BulletData mainBulletData;   // 全方位用弾幕アセット
    public BulletData aimedBulletData;  // 自機狙い用弾幕アセット

    [Header("移動・行動パラメーター")]
    public Vector3 targetTopPosition = new Vector3(0f, 2.8f, 0f); // 初期目標位置
    public float moveSpeed = 5.0f;

    private BossDanmakuExecutor _bossExecutor;

    public override void Initialize(PlayerStatusManager status)
    {
        base.Initialize(status);
        _bossExecutor = status.GetComponent<BossDanmakuExecutor>();

        // もし aimedBulletData が未設定なら fallback として mainBulletData を自動代入
        if (aimedBulletData == null)
        {
            aimedBulletData = mainBulletData;
        }
    }

    public override IEnumerator ExecutePatternRoutine()
    {
        // ---------------------------------------------------------------------
        // STEP 1. 初期移動（指定された画面上部の中央定位置へ移動）
        // ---------------------------------------------------------------------
        if (bossMove != null)
        {
            while (Vector3.Distance(bossMove.transform.position, targetTopPosition) > 0.1f)
            {
                bossMove.transform.position = Vector3.MoveTowards(
                    bossMove.transform.position,
                    targetTopPosition,
                    moveSpeed * Time.deltaTime
                );
                yield return null;
            }
            bossMove.transform.position = targetTopPosition;
        }

        // 開幕溜め演出（少し間を置く）
        yield return new WaitForSeconds(0.4f);

        // ---------------------------------------------------------------------
        // STEP 2. スペルカードメインループ（撃破されるまで継続）
        // ---------------------------------------------------------------------
        float timer = 0f;
        float baseAngle = 0f;
        int attackCycle = 0;

        while (true)
        {
            // 被弾等により行動不能な場合はループを安全待機
            PlayerHitHandler myHH = bossStatus != null ? bossStatus.GetComponentInChildren<PlayerHitHandler>() : null;
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
            {
                yield return null;
                continue;
            }

            timer += Time.deltaTime;

            // 💡 A. 左右に優雅にゆらゆら動く（サイン波移動）
            if (bossMove != null)
            {
                float currentX = Mathf.Sin(timer * 1.5f) * 2.5f;
                bossMove.transform.position = new Vector3(currentX, targetTopPosition.y, 0f);
            }

            // 💡 B. 攻撃フェーズの分岐（全方位旋回 ➔ 自機狙いN-Way）
            if (_bossExecutor != null && mainBulletData != null)
            {
                if (attackCycle % 3 == 0)
                {
                    // 🎯 周期A: 角度を少しずつ旋回させながら放つ20方向の全方位弾幕
                    if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT1, 0.2f);

                    _bossExecutor.FireRoundShot(
                        data: mainBulletData,
                        pos: bossMove.transform.position,
                        count: 20,
                        speed: 4.2f,
                        startAngle: baseAngle,
                        delay: 0f
                    );

                    baseAngle += 9f; // 次の波で回転させる
                }
                else
                {
                    // 🎯 周期B: 1P（プレイヤー自機）へ向けて放つ5-Way高精度狙撃弾
                    if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT2, 0.25f);

                    _bossExecutor.FireAimedNWayShot(
                        data: aimedBulletData,
                        pos: bossMove.transform.position,
                        way: 5,
                        speed: 5.5f,
                        wideAngle: 35f,
                        angleOffset: 0f,
                        delay: 0f
                    );
                }
            }

            attackCycle++;

            // 次の発射まで 0.45 秒待機
            yield return new WaitForSeconds(0.45f);
        }
    }

    public override void OnSpellEnd()
    {
        base.OnSpellEnd();
        // 必要に応じてスペルカード終了時のエフェクト等をここに記述
    }
}