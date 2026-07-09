using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class DanmakuBullet : MonoBehaviour
{
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

    // =========================================================================
    // 🧬【耐久貫通・多段ヒットインフラ】：敵やフィールドに接触しても消えないフラグ
    // =========================================================================
    [HideInInspector] public bool isIndestructible = false;
    [HideInInspector] public GameObject originPrefab;
    // 🌟【最核心新設】：プール再利用時に前回の世代のコルーチンからの誤消去命令を物理遮断するID
    [HideInInspector] public int instanceGenerationId = 0;
    private int _nextHitEnableFrame = 0;
    private const int MULTI_HIT_INTERVAL_FRAMES = 5;

    private bool _isSpearChargeMode = false;

    private static int _smallOrderCounter = 15000;
    private static int _mediumOrderCounter = 10000;
    private static int _largeOrderCounter = 5000;

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
        this.isMovementSuspended = false;
        this._hasSelfDestructTriggered = false;
        this._selfDestructTimer = -1f;
        this.isGrazeDone = false;
        this.instanceGenerationId++;

        // =========================================================================
        // 🎯【最核心バグ修正①】：前世代のオーラ残骸の「即時強制リネームパージ」
        // =========================================================================
        // 💡 理由：Destroy()はフレーム終了時までオブジェクトが残るため、Findで見つかってしまいます。
        //    見つかった古いオーラの名前をその場で全く別の名前に書き換えて「パージ」することで、
        //    後半の CreateAuraEffect() が古い死体を誤検知して生成を拒否するバグを100%回避します。
        Transform oldAura = transform.Find("PureColorAuraObject");
        if (oldAura != null)
        {
            oldAura.name = "DELETED_OLD_AURA_REMNANT"; // 👈 即座に隠れみのを剥がす
            Destroy(oldAura.gameObject);
        }

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

        bool isCustomPrefabBullet = (GetComponent<BoxCollider2D>() != null || transform.childCount > 0);
        Vector3 templateScale = isCustomPrefabBullet ? transform.localScale : Vector3.one;

        float multiplier = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(templateScale.x * multiplier, templateScale.y * multiplier, templateScale.z * multiplier);

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;
            else sr.material = SpriteCullingFixInfrastrucure();

            sr.sortingOrder = AllocateNextSortingOrder(data.sizeType);
        }

        if (!_isSpearChargeMode || delayFrames <= 0)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
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

        // =========================================================================
        // 🎯【最核心バグ修正②】：ディレイの有無に関わらず、生まれたその瞬間にオーラを新造！
        // =========================================================================
        // 💡 理由：カウントダウン終了後（数フレーム後）にオーラ生成を後回しにすると、
        //    高速連射時に同期がズレてハゲる原因になります。生まれた瞬間に100%オーラを纏わせます。
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
        this.isMovementSuspended = false;
        this._hasSelfDestructTriggered = false;
        this._selfDestructTimer = -1f;
        this.isGrazeDone = false;
        this.instanceGenerationId++;

        // ナイフカウンター側も同様に残骸の即時リネーム名指しパージを実行
        Transform oldAura = transform.Find("PureColorAuraObject");
        if (oldAura != null)
        {
            oldAura.name = "DELETED_OLD_AURA_REMNANT";
            Destroy(oldAura.gameObject);
        }

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

        bool isCustomPrefabBullet = (GetComponent<BoxCollider2D>() != null || transform.childCount > 0);
        Vector3 templateScaleKn = isCustomPrefabBullet ? transform.localScale : Vector3.one;

        float multiplierKn = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(templateScaleKn.x * multiplierKn, templateScaleKn.y * multiplierKn, templateScaleKn.z * multiplierKn);

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;
            else sr.material = SpriteCullingFixInfrastrucure();

            sr.sortingOrder = AllocateNextSortingOrder(data.sizeType);
        }

        _knifeCurrentAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0, 0, _knifeCurrentAngle - 90f);

        if (sr != null) sr.enabled = true;
        SetColliderActive(false);

        // カウンターも生まれた瞬間にオーラ新造を絶対保証
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
        // 💡 名前の書き換えパージを行っているため、古い死体と衝突せず確実にここを突破します
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
                    CreateAuraEffect(); // 💡 カウンターナイフが射出される瞬間にも確実に結合を保証
                }
            }
            return;
        }

        if (isMovementSuspended) return;

        float dtMove = Time.fixedDeltaTime;
        angle += angularVelocity * dtMove * 60f;
        speed += accel * dtMove * 60f;
        if (accel != 0 && speed > maxSpeed) speed = maxSpeed;

        float rad = angle * Mathf.Deg2Rad;
        transform.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * speed * dtMove;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

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
        if (collision.gameObject == owner || collision.transform.IsChildOf(owner.transform)) return;

        if (collision.CompareTag(targetTag))
        {
            if (Time.frameCount >= _nextHitEnableFrame)
            {
                _nextHitEnableFrame = Time.frameCount + MULTI_HIT_INTERVAL_FRAMES;
                collision.SendMessage("OnHit", currentData.damage, SendMessageOptions.DontRequireReceiver);

                if (!isIndestructible)
                {
                    Deactivate(true);
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