using System;
using System.Collections;
using UnityEngine;
// 🔥 単に「Random」と書いたらUnity側を優先
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
    // 📊【移設・新設】：サイズごとのソーティングオーダー動的分配インフラカウンター
    // =========================================================================
    private static int _smallOrderCounter = 15000;
    private static int _mediumOrderCounter = 10000;
    private static int _largeOrderCounter = 5000;

    private int AllocateNextSortingOrder(BulletSize size)
    {
        switch (size)
        {
            case BulletSize.Small:
                _smallOrderCounter++;
                if (_smallOrderCounter > 10000) _smallOrderCounter = 5000;
                return _smallOrderCounter;

            case BulletSize.Medium:
                _mediumOrderCounter++;
                if (_mediumOrderCounter > 15000) _mediumOrderCounter = 10000;
                return _mediumOrderCounter;

            case BulletSize.Large:
                _largeOrderCounter++;
                if (_largeOrderCounter > 20000) _largeOrderCounter = 15000;
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

    // 🟢 通常の弾幕・槍弾幕の発射初期化
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
        this._isKnifeCounter = false;

        bool isCustomPrefabBullet = (GetComponent<BoxCollider2D>() != null || transform.childCount > 0);
        Vector3 templateScale = isCustomPrefabBullet ? transform.localScale : Vector3.one;

        float multiplier = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(templateScale.x * multiplier, templateScale.y * multiplier, templateScale.z * multiplier);

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;

            // 📊【核心】：プールから出撃したこの瞬間に、サイズ別のオーダーを強制上書きパッキング！
            sr.sortingOrder = AllocateNextSortingOrder(data.sizeType);
        }

        transform.rotation = Quaternion.Euler(0, 0, angle);

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

        if (col != null && col is CircleCollider2D circleCol && data != null)
        {
            circleCol.radius = data.radius;
            circleCol.offset = data.colliderOffset;
        }

        SetColliderActive(delay <= 0);

        if (delay > 0)
        {
            if (sr != null) sr.enabled = false;
            StartCoroutine(DelayEffectRoutine(delay, data));
        }
        else
        {
            if (sr != null) sr.enabled = true;
        }

        // 🔄【オーラ同期セーフティ】：もし前回の使い回しオーラが残っていれば、新しいオーダーの真後ろ（-1）へ即座に再配置
        Transform auraChild = transform.Find("PureColorAuraObject");
        if (auraChild != null)
        {
            SpriteRenderer auraSR = auraChild.GetComponent<SpriteRenderer>();
            if (auraSR != null && sr != null)
            {
                auraSR.sortingOrder = sr.sortingOrder - 1;
            }
        }

        isInitialized = true;
        isActive = true;
        isGrazeDone = false;
    }

    // 🔵 カウンターナイフ専用の初期化メソッド
    public void InitializeKnifeCounter(GameObject shooter, string target, float shootSpeed, float delayDuration, BulletData data)
    {
        this.owner = shooter;
        this.targetTag = target;
        this.currentData = data;
        this.speed = shootSpeed;
        this.accel = 0;
        this.maxSpeed = shootSpeed;
        this.angularVelocity = 0;
        this.delayFrames = Mathf.RoundToInt(delayDuration * 60f);
        this.totalDelay = this.delayFrames;
        this.isConverging = false;
        this._isKnifeCounter = true;
        this.isAnimated = false;

        bool isCustomPrefabBullet = (GetComponent<BoxCollider2D>() != null || transform.childCount > 0);
        Vector3 templateScaleKn = isCustomPrefabBullet ? transform.localScale : Vector3.one;

        float multiplierKn = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(templateScaleKn.x * multiplierKn, templateScaleKn.y * multiplierKn, templateScaleKn.z * multiplierKn);

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;

            // 📊 カウンターナイフ側でも出撃オーダーを強制再分配！
            sr.sortingOrder = AllocateNextSortingOrder(data.sizeType);
        }

        _knifeCurrentAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0, 0, _knifeCurrentAngle - 90f);

        if (sr != null) sr.enabled = true;
        SetColliderActive(false);

        Transform oldAura = transform.Find("PureColorAuraObject");
        if (oldAura != null) Destroy(oldAura.gameObject);

        if (sr != null && data != null)
        {
            GameObject auraChild = new GameObject("PureColorAuraObject");
            auraChild.transform.SetParent(transform);
            auraChild.transform.localPosition = Vector3.zero;
            auraChild.transform.localRotation = Quaternion.identity;
            auraChild.transform.localScale = new Vector3(1.4f, 1.4f, 1.0f);

            SpriteRenderer auraSR = auraChild.AddComponent<SpriteRenderer>();
            auraSR.sortingLayerID = sr.sortingLayerID;
            auraSR.sortingOrder = sr.sortingOrder - 1; // 親の後ろ

            auraSR.material = (data.auraMaterial != null) ? data.auraMaterial : new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            auraSR.sprite = (data.auraWhiteSprite != null) ? data.auraWhiteSprite : sr.sprite;

            PlayerStatusManager myStatus = shooter.GetComponent<PlayerStatusManager>();
            if (myStatus == null) myStatus = shooter.GetComponentInParent<PlayerStatusManager>();

            if (myStatus != null && myStatus.characterData != null)
            {
                Color charImageColor = myStatus.characterData.imageColor;
                charImageColor.a = 1.0f;
                auraSR.color = charImageColor;
            }
            else
            {
                Color defaultColor = Color.yellow;
                defaultColor.a = 1.0f;
                auraSR.color = defaultColor;
            }
        }

        isInitialized = true;
        isActive = true;
        isGrazeDone = false;
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
                Deactivate(true);
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

            delayFrames--;
            if (delayFrames <= 0)
            {
                sr.enabled = true;
                SetColliderActive(true);
                if (activeDelayEffect != null) Destroy(activeDelayEffect);

                if (_isKnifeCounter)
                {
                    angle = GetAngleToTarget();
                    transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
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
            Deactivate(false);
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

    private IEnumerator DelayEffectRoutine(float delay, BulletData data)
    {
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
            collision.SendMessage("OnHit", currentData.damage, SendMessageOptions.DontRequireReceiver);
            Deactivate(true);
        }
    }

    public void Deactivate(bool playBreakEffect)
    {
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

        if (currentData != null && currentData.bulletPrefab != null && BulletPool.Instance != null)
        {
            BulletPool.Instance.Release(currentData.bulletPrefab, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Deactivate()
    {
        Deactivate(false);
    }
}