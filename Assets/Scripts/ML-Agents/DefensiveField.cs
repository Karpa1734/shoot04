using UnityEngine;
using System.Collections;

/// <summary>
/// 所有者情報を持ち、敵弾のみを消去して敵プレイヤーにのみダメージを与える防御フィールド。
/// </summary>
[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class DefensiveField : MonoBehaviour
{
    private Transform _owner; // 所有者のTransform
    private BulletData _data;
    private float _duration;

    [Header("Size Settings")]
    [SerializeField] private float _maxScale = 2.0f;
    [SerializeField] private float _expandTime = 0.2f;
    [SerializeField] private float _shrinkTime = 0.5f;

    /// <summary>
    /// 初期化。所有者の情報をセットし、タグとレイヤーを割り当てる。
    /// </summary>
    public void Initialize(Transform owner, BulletData data, float duration, string bulletTag, int layer)
    {
        _owner = owner;
        _data = data;
        _duration = duration;

        // 所有者に基づいた属性設定
        gameObject.tag = bulletTag;
        gameObject.layer = layer;

        // コライダーの設定[cite: 4]
        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = data.radius;
        col.offset = data.colliderOffset;

        StartCoroutine(FieldRoutine());
    }

    private IEnumerator FieldRoutine()
    {
        // 1. 出現（拡大）
        float elapsed = 0;
        while (elapsed < _expandTime)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.SmoothStep(0, _maxScale, elapsed / _expandTime);
            yield return null;
        }
        transform.localScale = Vector3.one * _maxScale;

        // 2. 持続フェーズ
        float stayElapsed = 0;
        while (stayElapsed < _duration)
        {
            // ラウンド終了時は中断して消滅へ[cite: 5]
            if (!PlayerMove.CanShoot) break;

            stayElapsed += Time.deltaTime;
            yield return null;
        }

        // 3. 消滅（縮小）
        elapsed = 0;
        while (elapsed < _shrinkTime)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.SmoothStep(_maxScale, 0, elapsed / _shrinkTime);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 所有者がいない場合は何もしない（フェールセーフ）[cite: 5]
        if (_owner == null) return;

        // --- 弾消し判定 ---
        // 衝突相手が弾丸タグを持っており、かつ自分（所有者）と同じタグでない場合（＝敵の弾）[cite: 4]
        if ((collision.CompareTag("PlayerBullet") || collision.CompareTag("EnemyBullet")) &&
            !collision.CompareTag(gameObject.tag))
        {
            DanmakuBullet bullet = collision.GetComponent<DanmakuBullet>();
            if (bullet != null)
            {
                // 消滅エフェクトを起動して弾を消す[cite: 4]
                bullet.Deactivate(true);
            }
            else
            {
                Destroy(collision.gameObject);
            }
        }

        // --- 敵プレイヤーへのダメージ判定 ---
        // 相手がプレイヤーであり、自分自身（所有者）ではない場合
        if (collision.CompareTag("Player") && collision.transform != _owner)
        {
            collision.SendMessage("OnHit", _data.damage, SendMessageOptions.DontRequireReceiver);
        }
    }
}