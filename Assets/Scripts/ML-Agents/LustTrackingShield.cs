using UnityEngine;
using System.Collections;

/// <summary>
/// 色欲Dスキル：敵の座標をリアルタイム追従し、残像を残して回転しながら、
/// 敵の全弾幕を消去しつつじわじわと進軍する追尾型絶対防御魔槍ビット。
/// 💡【拡張仕様】：外部からの強制消滅（EX連動パージ）インフラを新設。
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D), typeof(Rigidbody2D))]
public class LustTrackingShield : MonoBehaviour
{
    private Transform _owner;
    private Transform _targetTransform;
    private BulletData _data;

    private SpriteRenderer _sr;
    private float _speed;
    private float _duration = 8.0f;

    private float _targetBaseScale = 1.0f;
    private bool _isDespawning = false; // 💡 二重消滅・多重コルーチン衝突防止フラグ

    [Header("Behavior Settings")]
    [SerializeField] private float _expandTime = 0.3f;
    [SerializeField] private float _shrinkTime = 0.3f;
    [SerializeField] private float _rotationSpeed = 500f;

    [Header("⚡ Continuous Hit Settings (多段連続ヒット)")]
    [SerializeField] private float _hitInterval = 0.4f;
    private float _nextHitEnableTime = 0f;

    [Header("Afterimage Settings")]
    [SerializeField] private float _afterimageInterval = 0.04f;
    [SerializeField] private float _afterimageFadeTime = 0.4f;
    [SerializeField] private float _afterimageStartAlpha = 0.4f;
    private float _afterimageTimer = 0f;

    public void Initialize(Transform owner, Transform target, BulletData data, float speed, float duration)
    {
        _owner = owner;
        _targetTransform = target;
        _data = data;
        _speed = speed;
        _duration = duration;
        _sr = GetComponent<SpriteRenderer>();

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        _targetBaseScale = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = Vector3.zero;

        StartCoroutine(TrackingShieldRoutine());
    }

    private void Update()
    {
        transform.Rotate(0, 0, _rotationSpeed * Time.deltaTime);
        if (transform.localScale.x > 0.05f)
        {
            UpdateAfterimage();
        }
    }

    private IEnumerator TrackingShieldRoutine()
    {
        yield return StartCoroutine(ScaleRoutine(0f, _targetBaseScale, _expandTime));

        float elapsed = 0f;

        while (elapsed < _duration)
        {
            if (_isDespawning) yield break; // 💡 強制パージが走っていたらメインループを即断絶
            while (Mathf.Approximately(Time.timeScale, 0f)) yield return null;
            if (!PlayerMove.CanShoot) break;

            if (_targetTransform != null)
            {
                Vector3 currentTargetPos = _targetTransform.position;
                Vector3 moveDirection = (currentTargetPos - transform.position).normalized;
                transform.position += moveDirection * _speed * Time.fixedDeltaTime;
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        yield return StartCoroutine(ScaleRoutine(_targetBaseScale, 0f, _shrinkTime));

        Destroy(gameObject);
    }

    // =========================================================================
    // ⚡【新設】：EXスキル（ULT）発動時に、手前のシールドを高速で安全に消滅させる窓口
    // =========================================================================
    public void ForceRequestDespawn()
    {
        if (_isDespawning) return;
        _isDespawning = true;

        StopAllCoroutines(); // 既存の追尾タイムラインを完全遮断
        StartCoroutine(ExecuteForceDespawnRoutine());
    }

    private IEnumerator ExecuteForceDespawnRoutine()
    {
        // 現在のサイズから0に向けてぬるっと最速で縮小パージ
        yield return StartCoroutine(ScaleRoutine(transform.localScale.x, 0f, _shrinkTime * 0.5f));
        Destroy(gameObject);
    }

    private IEnumerator ScaleRoutine(float startScale, float endScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            while (Mathf.Approximately(Time.timeScale, 0f)) yield return null;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentScale = Mathf.SmoothStep(startScale, endScale, t);
            transform.localScale = Vector3.one * currentScale;
            yield return null;
        }
        transform.localScale = Vector3.one * endScale;
    }

    private void UpdateAfterimage()
    {
        if (_sr == null) return;
        _afterimageTimer += Time.deltaTime;
        if (_afterimageTimer >= _afterimageInterval)
        {
            _afterimageTimer = 0;
            SpawnAfterimage();
        }
    }

    private void SpawnAfterimage()
    {
        GameObject go = new GameObject("LustShieldAfterimage");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.localScale;

        SpriteRenderer trailSR = go.AddComponent<SpriteRenderer>();
        trailSR.sprite = _sr.sprite;
        trailSR.color = _sr.color;
        trailSR.material = _sr.material;
        trailSR.sortingLayerID = _sr.sortingLayerID;
        trailSR.sortingOrder = _sr.sortingOrder - 1;

        ShieldAfterimageFader fader = go.AddComponent<ShieldAfterimageFader>();
        fader.Setup(_afterimageFadeTime, _afterimageStartAlpha);
    }

    private void OnTriggerEnter2D(Collider2D collision) { HandleShieldCollision(collision); }
    private void OnTriggerStay2D(Collider2D collision) { HandleShieldCollision(collision); }

    private void HandleShieldCollision(Collider2D collision)
    {
        if (_owner == null) return;
        if (transform.localScale.x < 0.1f) return;

        // ⭕ 修正後：不滅フラグを絶対にすり抜けさせない安全盾の溶接
        if ((collision.CompareTag("PlayerBullet") || collision.CompareTag("EnemyBullet")) && !collision.CompareTag(gameObject.tag))
        {
            // 親オブジェクトも含めてコンポーネントを安全にスキャン
            DanmakuBullet bullet = collision.GetComponentInParent<DanmakuBullet>();
            if (bullet != null)
            {
                // 💡 相手が不滅弾（isIndestructible）なら消去を絶対に拒絶してスルーする！
                if (!bullet.isIndestructible)
                {
                    bullet.Deactivate(true);
                }
            }
            else
            {
                // コンポーネントが無く、かつタグが弾の場合のみ、安全を確認して消去
                Destroy(collision.gameObject);
            }
        }

        if (collision.CompareTag("Player") && collision.transform != _owner)
        {
            if (Time.time >= _nextHitEnableTime)
            {
                _nextHitEnableTime = Time.time + _hitInterval;
                collision.SendMessage("OnHit", _data.damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private class ShieldAfterimageFader : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _fadeDuration;
        private float _startAlpha;
        private float _elapsed = 0;

        public void Setup(float duration, float startAlpha)
        {
            _fadeDuration = duration;
            _startAlpha = startAlpha;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = _startAlpha;
                _sr.color = c;
            }
        }

        private void Update()
        {
            if (_sr == null) return;
            _elapsed += Time.deltaTime;
            float t = _elapsed / _fadeDuration;

            Color c = _sr.color;
            c.a = Mathf.Lerp(_startAlpha, 0f, t);
            _sr.color = c;

            if (_elapsed >= _fadeDuration) Destroy(gameObject);
        }
    }
}