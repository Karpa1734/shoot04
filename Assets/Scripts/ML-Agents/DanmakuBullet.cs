using System.Collections;
using UnityEngine;

public class DanmakuBullet : MonoBehaviour
{
    private SpriteRenderer sr;
    private CircleCollider2D col;

    public GameObject owner { get; private set; }
    private string targetTag;

    [Header("Effect Settings")]
    public GameObject effectPrefab; // ShotEffectが付いているプレハブを指定
    private GameObject activeDelayEffect;

    private BulletData currentData;
    private float speed, angle, accel, maxSpeed, angularVelocity;
    private bool isInitialized = false;
    private bool isActive = true;
    private int delayFrames = 0;
    private int totalDelay = 0;

    // 収束用フラグ
    private bool isConverging = false;
    private Vector3 initialOffset;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>();
    }

    public void Initialize(GameObject shooter, string target, float speed, float angle, float accel, float maxSpeed, float angVel, float delay, BulletData data, bool converge = false)
    {
        this.owner = shooter;
        this.targetTag = target;
        this.currentData = data;
        this.speed = speed;
        this.angle = angle;
        this.accel = accel;
        this.maxSpeed = maxSpeed;
        this.angularVelocity = angVel;
        this.delayFrames = Mathf.RoundToInt(delay);
        this.totalDelay = this.delayFrames;
        this.isConverging = converge;

        sr.sprite = data.bulletSprite;
        col.radius = data.radius;
        if (data.material != null) sr.material = data.material;

        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        if (delay > 0)
        {
            // --- 遅延エフェクト（魔法陣）の表示 ---
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

        // --- ディレイ（待機・収束）フェーズ ---
        if (delayFrames > 0)
        {
            if (isConverging && owner != null)
            {
                // 残りフレーム数に合わせて、徐々にオーナー（自機）の座標へ近づく
                // 1フレームあたりの移動量を計算して移動
                float t = 1f / delayFrames;
                transform.position = Vector3.Lerp(transform.position, owner.transform.position, t);
            }

            delayFrames--;
            if (delayFrames <= 0)
            {
                sr.enabled = true;
                col.enabled = true;
                if (activeDelayEffect != null) Destroy(activeDelayEffect);
            }
            return;
        }

        // --- 発射・移動フェーズ ---
        float dt = Time.fixedDeltaTime;
        angle += angularVelocity * dt * 60f;
        speed += accel * dt * 60f;
        if (accel != 0 && speed > maxSpeed) speed = maxSpeed;

        float rad = angle * Mathf.Deg2Rad;
        transform.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * speed * dt;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
            Destroy(gameObject);
    }

    private IEnumerator DelayEffectRoutine(float delay, BulletData data)
    {
        if (effectPrefab != null && data.delaySprite != null)
        {
            activeDelayEffect = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            // 魔法陣を弾に追従させる
            activeDelayEffect.transform.SetParent(this.transform);

            SpriteRenderer effSr = activeDelayEffect.GetComponent<SpriteRenderer>();
            if (effSr != null)
            {
                effSr.sprite = data.delaySprite;
                // 弾より少し手前に表示
                effSr.sortingOrder = sr.sortingOrder + 1;
            }

            ShotEffect logic = activeDelayEffect.GetComponent<ShotEffect>();
            if (logic != null)
            {
                // ShotEffect側のPlayDelayコルーチンを実行
                StartCoroutine(logic.PlayDelay(delay / 60f, data.delaySprite, transform.localScale.x));
            }
        }
        yield return null;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isInitialized || owner == null) return;
        if (collision.gameObject == owner || collision.transform.IsChildOf(owner.transform)) return;

        if (collision.CompareTag(targetTag))
        {
            collision.SendMessage("OnHit", SendMessageOptions.DontRequireReceiver);
            Deactivate();
        }
    }

    public void Deactivate()
    {
        isActive = false;
        if (activeDelayEffect != null) Destroy(activeDelayEffect);
        Destroy(gameObject);
    }
}