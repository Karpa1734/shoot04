using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SealOrb : MonoBehaviour
{
    private SpriteRenderer sr;
    private Sprite impactShockwaveSprite;
    private float angle;
    private float radius = 0f;
    private int homingOrder;
    private EnemyStatus target;
    private GameObject impactShockwavePrefab;
    private bool isExploded = false;

    private bool isHomingMode = false;
    private float currentSpeed = 0f;
    private Vector3 lastPosition;

    // --- パラメータ調整 ---
    private const float SPREAD_SPEED = 0.02f;
    private const int BASE_ORBIT_FRAMES = 120;
    private const int HOMING_INTERVAL = 12;

    // 【新規】航行速度の設定
    private const float ENEMY_HOMING_SPEED = 12f;  // 敵に向かう時の速度
    private const float PLAYER_RETURN_SPEED = 12f; // 自機に戻る時の速度

    public void Initialize(Sprite orbSprite, Sprite shockSprite, float startAngle, int order, Color color, EnemyStatus enemy, GameObject shockPrefab)
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sprite = orbSprite;
        sr.color = color;
        impactShockwaveSprite = shockSprite;
        angle = startAngle;
        homingOrder = order;
        target = enemy;
        impactShockwavePrefab = shockPrefab;

        if (sr.material == null || sr.material.shader.name != "Legacy Shaders/Particles/Additive")
        {
            sr.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        }

        lastPosition = transform.position;
        StartCoroutine(MoveRoutine());
    }

    IEnumerator MoveRoutine()
    {
        // 1. 自機の周りを回転しながら広がるフェーズ [cite: 1, 4, 35]
        int orbitFrames = BASE_ORBIT_FRAMES + (homingOrder * HOMING_INTERVAL);
        for (int i = 0; i < orbitFrames; i++)
        {
            while (Time.timeScale <= 0) yield return null;
            if (isExploded) yield break;

            Vector3 posBefore = transform.position;
            OrbitPlayer();

            // 回転フェーズ中の移動速度を計算（移行時の初速として保持）
            currentSpeed = (transform.position - posBefore).magnitude / Time.deltaTime;

            yield return null;
        }

        // 2. 追跡（ホーミング）フェーズ開始 [cite: 37]
        isHomingMode = true;
        angle += 90f;

        float trackTimer = 0;
        float maxTrackTime = 150f; // 画面内に敵がいない場合を考慮し少し長めに

        while (trackTimer < maxTrackTime)
        {
            while (Time.timeScale <= 0) yield return null;
            if (isExploded) yield break;

            target = FindNearestEnemy();

            Vector3 destination;
            float homingDamp;
            float targetSpeed; // 【修正】距離依存ではない目標速度

            if (target != null)
            {
                destination = target.transform.position;
                homingDamp = 8f;               // 敵へのホーミング精度
                targetSpeed = ENEMY_HOMING_SPEED; // 敵への一定速度
            }
            else
            {
                destination = PlayerMove.Instance != null ? PlayerMove.Instance.transform.position : transform.position;
                // 【修正】自機へのホーミング精度を「敵よりも鋭く」設定（値を小さくして急旋回させる）
                homingDamp = 6f;
                targetSpeed = PLAYER_RETURN_SPEED; // 自機への一定速度
            }

            // 指定した速度と精度で移動
            HomingToPosition(destination, homingDamp, targetSpeed);

            trackTimer++;
            yield return null;
        }

        Explode(); // [cite: 39]
    }

    private void OrbitPlayer()
    {
        if (PlayerMove.Instance != null)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * radius;
            transform.position = PlayerMove.Instance.transform.position + offset;

            radius += SPREAD_SPEED;
            angle += 5f; // [cite: 5, 36]
        }
    }

    private void HomingToPosition(Vector3 targetPos, float damp, float targetSpeed)
    {
        Vector3 diff = targetPos - transform.position;

        float targetAngleRad = Mathf.Atan2(diff.y, diff.x);
        float judgangle = Mathf.Sin(targetAngleRad - (angle * Mathf.Deg2Rad));

        // ホーミング旋回ロジック 
        if (Mathf.Abs(judgangle) > 0.05f)
            angle += Mathf.Asin(judgangle) * Mathf.Rad2Deg / damp;
        else
            angle = targetAngleRad * Mathf.Rad2Deg;

        // 【修正】距離に関係なく一定の速度(targetSpeed)に向けて滑らかに補間
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, 0.15f);

        float rad = angle * Mathf.Deg2Rad;
        transform.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * currentSpeed * Time.deltaTime;
    }

    private EnemyStatus FindNearestEnemy()
    {
        EnemyStatus[] enemies = Object.FindObjectsByType<EnemyStatus>(FindObjectsSortMode.None);
        EnemyStatus nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (EnemyStatus e in enemies)
        {
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < minDistance)
            {
                minDistance = d;
                nearest = e;
            }
        }
        return nearest;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (Time.timeScale <= 0 || isExploded) return;

        // 1. 敵へのダメージ処理（既存通り）
        EnemyStatus enemy = collision.GetComponent<EnemyStatus>();
        if (enemy != null)
        {
            if (isHomingMode)
            {
                enemy.TakeDamage(180, true);
                Explode();
            }
            else
            {
                enemy.TakeDamage(20, true);
            }
            return;
        }

        // 2. 弾消し処理 ＋ ★追加：アイテム化
        if (collision.CompareTag("EnemyBullet"))
        {
            EnemyBullet bullet = collision.GetComponent<EnemyBullet>();
            if (bullet != null)
            {
                // --- ★修正ポイント：ボムで消えた弾をアイテム化して吸い寄せる ---
                if (ItemSpawner.Instance != null)
                {
                    // 弾の位置に SCORE00 アイテムを生成
                    ItemSpawner.Instance.SpawnItem(ItemController.ITEM_TYPE.SCORE00, bullet.transform.position, true);
                }

                bullet.Deactivate(true); // 消滅エフェクトを出して消す
            }
            else
            {
                Destroy(collision.gameObject);
            }
        }
    }
    void Explode()
    {
        if (isExploded) return;
        isExploded = true;

        SEManager.Instance.Play(SEPath.SHOT1);
        // 衝撃波の生成
        if (impactShockwavePrefab != null)
        {
            GameObject shock = Instantiate(impactShockwavePrefab, transform.position, Quaternion.identity);
            Shockwave logic = shock.GetComponent<Shockwave>();
            if (logic != null)
            {
                // 初期サイズ 0.4、拡大速度 0.03 で初期化
                logic.InitializeWithCustomScale(impactShockwaveSprite, sr.color, 0.4f, 0.03f,true);
            }
        }

        // 弾側の Deactivate(true) と同様に自身を破棄（オーブはプール管理でない場合）
        Destroy(gameObject);
    }
}