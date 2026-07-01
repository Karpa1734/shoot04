using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 壁激激突点に居座る必殺爆発フィールド。敵弾をバリバリ消去し、
/// 敵プレイヤーに多段ダメージを与えながら、任意のカスタム弾幕を乱舞させる。
/// 💡【領域展開マトリクス変調】：領域中なら爆発スケールを3.0に巨大化し、弾幕の層を18way×7列へと動的ブースト！
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
public class LustEXExplosionField : MonoBehaviour
{
    private GameObject _owner;
    private BulletData _bulletData;
    private PlayerDanmakuEmitter _emitter;
    private float _duration;

    private float _hitInterval = 0.3f;
    private float _nextHitEnableTime = 0f;

    public void Initialize(GameObject owner, BulletData subBullet, PlayerDanmakuEmitter emitter, float duration)
    {
        _owner = owner;
        _bulletData = subBullet;
        _emitter = emitter;
        _duration = duration;

        // 💡 呼び出し元（自機）のスペルカード（領域展開）ステートを安全監査
        bool isSpellActive = false;
        if (_owner != null)
        {
            PlayerStatusManager ownerStatus = _owner.GetComponent<PlayerStatusManager>();
            if (ownerStatus == null) ownerStatus = _owner.GetComponentInParent<PlayerStatusManager>();
            if (ownerStatus != null)
            {
                isSpellActive = ownerStatus.isSpellCardActive;
            }
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && subBullet != null)
        {
            sr.sprite = (subBullet.delaySprite != null) ? subBullet.delaySprite : subBullet.bulletSprite;
            sr.material = new Material(Shader.Find("Sprites/Default"));
            sr.color = new Color(1f, 1f, 0f, 0.8f);
            sr.sortingLayerName = "Middle";
            sr.sortingOrder = 14950;
        }

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            // 🎯 領域展開中ならコライダーの攻撃半径もスケールに合わせて動的に拡張
            float baseRadius = (subBullet != null && subBullet.radius > 0f) ? subBullet.radius * 2.5f : 2.0f;
            col.radius = isSpellActive ? (baseRadius * 1.5f) : baseRadius;
        }

        transform.localScale = Vector3.zero;
        StartCoroutine(FieldTimelineRoutine(isSpellActive)); // 💡 領域ステートをタイムラインへインジェクション
    }

    private IEnumerator FieldTimelineRoutine(bool isSpellActive)
    {
        // =========================================================================
        // 🎯【指定1：領域展開による爆発スケールの動的分岐】
        // =========================================================================
        float elapsed = 0f;
        float expandTime = 0.25f;
        // 💡 領域展開中であれば最大サイズを『3.0f』へ巨大化、通常時は『2.0f』を維持
        float targetScale = isSpellActive ? 3.0f : 2.0f;

        while (elapsed < expandTime)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.SmoothStep(0f, targetScale, elapsed / expandTime);
            yield return null;
        }
        transform.localScale = Vector3.one * targetScale;

        // =========================================================================
        // 🎯【指定2：18way ✕ 速度差7列 ✕ 90度自動クランプ射出への拡張】
        // =========================================================================
        if (_emitter != null && _bulletData != null)
        {
            int shotCount = isSpellActive ? 28 : 18; // 💡 18way固定
            float angleStep = 360f / shotCount;

            FieldInfo angVelField = typeof(DanmakuBullet).GetField("angularVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo angleField = typeof(DanmakuBullet).GetField("angle", BindingFlags.NonPublic | BindingFlags.Instance);

            List<DanmakuBullet> spawnedBullets = new List<DanmakuBullet>();
            List<float> initialAngles = new List<float>();

            float baseAngleVelocityFrame = 1.0f;
            float maxRotationLimitDegrees = 60.0f;

            // 💡【列数の動的ポリモーフィズム】：領域展開中なら『7列』、通常空間なら『5列』へと自動仕分け
            int targetRowsCount = isSpellActive ? 9 : 5;

            for (int row = 0; row < targetRowsCount; row++)
            {
                float speedForLayer = 2.8f + (row * 0.8f);
                float curveSign = (row % 2 == 0) ? 1.0f : -1.0f;
                float assignedAngularVelocity = baseAngleVelocityFrame * curveSign;

                float angleOffsetForRow = row * 6.0f;

                for (int i = 0; i < shotCount; i++)
                {
                    float finalAngle = (i * angleStep) + angleOffsetForRow;
                    _emitter.ExecuteSubShot(_bulletData, transform.position, speedForLayer, finalAngle, accel: 0f, maxSpeed: 0f, gameObject.tag, gameObject.layer);
                }

                // 領域巨大化に伴い、検出オーバーラップ円の走査半径も1.5fから倍率補正
                float overlapRadius = isSpellActive ? 2.5f : 1.5f;
                Collider2D[] spawnedCheck = Physics2D.OverlapCircleAll(transform.position, overlapRadius);
                foreach (var c in spawnedCheck)
                {
                    if (c.CompareTag(gameObject.tag))
                    {
                        DanmakuBullet b = c.GetComponent<DanmakuBullet>();
                        if (b != null && !b.isIndestructible && angVelField != null && angleField != null)
                        {
                            angVelField.SetValue(b, assignedAngularVelocity);
                            b.isIndestructible = true;

                            float currentRealAngle = (float)angleField.GetValue(b);
                            spawnedBullets.Add(b);
                            initialAngles.Add(currentRealAngle);
                        }
                    }
                }
            }

            if (spawnedBullets.Count > 0 && angVelField != null && angleField != null)
            {
                StartCoroutine(ClampBulletRotationWithVisualTrailRoutine(spawnedBullets, initialAngles, angVelField, angleField, maxRotationLimitDegrees));
            }

            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT2, 0.4f);
        }

        // 2. 設置持続ループ
        float stayElapsed = 0f;
        while (stayElapsed < _duration)
        {
            if (!PlayerMove.CanShoot) break;
            stayElapsed += Time.fixedDeltaTime;
            yield return new WaitForSeconds(Time.fixedDeltaTime); // 💡 Time.timeScale=0f時のフリーズバグ対策
        }

        // 3. 縮小消滅
        elapsed = 0f;
        while (elapsed < expandTime)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.SmoothStep(targetScale, 0f, elapsed / expandTime);
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator ClampBulletRotationWithVisualTrailRoutine(List<DanmakuBullet> bullets, List<float> initialAngles, FieldInfo angVelField, FieldInfo angleField, float maxLimit)
    {
        int trailFrameCounter = 0;
        while (bullets.Count > 0)
        {
            yield return new WaitForFixedUpdate();
            trailFrameCounter++;
            bool shouldSpawnTrailThisFrame = (trailFrameCounter % 3 == 0);

            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                DanmakuBullet b = bullets[i];
                if (b == null || !b.gameObject.activeSelf)
                {
                    bullets.RemoveAt(i);
                    initialAngles.RemoveAt(i);
                    continue;
                }

                if (shouldSpawnTrailThisFrame) SpawnBulletVisualTrailPiece(b);

                if (angleField != null)
                {
                    float currentAngle = (float)angleField.GetValue(b);
                    float startAngle = initialAngles[i];
                    float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(startAngle, currentAngle));

                    if (deltaAngle >= maxLimit)
                    {
                        angVelField.SetValue(b, 0f);
                        bullets.RemoveAt(i);
                        initialAngles.RemoveAt(i);
                    }
                }
            }
        }
    }

    private void SpawnBulletVisualTrailPiece(DanmakuBullet targetBullet)
    {
        if (targetBullet == null) return;
        SpriteRenderer bulletSR = targetBullet.GetComponentInChildren<SpriteRenderer>();
        if (bulletSR == null) return;

        GameObject go = new GameObject("LustSpearVisualTrail");
        go.transform.SetPositionAndRotation(targetBullet.transform.position, targetBullet.transform.rotation);
        go.transform.localScale = targetBullet.transform.localScale;

        SpriteRenderer trailSR = go.AddComponent<SpriteRenderer>();
        trailSR.sprite = bulletSR.sprite;
        trailSR.material = bulletSR.material;
        trailSR.sortingLayerID = bulletSR.sortingLayerID;
        trailSR.sortingOrder = bulletSR.sortingOrder - 1;

        Color bColor = bulletSR.color;
        trailSR.color = new Color(bColor.r, bColor.g * 0.5f, bColor.b, 0.5f);

        BulletVisualTrailFader fader = go.AddComponent<BulletVisualTrailFader>();
        fader.Setup(duration: 0.25f, startAlpha: 0.45f);
    }

    private void OnTriggerEnter2D(Collider2D collision) { HandleShieldLogic(collision); }
    private void OnTriggerStay2D(Collider2D collision) { HandleShieldLogic(collision); }

    private void HandleShieldLogic(Collider2D collision)
    {
        if (_owner == null || transform.localScale.x < 0.2f) return;

        if ((collision.CompareTag("PlayerBullet") || collision.CompareTag("EnemyBullet")) && !collision.CompareTag(gameObject.tag))
        {
            DanmakuBullet bullet = collision.GetComponent<DanmakuBullet>();
            if (bullet != null)
            {
                if (!bullet.isIndestructible) bullet.Deactivate(true);
            }
            else Destroy(collision.gameObject);
        }

        if (collision.CompareTag("Player") && collision.gameObject != _owner)
        {
            if (Time.time >= _nextHitEnableTime)
            {
                _nextHitEnableTime = Time.time + _hitInterval;
                collision.SendMessage("OnHit", 15, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void OnDestroy()
    {
        if (_owner != null)
        {
            PlayerStatusManager ownerStatus = _owner.GetComponent<PlayerStatusManager>();
            if (ownerStatus == null) ownerStatus = _owner.GetComponentInParent<PlayerStatusManager>();

            if (ownerStatus != null && ownerStatus.isSpellCardActive)
            {
                ownerStatus.DeactivateSpellCard(false);
            }

            if (_emitter != null)
            {
                System.Reflection.FieldInfo exActiveField = typeof(PlayerDanmakuEmitter).GetField("_isEXSkillActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (exActiveField != null)
                {
                    exActiveField.SetValue(_emitter, false);
                }
            }
        }
    }

    private class BulletVisualTrailFader : MonoBehaviour
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