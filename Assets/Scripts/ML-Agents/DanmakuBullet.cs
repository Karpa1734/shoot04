using System.Collections;
using UnityEngine;

public class DanmakuBullet : MonoBehaviour
{
    [Header("Components")]
    private SpriteRenderer sr;
    private CircleCollider2D col;

    // 対戦用：誰が撃った弾か
    private GameObject owner;

    [Header("Effect Settings")]
    public GameObject effectPrefab; // 以前のEnemyBulletから引き継ぎ
    private GameObject activeDelayEffect;

    // 内部パラメータ
    private BulletData currentData;
    private float speed, angle, accel, maxSpeed, angularVelocity;
    private bool isInitialized = false;
    private bool isActive = true;
    private int delayFrames = 0;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>();
    }

    public void Initialize(GameObject shooter, float speed, float angle, float accel, float maxSpeed, float angVel, float delay, BulletData data)
    {
        this.owner = shooter;
        this.currentData = data;
        this.speed = speed;
        this.angle = angle;
        this.accel = accel;
        this.maxSpeed = maxSpeed;
        this.angularVelocity = angVel;
        this.delayFrames = Mathf.RoundToInt(delay);

        // スプライトと当たり判定の設定
        sr.sprite = data.bulletSprite;
        col.radius = data.radius;
        if (data.material != null) sr.material = data.material;

        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        if (delay > 0)
        {
            // 遅延がある場合はコルーチンで予告演出を再生
            StartCoroutine(DelayEffectRoutine(delay, data));
            sr.enabled = false;
            col.enabled = false;
        }
        else
        {
            sr.enabled = true;
            col.enabled = true;
        }

        isInitialized = true;
        isActive = true;
    }

    void FixedUpdate()
    {
        if (!isInitialized || !isActive) return;

        // 遅延カウントダウン
        if (delayFrames > 0)
        {
            delayFrames--;
            if (delayFrames <= 0)
            {
                sr.enabled = true;
                col.enabled = true;
                if (activeDelayEffect != null) Destroy(activeDelayEffect);
            }
            return;
        }

        float dt = Time.fixedDeltaTime;
        angle += angularVelocity * dt * 60f;
        speed += accel * dt * 60f;
        if (accel > 0 && speed > maxSpeed) speed = maxSpeed;

        float rad = angle * Mathf.Deg2Rad;
        transform.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * speed * dt;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // 画面外消去判定
        if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
            Deactivate(false);
    }

    // 遅延中の魔法陣演出
    IEnumerator DelayEffectRoutine(float frames, BulletData data)
    {
        if (effectPrefab != null && data.delaySprite != null)
        {
            activeDelayEffect = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            SpriteRenderer effSr = activeDelayEffect.GetComponent<SpriteRenderer>();
            if (effSr != null) effSr.sprite = data.delaySprite;

            // ShotEffectスクリプトがある場合は再生
            var logic = activeDelayEffect.GetComponent<ShotEffect>();
            if (logic != null)
                StartCoroutine(logic.PlayDelay(frames / 60f, data.delaySprite, transform.localScale.x));
        }
        yield return null;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 撃った本人には当たらない
        if (collision.gameObject == owner) return;

        if (collision.CompareTag("Player") || collision.CompareTag("Enemy"))
        {
            collision.SendMessage("OnHit", SendMessageOptions.DontRequireReceiver);
            Deactivate(true); // 被弾時はエフェクトを出す
        }
    }

    public void Deactivate(bool playBreakEffect)
    {
        if (!isActive) return;
        isActive = false;

        if (activeDelayEffect != null) Destroy(activeDelayEffect);

        // 消滅エフェクトの生成
        if (playBreakEffect && effectPrefab != null && currentData != null)
        {
            GameObject eff = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            var logic = eff.GetComponent<ShotEffect>();
            if (logic != null)
                StartCoroutine(logic.PlayBreakAnimation(currentData.breakColor, transform.localScale.x));
        }

        // ここでプールに戻すか破壊する
        Destroy(gameObject);
    }
}