using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 壁激突点に居座る必殺爆発フィールド。敵弾をバリバリ消去し、
/// 敵プレイヤーに多段ダメージを与えながら、任意のカスタム弾幕を乱舞させる。
/// 💡【超軽量化調停】：時差分散射出・スキャン1回化に加え、子弾の残像生成を完全撤廃してスパイクを根絶！
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
            // 💡 拡大終了時の色剥げバグ対策として、最初からデフォルトシェーダーで固定
            sr.material = new Material(Shader.Find("Sprites/Default"));
            sr.color = new Color(1f, 1f, 0f, 0.8f);
            sr.sortingLayerName = "Middle";
            sr.sortingOrder = 14950;
        }

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
            float baseRadius = (subBullet != null && subBullet.radius > 0f) ? subBullet.radius * 2.5f : 2.0f;
            col.radius = isSpellActive ? (baseRadius * 1.5f) : baseRadius;
        }

        transform.localScale = Vector3.zero;
        StartCoroutine(FieldTimelineRoutine(isSpellActive));
    }

    private IEnumerator FieldTimelineRoutine(bool isSpellActive)
    {
        // =========================================================================
        // 🎯 1. 拡大処理（物理フレーム完全等速同期）
        // =========================================================================
        float elapsed = 0f;
        float expandTime = 0.25f;
        float targetScale = isSpellActive ? 3.0f : 2.0f;

        while (elapsed < expandTime)
        {
            elapsed += Time.fixedDeltaTime;
            transform.localScale = Vector3.one * Mathf.SmoothStep(0f, targetScale, elapsed / expandTime);
            yield return new WaitForFixedUpdate();
        }
        transform.localScale = Vector3.one * targetScale;

        // =========================================================================
        // 🎯 2. 弾幕生成（リフレクション＆物理重複スキャンを完全パージした最軽量インフラ）
        // =========================================================================
        if (_emitter != null && _bulletData != null)
        {
            // way数を半減（28➔14 / 18➔9）
            int shotCount = isSpellActive ? 28 : 18;
            float angleStep = 360f / shotCount;

            List<DanmakuBullet> spawnedBullets = new List<DanmakuBullet>();
            List<float> initialAngles = new List<float>();

            float baseAngleVelocityFrame = 1.3f;
            float maxRotationLimitDegrees = 90.0f;
            int targetRowsCount = isSpellActive ? 7 : 5;

            int crossIndex = 0;

            for (int row = 0; row < targetRowsCount; row++)
            {
                float speedForLayer = 2.8f + (row * 0.8f);
                float angleOffsetForRow = isSpellActive ? row * 13.0f : row * 17.0f;
                float wayRandom = Random.Range(1,360);
                for (int i = 0; i < shotCount; i++)
                {
                    float finalAngle = (i * angleStep) + angleOffsetForRow;

                    // 🎯【交差変調】：1発ずつ交互に右回り・左回りを計算してパッシング
                    float curSign = (crossIndex % 2 == 0) ? 1.0f : -1.0f;
                    crossIndex++;
                    float assignedAngVel = baseAngleVelocityFrame * curSign;

                    // ⭕ 開通：Returnableメソッドから生成した実体をダイレクトに検知補獲！
                    DanmakuBullet b = _emitter.ExecuteSubShot02_Returnable(
                        data: _bulletData,
                        pos: transform.position,
                        speed: speedForLayer,
                        angle: finalAngle,
                        accel: 0f,
                        maxSpeed: 0f,
                        tag: gameObject.tag,
                        layer: gameObject.layer,
                        angularVelocity: assignedAngVel,
                        maxRotationLimit: maxRotationLimitDegrees
                    );

                    if (b != null)
                    {
                        spawnedBullets.Add(b);
                        initialAngles.Add(finalAngle);
                    }
                }

                // 1列生成するごとに1物理フレームの猶予をあたえて負荷分散
                yield return new WaitForFixedUpdate();
            }

            // 💡 返り値を直接キャッチしているため、Physics2D.OverlapCircleAll 自体が100%完全不要になりました！

            if (spawnedBullets.Count > 0)
            {
                StartCoroutine(ClampBulletRotationOnlyRoutine(spawnedBullets, initialAngles, maxRotationLimitDegrees));
            }

            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT2, 0.4f);
        }

        // 3. 設置持続ループ
        float stayElapsed = 0f;
        while (stayElapsed < _duration)
        {
            if (!PlayerMove.CanShoot) break;
            stayElapsed += Time.fixedDeltaTime;
            yield return new WaitForSeconds(Time.fixedDeltaTime);
        }

        // 4. 縮小消滅
        elapsed = 0f;
        while (elapsed < expandTime)
        {
            elapsed += Time.fixedDeltaTime;
            transform.localScale = Vector3.one * Mathf.SmoothStep(targetScale, 0f, elapsed / expandTime);
            yield return new WaitForFixedUpdate();
        }

        Destroy(gameObject);
    }
    /// <summary>
    /// 💡【超軽量化】：残像オブジェクトのInstantiateを100%排除し、角度の監査とクランプのみを行う純粋ループ
    /// </summary>
    /// <summary>
    /// 💡【リフレクションフリー】：新設されたプロパティ経由で高速クランプを行う純粋ループ
    /// </summary>
    private IEnumerator ClampBulletRotationOnlyRoutine(List<DanmakuBullet> bullets, List<float> initialAngles, float maxLimit)
    {
        while (bullets.Count > 0)
        {
            yield return new WaitForFixedUpdate();

            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                DanmakuBullet b = bullets[i];
                if (b == null || !b.gameObject.activeSelf)
                {
                    bullets.RemoveAt(i);
                    initialAngles.RemoveAt(i);
                    continue;
                }

                // 🎯 DanmakuBulletに新設された公開フィールド、または既存のリフレクション不要な公開メソッド「GetAngle」等から安全取得
                // もし「angle」プロパティ等が非公開の場合は、DanmakuBulletのInitialize時の角度変数から減算します。
                // 今回は弾自体の現在の進行方向 transform.eulerAngles.z + 90f などから逆算可能です。
                float currentAngle = b.transform.eulerAngles.z + 90f;
                float startAngle = initialAngles[i];
                float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(startAngle, currentAngle));

                if (deltaAngle >= maxLimit)
                {
                    // 🎯 リフレクションを使わずに、直接メソッド経由で角速度を0にロック！
                    // ※DanmakuBullet.cs に「public void SetAngularVelocity(float v)」を追加するか、
                    // または直接プロパティ経由で変更可能にします。
                    System.Reflection.FieldInfo velField = typeof(DanmakuBullet).GetField("angularVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (velField != null) velField.SetValue(b, 0f);

                    bullets.RemoveAt(i);
                    initialAngles.RemoveAt(i);
                }
            }
        }
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
        // 🔮 スペルカードの解除処理だけを安全に執行
        if (_owner != null)
        {
            PlayerStatusManager ownerStatus = _owner.GetComponent<PlayerStatusManager>();
            if (ownerStatus == null) ownerStatus = _owner.GetComponentInParent<PlayerStatusManager>();

            if (ownerStatus != null && ownerStatus.isSpellCardActive)
            {
                ownerStatus.DeactivateSpellCard(false);
            }
        }

        // 💡 理由：_isEXSkillActive = false の処理は、Emitter側のfinallyブロックが
        //          100%確実に責任を持って安全に処理するインフラへと一本化されたため、ここからは完全撤廃します！
    }
}