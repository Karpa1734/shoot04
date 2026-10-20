using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class DanmakuBullet : MonoBehaviour
{
    // =========================================================================
    // 🌟 外側から角度や速度を変更・取得するためのパブリックインターフェース
    // =========================================================================
    // 🌟 削り弾フラグの内部保持用
    private bool _isContinuousDamageBullet = false;
    public float Angle
    {
        get => angle;
        set => angle = value;
    }

    public void SetAngle(float newAngle)
    {
        angle = newAngle;
    }

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }

    private SpriteRenderer sr;
    private Collider2D col;
    private Collider2D[] allColliders;
    public GameObject owner { get; private set; }
    private string targetTag;

    [Header("Effect Settings")]
    public GameObject effectPrefab;
    private GameObject activeDelayEffect;

    private BulletData currentData;
    private float speed, angle, accel, maxSpeed, angularVelocity;
    private bool isInitialized = false;
    private bool isActive = true;
    private int delayFrames = 0;
    private int totalDelay = 0;
    private int _bulletAgeFrames = 0;
    private float _initialSpeedForDecel = 0f;
    private bool _isDecelerating = false;
    private float _decelerationProgress = 0f;
    public int DelayFrames => delayFrames;

    private int currentAnimFrame = 0;
    private float animTimer = 0f;
    private bool isAnimated = false;
    private bool isConverging = false;
    private Vector3 initialOffset;
    private bool isGrazeDone = false;

    private bool _isKnifeCounter = false;
    private float _knifeRotateSpeed = 720f;
    private float _knifeCurrentAngle = 0f;
    public bool isMovementSuspended = false;
    private float _selfDestructTimer = -1f;
    private bool _hasSelfDestructTriggered = false;

    [HideInInspector] public bool isIndestructible = false;
    [HideInInspector] public GameObject originPrefab;
    [HideInInspector] public int instanceGenerationId = 0;
    private int _nextHitEnableFrame = 0;
    private const int MULTI_HIT_INTERVAL_FRAMES = 5;

    private bool _isSpearChargeMode = false;
    // 🌟 Xスキル用：全方位ランダム湾曲・減速インフラ変数
    [HideInInspector] public bool useXSkillCurve = false;
    [HideInInspector] public int xDecelStartFrame = 15;       // 減速を開始するフレーム
    [HideInInspector] public float xMinSpeedRatio = 0.6f;    // 速度0までは減らない（元の速度の何割まで減るか、例: 30%）
    [HideInInspector] public float xTargetAngularVelocity = 0f; // この弾が曲がる目標の角速度（度/秒）
    [HideInInspector] public float xCurveAccelDuration = 30f;  // 曲がり始めるまでの滑らかな補間時間
    private float _xInitialSpeedForCurve = 0f;
    private float _currentAngularVelocityForX = 0f;
    private static int _smallOrderCounter = 15000;
    private static int _mediumOrderCounter = 10000;
    private static int _largeOrderCounter = 5000;
    // 🌟 一定角度まで曲がったらまっすぐ進むための追跡用変数
    private float _accumulatedCurveAngle = 0f;
    [HideInInspector] public float xMaxCurveAngle = 60f; // 例: 合計で60度曲がったらまっすぐ進む（インスペクターやEmitterから調整可能）
    private bool _hasReachedMaxCurve = false;
    private int AllocateNextSortingOrder(BulletSize size)
    {
        switch (size)
        {
            case BulletSize.Small:
                _smallOrderCounter++;
                if (_smallOrderCounter > 20000) _smallOrderCounter = 15000;
                return _smallOrderCounter;
            case BulletSize.Medium:
                _mediumOrderCounter++;
                if (_mediumOrderCounter > 15000) _mediumOrderCounter = 10000;
                return _mediumOrderCounter;
            case BulletSize.Large:
                _largeOrderCounter++;
                if (_largeOrderCounter > 10000) _largeOrderCounter = 5000;
                return _largeOrderCounter;
            default:
                return 1000;
        }
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        allColliders = GetComponentsInChildren<Collider2D>(true);
    }

    public void SetColliderActive(bool active)
    {
        if (allColliders == null) return;
        foreach (var c in allColliders)
        {
            if (c != null) c.enabled = active;
        }
    }

    public bool TryGraze()
    {
        if (isGrazeDone) return false;
        isGrazeDone = true;
        return true;
    }

    public void Initialize(GameObject shooter, string target, float speed, float angle, float accel, float maxSpeed, float angVel, float delay, BulletData data, bool converge = false)
    {
        this._bulletAgeFrames = 0;
        this._isDecelerating = false;
        this._decelerationProgress = 0f;
        this.isMovementSuspended = false;
        this._hasSelfDestructTriggered = false;
        this._selfDestructTimer = -1f;
        this.isGrazeDone = false;
        this.instanceGenerationId++;
        // 🌟【修正の核心】：BulletData から削り弾フラグを確実に取得して保持する
        this._isContinuousDamageBullet = (data != null && data.isContinuousDamageBullet);
        Transform oldAura = transform.Find("PureColorAuraObject");
        if (oldAura != null)
        {
            oldAura.name = "DELETED_OLD_AURA_REMNANT";
            Destroy(oldAura.gameObject);
        }
        this._accumulatedCurveAngle = 0f;
        this._hasReachedMaxCurve = false;
        this.useXSkillCurve = false;
        this.xTargetAngularVelocity = 0f;
        this._currentAngularVelocityForX = 0f;
        this.owner = shooter;
        this.targetTag = target;
        this.currentData = data;
        this.speed = speed;
        this.angle = angle;
        this.accel = accel;
        this.maxSpeed = maxSpeed;
        this.angularVelocity = angVel;
        this.originPrefab = data != null ? data.bulletPrefab : null;
        this.delayFrames = Mathf.RoundToInt(delay);
        this.totalDelay = this.delayFrames;
        this.isConverging = converge;
        this._isKnifeCounter = false;
        this._nextHitEnableFrame = 0;

        _isSpearChargeMode = (data != null && (data.name.Contains("Spear") || data.bulletPrefab.name.Contains("Spear")));

        float multiplier = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = Vector3.one * multiplier;

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;
            else sr.material = SpriteCullingFixInfrastrucure();

            sr.sortingOrder = AllocateNextSortingOrder(data.sizeType);

            Color c = sr.color;
            c.a = 1.0f;
            sr.color = c;
        }

        CircleCollider2D circleCol = GetComponent<CircleCollider2D>();
        if (circleCol != null && data != null)
        {
            circleCol.radius = data.radius > 0f ? data.radius : 0.05f;
            circleCol.offset = data.colliderOffset;
        }

        // 🌟【修正】：rotateSpriteWithAngle が true の場合のみ進行方向に回転させる
        bool shouldRotate = (data == null || data.rotateSpriteWithAngle);

        if (!_isSpearChargeMode || delayFrames <= 0)
        {
            if (shouldRotate)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
            else
            {
                transform.rotation = Quaternion.identity; // 回転させない場合は常にデフォルト（上向き等）を維持
            }
        }

        if (data != null && data.animationSprites != null && data.animationSprites.Length > 1)
        {
            isAnimated = true;
            if (sr != null) sr.sprite = data.animationSprites[0];
        }
        else
        {
            isAnimated = false;
            if (sr != null && data != null) sr.sprite = data.bulletSprite;
        }

        SetColliderActive(delayFrames <= 0);

        CreateAuraEffect();

        if (delayFrames > 0)
        {
            if (sr != null) sr.enabled = true;
            StartCoroutine(DelayEffectRoutine(data));
        }
        else
        {
            if (sr != null) sr.enabled = true;
        }

        isInitialized = true;
        isActive = true;
    }

    public void InitializeKnifeCounter(GameObject shooter, string target, float shootSpeed, float delayDuration, BulletData data)
    {
        this._bulletAgeFrames = 0;
        this._isDecelerating = false;
        this._decelerationProgress = 0f;
        this.isMovementSuspended = false;
        this._hasSelfDestructTriggered = false;
        this._selfDestructTimer = -1f;
        this.isGrazeDone = false;
        this.instanceGenerationId++;
        // 🌟【修正の核心】：BulletData から削り弾フラグを確実に取得して保持する
        this._isContinuousDamageBullet = (data != null && data.isContinuousDamageBullet);
        Transform oldAura = transform.Find("PureColorAuraObject");
        if (oldAura != null)
        {
            oldAura.name = "DELETED_OLD_AURA_REMNANT";
            Destroy(oldAura.gameObject);
        }
        this._accumulatedCurveAngle = 0f;
        this._hasReachedMaxCurve = false;
        this.useXSkillCurve = false;
        this.xTargetAngularVelocity = 0f;
        this._currentAngularVelocityForX = 0f;
        this.owner = shooter;
        this.targetTag = target;
        this.currentData = data;
        this.speed = shootSpeed;
        this.accel = 0;
        this.maxSpeed = shootSpeed;
        this.angularVelocity = 0;
        this.isIndestructible = false;
        this._nextHitEnableFrame = 0;
        this.originPrefab = data != null ? data.bulletPrefab : null;
        this.delayFrames = Mathf.RoundToInt(delayDuration * 60f);
        this.totalDelay = this.delayFrames;
        this.isConverging = false;
        this._isKnifeCounter = true;
        this.isAnimated = false;
        this._isSpearChargeMode = false;

        float multiplierKn = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = Vector3.one * multiplierKn;

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;
            else sr.material = SpriteCullingFixInfrastrucure();

            sr.sortingOrder = AllocateNextSortingOrder(data.sizeType);

            Color c = sr.color;
            c.a = 1.0f;
            sr.color = c;
        }

        CircleCollider2D circleCol = GetComponent<CircleCollider2D>();
        if (circleCol != null && data != null)
        {
            circleCol.radius = data.radius > 0f ? data.radius : 0.05f;
            circleCol.offset = data.colliderOffset;
        }

        _knifeCurrentAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0, 0, _knifeCurrentAngle - 90f);

        if (sr != null) sr.enabled = true;
        SetColliderActive(false);

        CreateAuraEffect();

        isInitialized = true;
        isActive = true;
    }

    private Material SpriteCullingFixInfrastrucure()
    {
        return Shader.Find("Sprites/Default") != null
            ? Canvas.GetDefaultCanvasMaterial()
            : new Material(Shader.Find("Sprites/Default"));
    }

    private void CreateAuraEffect()
    {
        if (transform.Find("PureColorAuraObject") != null) return;
        if (sr == null || currentData == null) return;

        GameObject auraChild = new GameObject("PureColorAuraObject");
        auraChild.transform.SetParent(transform);
        auraChild.transform.localPosition = Vector3.zero;
        auraChild.transform.localRotation = Quaternion.identity;

        float targetScaleX = 1.5f;
        float targetScaleY = 1.5f;

        if (_isSpearChargeMode)
        {
            targetScaleY = 1.2f;
            targetScaleX = 1.2f;
        }

        float parentScaleX = transform.localScale.x != 0 ? transform.localScale.x : 1f;
        float parentScaleY = transform.localScale.y != 0 ? transform.localScale.y : 1f;

        auraChild.transform.localScale = new Vector3(targetScaleX / parentScaleX, targetScaleY / parentScaleY, 1.0f);

        SpriteRenderer auraSR = auraChild.AddComponent<SpriteRenderer>();
        auraSR.sortingLayerID = sr.sortingLayerID;
        auraSR.sortingOrder = sr.sortingOrder - 1;

        if (currentData.auraMaterial != null) auraSR.material = currentData.auraMaterial;
        else auraSR.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));

        auraSR.sprite = (currentData.auraWhiteSprite != null) ? currentData.auraWhiteSprite : sr.sprite;

        PlayerStatusManager myStatus = null;
        if (owner != null) myStatus = owner.GetComponent<PlayerStatusManager>();
        if (myStatus == null && owner != null) myStatus = owner.GetComponentInParent<PlayerStatusManager>();

        int ownerId = (myStatus != null) ? myStatus.playerId : 1;
        Color auraColor = (myStatus != null && myStatus.characterData != null) ? myStatus.characterData.imageColor : ((ownerId == 1) ? Color.cyan : Color.red);
        auraColor.a = _isSpearChargeMode ? 0.8f : 0.6f;
        auraSR.color = auraColor;
    }

    public void StartSelfDestructTimer(float duration)
    {
        _selfDestructTimer = duration;
        _hasSelfDestructTriggered = false;
    }

    void FixedUpdate()
    {
        if (!isInitialized || !isActive) return;
        if (isAnimated) UpdateAnimation();

        _bulletAgeFrames++;

        if (_selfDestructTimer > 0f)
        {
            _selfDestructTimer -= Time.fixedDeltaTime;
            if (_selfDestructTimer <= 0f && !_hasSelfDestructTriggered)
            {
                _hasSelfDestructTriggered = true;
                _selfDestructTimer = -1f;
                Deactivate(true, force: true);
                return;
            }
        }

        if (delayFrames > 0)
        {
            if (_isKnifeCounter)
            {
                float dt = Time.fixedDeltaTime;
                _knifeCurrentAngle += _knifeRotateSpeed * dt;
                transform.rotation = Quaternion.Euler(0, 0, _knifeCurrentAngle - 90f);
            }
            else if (isConverging && owner != null)
            {
                float t = 1f / delayFrames;
                transform.position = Vector3.Lerp(transform.position, owner.transform.position, t);
            }

            if (_isSpearChargeMode && totalDelay > 0)
            {
                float progress = 1f - ((float)delayFrames / totalDelay);
                float currentXRotation = Mathf.Lerp(90f, 0f, progress);
                transform.localRotation = Quaternion.Euler(currentXRotation, 0f, angle - 90f);
            }

            delayFrames--;
            if (delayFrames <= 0)
            {
                sr.enabled = true;
                SetColliderActive(true);
                if (activeDelayEffect != null) Destroy(activeDelayEffect);

                if (_isSpearChargeMode)
                {
                    transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                }

                if (_isKnifeCounter)
                {
                    angle = GetAngleToTarget();
                    transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
                    CreateAuraEffect();
                }
            }
            return;
        }

        if (isMovementSuspended) return;

        float dtMove = Time.fixedDeltaTime;

        // =========================================================================
        // 🌟【Xスキル用】：発射後の減速 ＆ セットごとに変化するランダム曲がり挙動
        // =========================================================================
        if (useXSkillCurve)
        {
            if (_bulletAgeFrames == 1)
            {
                _xInitialSpeedForCurve = speed > 0f ? speed : 5f; // 初速を安全に記録
                _accumulatedCurveAngle = 0f;
                _hasReachedMaxCurve = false;
            }

            // 指定フレーム以降から減速と湾曲を開始
            if (_bulletAgeFrames >= xDecelStartFrame)
            {
                // 1. 速度0までは減らないように下限（minSpeed）を死守して減速
                float minSpeed = Mathf.Max(1.5f, _xInitialSpeedForCurve * xMinSpeedRatio);
                if (speed > minSpeed)
                {
                    speed -= 1.5f * dtMove;
                    if (speed < minSpeed) speed = minSpeed;
                }

                // 2. 一定角度（xMaxCurveAngle）に達していない場合のみ曲げる
                if (!_hasReachedMaxCurve)
                {
                    // 目標の角速度に向かって滑らかに曲げる
                    _currentAngularVelocityForX = Mathf.MoveTowards(_currentAngularVelocityForX, xTargetAngularVelocity, 10f * dtMove);

                    float frameAngleDelta = _currentAngularVelocityForX * dtMove;
                    angle += frameAngleDelta;

                    // 累積でどれくらい曲がったかを絶対値で加算追跡
                    _accumulatedCurveAngle += Mathf.Abs(frameAngleDelta);

                    // 指定した最大曲げ角度（例: 60度）に到達したら、角速度を0にして以降はまっすぐ進める
                    if (_accumulatedCurveAngle >= xMaxCurveAngle)
                    {
                        _hasReachedMaxCurve = true;
                        xTargetAngularVelocity = 0f; // 目標角速度を0にして直進状態へ移行
                    }
                }
                else
                {
                    // 最大角度到達後は、角速度を滑らかに0に戻して直進を維持
                    _currentAngularVelocityForX = Mathf.MoveTowards(_currentAngularVelocityForX, 0f, 20f * dtMove);
                    angle += _currentAngularVelocityForX * dtMove;
                }
            }
        }
        // =========================================================================
        // =========================================================================
        // =========================================================================

        if (currentData != null && currentData.useRangeDeceleration)
        {
            int startFrame = currentData.decelerationStartFrame;
            int durationFrames = Mathf.Max(1, currentData.decelerationDurationFrames);

            if (_bulletAgeFrames >= startFrame)
            {
                if (!_isDecelerating)
                {
                    _isDecelerating = true;
                    _initialSpeedForDecel = speed;
                }

                float decelElapsed = _bulletAgeFrames - startFrame;
                _decelerationProgress = Mathf.Clamp01(decelElapsed / durationFrames);

                speed = Mathf.Lerp(_initialSpeedForDecel, 0f, _decelerationProgress);

                float fadeAlpha = Mathf.Lerp(1.0f, 0f, _decelerationProgress);

                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = fadeAlpha;
                    sr.color = c;
                }

                Transform auraChild = transform.Find("PureColorAuraObject");
                if (auraChild != null)
                {
                    SpriteRenderer auraSR = auraChild.GetComponent<SpriteRenderer>();
                    if (auraSR != null)
                    {
                        Color ac = auraSR.color;
                        ac.a = fadeAlpha * 0.6f;
                        auraSR.color = ac;
                    }
                }

                if (_decelerationProgress >= 1.0f || speed <= 0.01f)
                {
                    Deactivate(false, force: true);
                    return;
                }
            }
        }

        angle += angularVelocity * dtMove * 60f;
        speed += accel * dtMove * 60f;
        if (accel != 0 && speed > maxSpeed) speed = maxSpeed;

        float rad = angle * Mathf.Deg2Rad;
        transform.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * speed * dtMove;

        // 🌟【修正の核心】：回転設定が有効な弾だけ、進行方向（angle）に合わせて回転を更新する
        bool shouldRotate = (currentData == null || currentData.rotateSpriteWithAngle);
        if (shouldRotate)
        {
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }
        else
        {
            transform.rotation = Quaternion.identity; // 回転させない弾は常にまっすぐ固定
        }


        if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
        {
            Deactivate(false, force: true);
        }
    }

    private float GetAngleToTarget()
    {
        Transform target = null;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != owner)
            {
                target = p.transform;
                break;
            }
        }
        if (target != null)
        {
            Vector3 dir = target.position - transform.position;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        return 0f;
    }

    private void UpdateAnimation()
    {
        animTimer += Time.fixedDeltaTime;
        float frameDuration = 1f / currentData.animationFPS;

        if (animTimer >= frameDuration)
        {
            animTimer = 0f;
            currentAnimFrame = (currentAnimFrame + 1) % currentData.animationSprites.Length;
            sr.sprite = currentData.animationSprites[currentAnimFrame];

            Transform auraChild = transform.Find("PureColorAuraObject");
            if (auraChild != null)
            {
                SpriteRenderer childSR = auraChild.GetComponent<SpriteRenderer>();
                if (childSR != null) childSR.sprite = sr.sprite;
            }
        }
    }

    private IEnumerator DelayEffectRoutine(BulletData data)
    {
        if (_isSpearChargeMode) yield break;

        if (effectPrefab != null && data.delaySprite != null)
        {
            activeDelayEffect = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            activeDelayEffect.transform.SetParent(this.transform);

            SpriteRenderer effSr = activeDelayEffect.GetComponent<SpriteRenderer>();
            if (effSr != null)
            {
                effSr.sprite = data.delaySprite;
                effSr.sortingOrder = sr.sortingOrder + 1;
            }

            ShotEffect logic = activeDelayEffect.GetComponent<ShotEffect>();
            if (logic != null)
            {
                StartCoroutine(logic.PlayDelay((float)delayFrames / 60f, data.delaySprite, transform.localScale.x));
            }
        }
        yield return null;
    }

    private void HandleHitCollisionLogic(Collider2D collision)
    {
        if (!isInitialized || owner == null || !isActive) return;

        // 自分自身や自分の子オブジェクトの判定には当たらないようにする
        if (collision.gameObject == owner || collision.transform.IsChildOf(owner.transform)) return;

        PlayerStatusManager statusMgr = collision.GetComponent<PlayerStatusManager>();
        if (statusMgr == null) statusMgr = collision.GetComponentInParent<PlayerStatusManager>();

        PlayerHitHandler hitHandler = collision.GetComponent<PlayerHitHandler>();
        if (hitHandler == null) hitHandler = collision.GetComponentInParent<PlayerHitHandler>();

        // 🌟 A. 【削り弾（継続ダメージ弾）の処理】
        // 削り弾は相手のどこに触れても（領域や判定を問わず）継続的にダメージを与え続けます
        if (_isContinuousDamageBullet)
        {
            if (collision.CompareTag(targetTag) || statusMgr != null)
            {
                if (statusMgr != null)
                {
                    if (hitHandler != null)
                    {
                        hitHandler.OnHitIgnoreInvincibility(currentData.damage);
                    }
                    else
                    {
                        statusMgr.ApplyDamage(currentData.damage);
                    }
                }
            }
            return;
        }

        // 🌟 B. 【通常弾の処理】
        // 通常弾は、相手の指定されたターゲットタグ（例: "EnemyBullet" や "PlayerBullet" など）を持つオブジェクト、
        // または明確なヒットボックス（Hitboxタグ等）に接触したときだけ命中とみなして消滅させる
        if (collision.CompareTag(targetTag))
        {
            if (statusMgr != null && statusMgr.gameObject != owner)
            {
                if (Time.frameCount >= _nextHitEnableFrame)
                {
                    _nextHitEnableFrame = Time.frameCount + MULTI_HIT_INTERVAL_FRAMES;

                    collision.SendMessage("OnHit", currentData.damage, SendMessageOptions.DontRequireReceiver);
                    statusMgr.SendMessage("OnHit", currentData.damage, SendMessageOptions.DontRequireReceiver);

                    if (!isIndestructible)
                    {
                        Deactivate(true);
                    }
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision) { HandleHitCollisionLogic(collision); }
    private void OnTriggerStay2D(Collider2D collision) { HandleHitCollisionLogic(collision); }

    public void Deactivate(bool playBreakEffect, bool force = false)
    {
        if (isIndestructible && !force) return;

        isActive = false;
        if (activeDelayEffect != null) Destroy(activeDelayEffect);

        if (Mathf.Abs(transform.position.x) > 9.5f || Mathf.Abs(transform.position.y) > 9.5f)
        {
            playBreakEffect = false;
        }

        if (playBreakEffect && effectPrefab != null && currentData != null)
        {
            GameObject eff = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            SpriteRenderer effSr = eff.GetComponent<SpriteRenderer>();
            if (effSr != null && sr != null) effSr.sortingOrder = sr.sortingOrder + 1;

            ShotEffect logic = eff.GetComponent<ShotEffect>();
            if (logic != null)
                logic.StartCoroutine(logic.PlayBreakAnimation(currentData.breakColor, transform.localScale.x));
        }

        SetColliderActive(false);
        transform.rotation = Quaternion.identity;

        if (originPrefab != null && BulletPool.Instance != null)
        {
            BulletPool.Instance.Release(originPrefab, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void Deactivate(bool playBreakEffect) { Deactivate(playBreakEffect, force: false); }
    public void Deactivate() { Deactivate(false, force: false); }
}