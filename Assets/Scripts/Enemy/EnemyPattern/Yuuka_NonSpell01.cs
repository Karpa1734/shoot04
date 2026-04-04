using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Yuuka_NonSpell01 : BossPatternBase
{
    [Header("Polygon Settings")]
    public int edges = 3;
    public float baseSpeed = 3.0f;
    public int bulletCount = 30;
    public float fireInterval = 1.5f;
    float angle = 0;


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
            // 1回目の多角形発射
            SEManager.Instance.Play(SEPath.SHOT1, 0.5f);
            // transform.position を渡すことで、移動中の現在地から発射される
            //CreatePolygonShot(BLUE[0], transform.position, edges, bulletCount, baseSpeed, angle, 5);
            CreateWideShot(BLUE[0], transform.position, 3.5f, angle,10,3, 10);
            yield return new WaitForSeconds(0.2f);
            /*
            // 2回目の多角形発射（180度反転）
            SEManager.Instance.Play(SEPath.SHOT1, 0.5f);
            CreatePolygonShot(WHITE[0], transform.position, edges, bulletCount, baseSpeed - 0.5f, angle + 180f, 5);

            yield return new WaitForSeconds(fireInterval);
            */
            angle += 15;
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
        IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            if (PlayerMove.Instance == null) yield break;

            // 自機への角度計算
            Vector2 dir = PlayerMove.Instance.transform.position - transform.position;
            float angleToPlayer = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            int laserCount = 12;
            float initialRotSpeed = 5.0f;
            int stopFrame = 40;
            int warningFrame = 80;
            float radius = 1.0f;

            // ★滑らかに止まるための逆算：200f (定速分) + 約45f (減速分) = 245f
            float estimatedRotation = 245f;
            float baseAngle = angleToPlayer - estimatedRotation;

            for (int i = 0; i < laserCount; i++)
            {
                CreateLaserB(i, 120.0f, 1.2f, BulletManager.LaserColor.RED, warningFrame);

                float currentStartAngle = baseAngle + (360f / laserCount * i);

                // 第10引数に true を渡して「滑らかに止まる」モードを有効化
                SetLaserDataB(i, 0, 0f, radius, 0f, currentStartAngle, initialRotSpeed, currentStartAngle, initialRotSpeed, false, true);

                // 停止時：速度0を目標にする（ここでも true を渡す）
                SetLaserDataB(i, stopFrame, -999f, -999f, -999f, -999f, 0f, -999f, 0f, false, true);

                // 消滅
                SetLaserDataB(i, 240, 0f, -999f, -999f, -999f, -999f, -999f, -999f, true);

                FireShot(i);
            }

            yield return new WaitForSeconds(warningFrame / 60f);
            yield return new WaitForSeconds(6.0f - (warningFrame / 60f));
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