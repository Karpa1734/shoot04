using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Basic Settings")]
    public float speed = 22.5f; // Shot.txt準拠のメインショット速度
    public float damage = 1.0f;
    public bool isHoming = false; // ホーミング弾ならチェックを入れる

    [Header("Homing Settings")]
    public float homingLimitTime = 1.0f; // 追尾をあきらめるまでの時間 
    public float homingSensitivity = 10f; // 回転の鋭さ 

    [Header("Hit Effect Settings")]
    public Sprite hitSprite;      // ヒット時の画像 (pl_shotの左下) [cite: 4, 20]
    public Material addMaterial;  // 加算合成マテリアル [cite: 5, 20]
    public float fadeTime = 0.2f; // フェードアウト時間 [cite: 6, 21]

    private bool isHit = false;
    private float hitTimer = 0f;
    private Vector3 hitVelocity;
    private SpriteRenderer sr;
    private CircleCollider2D col;

    // ホーミング用内部変数
    private Transform target;
    private float homingTimer = 0f;
    private bool isHomingActive = true;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>();

        // 生成時に変数を初期化
        homingTimer = 0f;
        isHomingActive = isHoming;
    }

    void Update()
    {
        if (isHit)
        {
            UpdateHitEffect();
            return;
        }

        // --- 移動・ホーミング処理 ---
        if (isHoming && isHomingActive)
        {
            UpdateHomingLogic();
        }

        // 常に前方（transform.up）へ進む
        transform.Translate(Vector3.up * speed * Time.deltaTime);

        // 画面外判定
        if (Mathf.Abs(transform.position.y) > 6f || Mathf.Abs(transform.position.x) > 8f)
        {
            Destroy(gameObject);
        }
    }

    void UpdateHomingLogic()
    {
        // ターゲットがいない、または非アクティブなら探す [cite: 11, 18]
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            FindClosestEnemy();
        }

        if (target != null)
        {
            // ターゲットへの方向を計算
            Vector3 dir = (target.position - transform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            Quaternion q = Quaternion.AngleAxis(angle, Vector3.forward);

            // 徐々に回転を合わせる (asin/atan2ロジックのUnity版) 
            transform.rotation = Quaternion.Slerp(transform.rotation, q, Time.deltaTime * homingSensitivity);
        }

        // 時間経過でホーミングを停止（処理落ち対策） 
        homingTimer += Time.deltaTime;
        if (homingTimer >= homingLimitTime)
        {
            isHomingActive = false;
        }
    }

    void FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float minDis = Mathf.Infinity;
        foreach (GameObject e in enemies)
        {
            float dis = Vector2.Distance(transform.position, e.transform.position);
            if (dis < minDis)
            {
                minDis = dis;
                target = e.transform;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isHit) return;

        if (collision.CompareTag("Enemy"))
        {
            collision.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            StartHitEffect();
        }
    }

    void StartHitEffect()
    {
        isHit = true;
        if (col != null) col.enabled = false;

        // ヒット時の慣性計算 [cite: 5, 20]
        float inertiaFactor = isHoming ? 4f : 8f;
        hitVelocity = transform.up * (speed / inertiaFactor);

        if (sr != null)
        {
            if (hitSprite != null) sr.sprite = hitSprite;
            if (addMaterial != null) sr.material = addMaterial;
        }
    }

    void UpdateHitEffect()
    {
        hitTimer += Time.deltaTime;
        transform.position += hitVelocity * Time.deltaTime;

        if (sr != null)
        {
            float alpha = Mathf.Lerp(0.75f, 0f, hitTimer / fadeTime);
            sr.color = new Color(1, 1, 1, alpha);
        }

        if (hitTimer >= fadeTime) Destroy(gameObject);
    }
}