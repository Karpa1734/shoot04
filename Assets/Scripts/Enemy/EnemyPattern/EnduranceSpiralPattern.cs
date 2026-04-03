using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

public class EnduranceSpiralPattern : BossPatternBase
{
    [Header("Bullet Settings")]
    public BulletData bulletData;
    public float bulletSpeed = 3f;
    public int wayCount = 4;
    public float rotationSpeed = 10f;
    public float fireInterval = 0.1f;

    private float angle = 0f;
    private bool isFiring = false;

        float timer = 0f;
    protected override void Awake()
    {
        base.Awake();

        // --- 追加：親オブジェクト（ボス本体）からコンポーネントを取得する ---
        if (parentCollider == null)
            parentCollider = GetComponentInParent<Collider2D>();

        if (bossRenderer == null)
            bossRenderer = transform.parent.GetComponentInChildren<SpriteRenderer>();
    }

    IEnumerator Start()
    {
        // 1. 2秒間じっとする
        yield return new WaitForSeconds(2.0f);

        // 2. Y座標 0 に移動（weight=60は約1秒）
        yield return StartCoroutine(SetMovePosition03(CENTER_X, 0f, 60f));

        // 3. 透明化（1秒かけてアルファ0.2へ）と当たり判定の消去
        yield return StartCoroutine(StealthRoutine(1.0f, 0.2f));

        // 4. 弾幕発射フラグをオン（フェーズ終了まで持続）
        isFiring = true;
    }

    void Update()
    {
        if (!isFiring || bulletData == null) return;

        // 弾幕の回転と発射間隔の管理
        angle += rotationSpeed * Time.deltaTime * 60f;

        timer += Time.deltaTime;

        if (timer >= fireInterval)
        {
            FireSpiral();
            timer = 0f;
        }
    }

    void FireSpiral()
    {
        SEManager.Instance.Play(SEPath.SHOT1, 0.2f);
        for (int i = 0; i < wayCount; i++)
        {
            float currentAngle = angle + (i * 360f / wayCount);
            // 弾の生成
            CreateShot(GREEN[0], transform.position, bulletSpeed, currentAngle);
        }
    }

    // 透明化と当たり判定をオフにするコルーチン
    IEnumerator StealthRoutine(float duration, float targetAlpha)
    {
        // 当たり判定を完全に消す（自機との衝突・ショットの着弾がなくなります）
        if (parentCollider != null) parentCollider.enabled = false;

        if (bossRenderer != null)
        {
            float elapsed = 0f;
            Color startColor = bossRenderer.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(startColor.a, targetAlpha, elapsed / duration);
                bossRenderer.color = new Color(startColor.r, startColor.g, startColor.b, a);
                yield return null;
            }
        }
    }

    // パターン終了（フェーズ移行）時に必ず元の状態に戻す
    protected override void OnDestroy()
    {
        // 基底クラスのリセット処理（当たり判定を戻す、色を戻す）を呼び出す
        base.OnDestroy();

        // 個別のスクリプトで追加したい処理があればここに書く
    }
}