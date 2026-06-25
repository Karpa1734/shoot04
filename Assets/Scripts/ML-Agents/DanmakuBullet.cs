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
    // 🧬【槍弾チャージ拡張】：槍専用の内部ワーク変数
    // =========================================================================
    private bool _isSpearChargeMode = false;

    // =========================================================================
    // 📊【上限閾値・完全修復】：小さい弾が最前面（15000～20000）を絶対死守！
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
        this.delayFrames = Mathf.RoundToInt(delay * 60f); // 🛠️ 秒数からフレーム数へ確実にマッピング修正
        this.totalDelay = this.delayFrames;
        this.isConverging = converge;
        this._isKnifeCounter = false;

        // 🎯【槍識別判定】：データ名やプレハブ名に「Spear」が含まれているかをスマートに判定
        _isSpearChargeMode = (data != null && (data.name.Contains("Spear") || data.bulletPrefab.name.Contains("Spear")));

        bool isCustomPrefabBullet = (GetComponent<BoxCollider2D>() != null || transform.childCount > 0);
        Vector3 templateScale = isCustomPrefabBullet ? transform.localScale : Vector3.one;

        float multiplier = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(templateScale.x * multiplier, templateScale.y * multiplier, templateScale.z * multiplier);

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;

            // 📊 サイズ別のオーダーを強制上書きパッキング！
            sr.sortingOrder = AllocateNextSortingOrder(data.sizeType);
        }

        // 🛠️ 初期回転の設定：槍モードなら最初はX回転を90度にしてペラペラ（非表示に近い）状態にする
        if (_isSpearChargeMode && delay > 0f)
        {
            transform.localRotation = Quaternion.Euler(90f, 0f, angle - 90f);
        }
        else
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

        if (col != null && col is CircleCollider2D circleCol && data != null)
        {
            circleCol.radius = data.radius;
            circleCol.offset = data.colliderOffset;
        }

        SetColliderActive(delay <= 0);

        if (delay > 0)
        {
            if (sr != null) sr.enabled = true; // 💡 槍の起き上がりを見せたいので、ディレイ中も表示はONにする
            StartCoroutine(DelayEffectRoutine(delay, data));
        }
        else
        {
            if (sr != null) sr.enabled = true;
            CreateAuraEffect();
        }

        // 🔄【オーラ同期セーフティ】
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
        this._isSpearChargeMode = false;

        bool isCustomPrefabBullet = (GetComponent<BoxCollider2D>() != null || transform.childCount > 0);
        Vector3 templateScaleKn = isCustomPrefabBullet ? transform.localScale : Vector3.one;

        float multiplierKn = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(templateScaleKn.x * multiplierKn, templateScaleKn.y * multiplierKn, templateScaleKn.z * multiplierKn);

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;
            sr.sortingOrder = AllocateNextSortingOrder(data.sizeType);
        }

        _knifeCurrentAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0, 0, _knifeCurrentAngle - 90f);

        if (sr != null) sr.enabled = true;
        SetColliderActive(false);

        Transform oldAura = transform.Find("PureColorAuraObject");
        if (oldAura != null) Destroy(oldAura.gameObject);

        CreateAuraEffect();

        isInitialized = true;
        isActive = true;
        isGrazeDone = false;
    }

    // =========================================================================
    // ✨【オーラ歪み調停インフラ】：親の偏った長さに引きずられない正円オーラを生成
    // =========================================================================
    private void CreateAuraEffect()
    {
        if (transform.Find("PureColorAuraObject") != null) return;
        if (sr == null || currentData == null) return;

        GameObject auraChild = new GameObject("PureColorAuraObject");
        auraChild.transform.SetParent(transform);
        auraChild.transform.localPosition = Vector3.zero;
        auraChild.transform.localRotation = Quaternion.identity;

        // 🛠️ 歪み対策の核心：親（槍本体）のXとYのスケール比率の偏りを逆算（パージ）し、
        //    ゲーム画面上で完全に均等（1.4倍の綺麗な正円・楕円にならない形）になるようアライメントします。
        float parentScaleX = transform.localScale.x != 0 ? transform.localScale.x : 1f;
        float parentScaleY = transform.localScale.y != 0 ? transform.localScale.y : 1f;
        auraChild.transform.localScale = new Vector3(1.5f / parentScaleX, 1.5f / parentScaleY, 1.0f);

        SpriteRenderer auraSR = auraChild.AddComponent<SpriteRenderer>();
        auraSR.sortingLayerID = sr.sortingLayerID;
        auraSR.sortingOrder = sr.sortingOrder - 1; // 本体スプライトの直下

        if (currentData.auraMaterial != null) auraSR.material = currentData.auraMaterial;
        else auraSR.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));

        // もし槍専用の綺麗な正円オーラ画像（丸型の白い光など）があればauraWhiteSpriteに登録してください。未設定ならスプライトを流用
        auraSR.sprite = (currentData.auraWhiteSprite != null) ? currentData.auraWhiteSprite : sr.sprite;

        PlayerStatusManager myStatus = null;
        if (owner != null) myStatus = owner.GetComponent<PlayerStatusManager>();
        if (myStatus == null && owner != null) myStatus = owner.GetComponentInParent<PlayerStatusManager>();

        int ownerId = (myStatus != null) ? myStatus.playerId : 1;
        Color auraColor = (myStatus != null && myStatus.characterData != null) ? myStatus.characterData.imageColor : ((ownerId == 1) ? Color.cyan : Color.red);
        auraColor.a = 0.6f; // 少し透明度を上げてまばゆさを調整
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

            // =========================================================================
            // 🎯【核心修正】：槍弾のX回転起き上がりチャージ演算（90度 ➔ 0度）
            // =========================================================================
            if (_isSpearChargeMode && totalDelay > 0)
            {
                float progress = 1f - ((float)delayFrames / totalDelay); // 0.0 ➔ 1.0

                // 90度からスタートして0度（完全フラットな起き上がり状態）へLerp
                float currentXRotation = Mathf.Lerp(90f, 0f, progress);
                transform.localRotation = Quaternion.Euler(currentXRotation, 0f, angle - 90f);
            }

            delayFrames--;
            if (delayFrames <= 0)
            {
                sr.enabled = true;
                SetColliderActive(true);
                if (activeDelayEffect != null) Destroy(activeDelayEffect);

                // チャージ完了に伴い完全フラットな回転に固定
                if (_isSpearChargeMode)
                {
                    transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                }

                if (_isKnifeCounter)
                {
                    angle = GetAngleToTarget();
                    transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
                }
                else
                {
                    CreateAuraEffect();
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
        // 🛠️ 槍チャージモードの時は大元の丸型魔法陣の生成を完全にスキップ！
        if (_isSpearChargeMode)
        {
            yield break;
        }

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