using KanKikuchi.AudioManager;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class AimedNWayPattern : BossPatternBase
{
    [Header("Bullet Settings")]
    public BulletData bulletData;
    public BulletData bulletData2;
    public int nWay = 5;
    public float spreadAngle = 30f;
    public float bulletSpeed = 4f;
    public float fireInterval = 1.2f; // 間隔を少し短く

        // 最高速度を思い切って上げます。
    public float moveMaxSpeed = 12.0f;
    private float angle = 0;
    private bool isMoving = false;
    private Coroutine fireRoutine;
    private Coroutine mainAttackRoutine;
    private Coroutine moveRoutine;
    protected override void Awake()
    {
        base.Awake(); //
    }
    void OnEnable()
    {
        // 以前のルーチンが残っていれば停止
        if (mainAttackRoutine != null) StopCoroutine(mainAttackRoutine);
        if (moveRoutine != null) StopCoroutine(moveRoutine);

        // 攻撃と移動、それぞれ独立したコルーチンとして開始する
        mainAttackRoutine = StartCoroutine(AttackRoutine()); // ★StartCoroutineを使用
        //moveRoutine = StartCoroutine(ContinuousMoveRoutine());
    }
    /*
    IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            var list = new List<EnemyBullet.BulletTransformData>();

            float wigglePower = 2.5f;
            float interval = 0.6f;

            // ★修正：angle = -999f を指定して、角度の上書きを禁止する
            list.Add(new EnemyBullet.BulletTransformData
            {
                triggerTime = interval * 1,
                angVel = -wigglePower,
            });

            list.Add(new EnemyBullet.BulletTransformData
            {
                triggerTime = interval * 2,
                angVel = wigglePower,
            });

            list.Add(new EnemyBullet.BulletTransformData
            {
                triggerTime = interval * 3,
                angVel = -wigglePower,
            });
            list.Add(new EnemyBullet.BulletTransformData
            {
                triggerTime = interval * 4,
                angVel = wigglePower,
            });

            list.Add(new EnemyBullet.BulletTransformData
            {
                triggerTime = interval * 5,
                angVel = -wigglePower,
            });
            list.Add(new EnemyBullet.BulletTransformData
            {
                triggerTime = interval * 6,
                angle = -999f,    // 角度は変えない
                angVel =0,
            });

            float startAngle = 0;
            for (int i = 0; i < 36; i++)
            {
                CreateMultiTransformLaser(
                    transform.position.x, transform.position.y,
                    1.0f, 15, RED, 3f, startAngle, 10, list,
                    0f, wigglePower, 6.0f, 3
                );
                startAngle += 360f / 36;
            }

            yield return new WaitForSeconds(4.0f);
        }
    }
    */
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
    // 移動専用のループ
    IEnumerator ContinuousMoveRoutine()
    {
        while (true)
        {
            yield return StartCoroutine(SetMovePositionRand03(
                moveMinX, moveMaxX,
                moveMinY, moveMaxY,
                moveWeight
            ));

            // 移動し終わったら少し待機して次の移動へ
            yield return new WaitForSeconds(fireInterval);
        }
    }
    void FireRound()
    {
        if (PlayerMove.Instance == null) return; //

        SEManager.Instance.Play(SEPath.SHOT1, 0.5f);
        //CreateRoundShot(bulletData, transform.position,5, bulletSpeed, angle ,10);

        CreateReflectShot(RED[0], transform.position, bulletSpeed, angle, 2, true, true, true, true, 10 ,-1f, BLUE[0]);

        angle += 11;
    }
    
    // StreamLaserPattern.cs
  
    IEnumerator MoveRoutine()
    {
        isMoving = true;

        // 第6引数（maxSpeed）は不要になったので、引数を整理して呼び出し
        yield return StartCoroutine(SetMovePositionRand03(
            moveMinX, moveMaxX,
            moveMinY, moveMaxY,
            moveWeight
        ));

        isMoving = false;
    }
}