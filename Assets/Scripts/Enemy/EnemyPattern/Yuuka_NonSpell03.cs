using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Yuuka_NonSpell03 : BossPatternBase
{
    [Header("Polygon Settings")]
    public int edges = 3;
    public float baseSpeed = 3.0f;
    public int bulletCount = 30;
    public float fireInterval = 1.5f;
    float angle = 0;
    float interval;
    float intervalmin = 0.015f;

    private Coroutine mainAttackRoutine;
    private Coroutine moveRoutine;

    private bool isMoving = false;
    void OnEnable()
    {
        // 既存のルーチンがあれば停止して重複を防ぐ
        if (mainAttackRoutine != null) StopCoroutine(mainAttackRoutine);
        if (moveRoutine != null) StopCoroutine(moveRoutine);

        // 攻撃と移動、それぞれ独立したコルーチンとして開始
        mainAttackRoutine = StartCoroutine(BitAttackRoutine());
        moveRoutine = StartCoroutine(MoveRoutine());
    }

    IEnumerator BitAttackRoutine()
    {
        // ビットを使用する場合はここで生成（現在はコメントアウトの状態を維持）
        // CreateOrbitBits(5, 3.0f, 2.0f, 40.0f, bitPrefab);
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            interval = fireInterval;
            while (interval >= intervalmin) {
                // 1回目の多角形発射
                SEManager.Instance.Play(SEPath.SHOT1, 0.5f);
                // transform.position を渡すことで、移動中の現在地から発射される
                CreatePolygonShot(BLUE[0], transform.position, edges, bulletCount, baseSpeed, angle, 5);

                yield return new WaitForSeconds(interval);
                interval -= 0.02f;
                angle += 13;
            }

            interval = fireInterval;
            while (interval >= intervalmin)
            {
                // 2回目の多角形発射（180度反転）
                SEManager.Instance.Play(SEPath.SHOT1, 0.5f);
                CreatePolygonShot(WHITE[0], transform.position, edges, bulletCount, baseSpeed - 0.5f, angle + 180f, 5);

                yield return new WaitForSeconds(interval);
                interval -= 0.02f;
                angle -= 13;
            }
            angle += 23;
        }
    }

    // --- 修正版：移動専用のループルーチン ---
    IEnumerator MoveRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        // 攻撃が続いている間、無限に移動を繰り返す
        while (true)
        {
            isMoving = true;

            // ランダムな座標へ移動開始
            yield return StartCoroutine(SetMovePositionRand03(
                moveMinX, moveMaxX,
                moveMinY, moveMaxY,
                moveWeight
            ));

            isMoving = false;

            // 移動完了後に少し休憩（これがないと休みなく動き続けます）
            yield return new WaitForSeconds(moveInterval);
        }
    }

    // 段階移行時などのクリーンアップ
    protected override void OnDisable()
    {
        base.OnDisable(); // ビットの破棄などを実行
        if (mainAttackRoutine != null) StopCoroutine(mainAttackRoutine);
        if (moveRoutine != null) StopCoroutine(moveRoutine);
    }
}