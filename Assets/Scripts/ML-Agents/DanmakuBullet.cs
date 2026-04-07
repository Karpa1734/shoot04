using System;
using System.Collections;
using UnityEngine;

public class DanmakuBullet : MonoBehaviour
{
    private SpriteRenderer sr;
    private CircleCollider2D col;

    [NonSerialized] public GameObject owner;    // 撃った人
    private string targetTag;    // 攻撃対象のタグ

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

    // ★ 初期化時に targetTag を受け取るように拡張
    public void Initialize(GameObject shooter, string target, float speed, float angle, float accel, float maxSpeed, float angVel, float delay, BulletData data)
    {
        this.owner = shooter;
        this.targetTag = target; // "Player" か "Enemy" を受け取る
        this.currentData = data;
        this.speed = speed;
        this.angle = angle;
        this.accel = accel;
        this.maxSpeed = maxSpeed;
        this.angularVelocity = angVel;
        this.delayFrames = Mathf.RoundToInt(delay);

        sr.sprite = data.bulletSprite;
        col.radius = data.radius;
        if (data.material != null) sr.material = data.material;

        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // 遅延処理
        if (delay > 0)
        {
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

        if (delayFrames > 0)
        {
            delayFrames--;
            if (delayFrames <= 0)
            {
                sr.enabled = true;
                col.enabled = true;
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

        if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
            Deactivate();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Hit!!" + collision.gameObject);
        if (!isInitialized || owner == null) return;

        // 1. 自分自身（親）や自分の子供（当たり判定用Hitbox）には絶対に当たらない
        if (collision.gameObject == owner || collision.transform.IsChildOf(owner.transform))
        {
            return;
        }

        // 2. 指定されたターゲットタグに当たった場合のみ処理
        // 1vs1で両方が "Player" タグの場合でも、上記の「owner判定」で自爆は防げます
        if (collision.CompareTag(targetTag))
        {
            collision.SendMessage("OnHit", SendMessageOptions.DontRequireReceiver);
            Deactivate();
        }
    }

    public void Deactivate()
    {
        isActive = false;
        isInitialized = false;
        Destroy(gameObject);
    }
}