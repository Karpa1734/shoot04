// --- DefensiveField.cs 連続多段ヒット＆滞在弾消し完全調停版 ---
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
    [SerializeField] private float _maxScale = 2.0f; //
    [SerializeField] private float _expandTime = 0.2f; //
    [SerializeField] private float _shrinkTime = 0.5f; //

    [Header("⚡ Continuous Hit Settings (連続ヒットインフラ)")]
    [Tooltip("重なり続けている際、何秒ごとに連続ダメージ（ヒット判定）を発生させるか")]
    [SerializeField] private float _hitInterval = 0.4f; // 💡 0.4秒間隔で多段ヒット
    private float _nextHitEnableTime = 0f; // 次にヒット可能になる絶対時刻

    /// <summary>
    /// 初期化。所有者の情報をセットし、タグとレイヤーを割り当てる。
    /// </summary>
    public void Initialize(Transform owner, BulletData data, float duration, string bulletTag, int layer, float overrideScale = -1f)
    {
        _owner = owner; //
        _data = data; //
        _duration = duration; //

        if (overrideScale > 0f) //
        {
            _maxScale = overrideScale; //
        }

        gameObject.tag = bulletTag; //
        gameObject.layer = layer; //

        var col = GetComponent<CircleCollider2D>(); //
        if (col != null) //
        {
            col.isTrigger = true; //
            col.radius = data.radius; //
            col.offset = data.colliderOffset; //
        }

        StartCoroutine(FieldRoutine()); //
    }

    private IEnumerator FieldRoutine() //
    {
        float elapsed = 0; //
        while (elapsed < _expandTime) //
        {
            elapsed += Time.deltaTime; //
            transform.localScale = Vector3.one * Mathf.SmoothStep(0, _maxScale, elapsed / _expandTime); //
            yield return null; //
        }
        transform.localScale = Vector3.one * _maxScale; //

        float stayElapsed = 0; //
        while (stayElapsed < _duration) //
        {
            if (!PlayerMove.CanShoot) break; //
            stayElapsed += Time.deltaTime; //
            yield return null; //
        }

        elapsed = 0; //
        while (elapsed < _shrinkTime) //
        {
            elapsed += Time.deltaTime; //
            transform.localScale = Vector3.one * Mathf.SmoothStep(_maxScale, 0, elapsed / _shrinkTime); //
            yield return null; //
        }

        Destroy(gameObject); //
    }

    // =========================================================================
    // 🎯【核心修正】：触れた瞬間(Enter)と、触れ続けている間(Stay)の両方で判定を共有化！
    // =========================================================================
    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleCollisionLogic(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        HandleCollisionLogic(collision);
    }

    /// <summary>
    /// リアルタイム持続当たり判定の統括制御コアエンジン
    /// </summary>
    private void HandleCollisionLogic(Collider2D collision)
    {
        if (_owner == null) return;

        // 🛑 1. 弾消し判定：フィールド内に「留まっている」「後から生まれてきた」敵弾も常時検知して即座に破砕消去！
        if ((collision.CompareTag("PlayerBullet") || collision.CompareTag("EnemyBullet")) &&
            !collision.CompareTag(gameObject.tag))
        {
            DanmakuBullet bullet = collision.GetComponent<DanmakuBullet>();
            if (bullet != null)
            {
                bullet.Deactivate(true);
            }
            else
            {
                Destroy(collision.gameObject);
            }
        }

        // ⚔️ 2. 敵プレイヤーへのダメージ判定：重なり続けている間、指定インターバル秒数ごとに連続OnHit！
        if (collision.CompareTag("Player") && collision.transform != _owner)
        {
            // 現在のゲーム時刻が、次のヒット許可時刻を超えていれば安全に多段ヒットを執行
            if (Time.time >= _nextHitEnableTime)
            {
                _nextHitEnableTime = Time.time + _hitInterval; // 次回発生時刻をリキャストチャージ
                collision.SendMessage("OnHit", _data.damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}