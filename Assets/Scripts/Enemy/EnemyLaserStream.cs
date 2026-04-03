// EnemyLaserStream.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyLaserStream : MonoBehaviour
{
    private BulletData bulletData;
    private Material laserMat;
    private float width;
    private int bulletCount;
    private float delayFrames;
    private int interval; // ★追加：何フレームに1回撃つか
    // 初期状態のパラメータ
    private float startSpeed, startAngle, startAccel, startAngVel, startMaxSpeed;

    private List<EnemyBullet.BulletTransformData> bulletTransforms = new List<EnemyBullet.BulletTransformData>();
    private bool isFiring = false;

    // ★引数を拡張して初期の加速・角速度を受け取れるようにする
    public void Setup(float x, float y, float width, int count, BulletData data, float delay, Material mat,
                      float speed, float angle, float accel, float angVel, float maxSpeed, int interval) // ★引数追加
    {
        transform.position = new Vector3(x, y, 0);
        this.width = width;
        this.bulletCount = count;
        this.bulletData = data;
        this.laserMat = mat;
        this.delayFrames = delay;
        this.interval = Mathf.Max(1, interval); // 最低でも1フレーム間隔

        this.startSpeed = speed;
        this.startAngle = angle;

        // 初期パラメータを 0秒目のデータとして入れる処理
        if (accel != 0 || angVel != 0)
        {
            bulletTransforms.Insert(0, new EnemyBullet.BulletTransformData
            {
                triggerTime = 0,
                speed = speed,
                angle = angle,
                accel = accel,
                angVel = angVel,
                maxSpeed = (maxSpeed <= 0) ? speed : maxSpeed
            });
        }
    }

    public void AddTransformData(EnemyBullet.BulletTransformData t) => bulletTransforms.Add(t);

    public void Fire() => StartCoroutine(StreamRoutine());

    IEnumerator StreamRoutine()
    {
        yield return new WaitForSeconds(delayFrames / 60f);
        isFiring = true;

        int firedCount = 0;
        while (firedCount < bulletCount)
        {
            SpawnBullet();
            firedCount++;
            // ★修正：指定されたフレーム数分だけ待機する
            for (int i = 0; i < interval; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            if (!isFiring) yield break;
        }
        Destroy(gameObject);
    }

    private void SpawnBullet()
    {
        if (BulletPool.Instance == null) return;

        GameObject segment = BulletPool.Instance.Get(bulletData.bulletPrefab, transform.position, Quaternion.identity);
        EnemyBullet eb = segment.GetComponent<EnemyBullet>();

        if (eb != null)
        {
            // ★修正箇所：EnemyBullet が元々持っている 6 つの引数に合わせる
            // (speed, angle, delay, data, transforms, mat)
            eb.InitializeMultiTransform(startSpeed, startAngle, 0, bulletData, bulletTransforms, laserMat);
            segment.transform.localScale = Vector3.one * width;
        }
    }

    public void ClearLaser()
    {
        isFiring = false;
        Destroy(gameObject);
    }
}