using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BulletClearEffect : MonoBehaviour
{
    public float expandSpeed = 20f; // 円が広がる速度
    public float maxRadius = 15f;   // 画面全体を覆うのに十分な半径

    public void StartClearing(Vector3 center)
    {
        transform.position = center;
        StartCoroutine(ClearRoutine());
    }

    IEnumerator ClearRoutine()
    {
        float currentRadius = 0f;

        while (currentRadius < maxRadius)
        {
            currentRadius += expandSpeed * Time.deltaTime;

            // --- 1. 通常の弾（EnemyBullet）の消去 ---
            GameObject[] bullets = GameObject.FindGameObjectsWithTag("EnemyBullet");
            foreach (GameObject b in bullets)
            {
                if (b == null) continue;
                float distance = Vector2.Distance(transform.position, b.transform.position);
                if (distance < currentRadius)
                {
                    EnemyBullet eb = b.GetComponent<EnemyBullet>();
                    if (eb != null) eb.Deactivate(true);
                }
            }

            // --- 2. ストリームレーザー（EnemyLaserStream）の消去 ---
            EnemyLaserStream[] streams = Object.FindObjectsByType<EnemyLaserStream>(FindObjectsSortMode.None);
            foreach (EnemyLaserStream s in streams)
            {
                if (s == null) continue;
                float distance = Vector2.Distance(transform.position, s.transform.position);
                if (distance < currentRadius) s.ClearLaser();
            }

            // --- 3. ★設置型・極太レーザー（EnemyLaserBeam）の消去を追加 ---
            // FindObjectsByType を使用して全てのレーザーを取得
            EnemyLaserBeam[] beams = Object.FindObjectsByType<EnemyLaserBeam>(FindObjectsSortMode.None);
            foreach (EnemyLaserBeam beam in beams)
            {
                if (beam == null) continue;

                // レーザーの起点（設置点）との距離を計算
                float distance = Vector2.Distance(transform.position, beam.transform.position);

                if (distance < currentRadius)
                {
                    // ★重要：Destroy ではなく ForceClose を呼ぶことで「細くなって消える」
                    beam.ForceClose();
                }
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}