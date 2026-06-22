using System.Collections;
using UnityEngine;

public class DanmakuBullet : MonoBehaviour
{
    private SpriteRenderer sr;
    private Collider2D col;                  // 単発アクセス用のフォールバックキャッシュ
    private Collider2D[] allColliders;       // 💡【不足修復】：親Box＋子Circleを一網打尽にする配列枠
    public GameObject owner { get; private set; }
    private string targetTag;

    [Header("Effect Settings")]
    public GameObject effectPrefab; // ShotEffectが付いているプレハブを指定
    private GameObject activeDelayEffect;

    private BulletData currentData;
    private float speed, angle, accel, maxSpeed, angularVelocity;
    private bool isInitialized = false;
    private bool isActive = true;
    private int delayFrames = 0;
    private int totalDelay = 0;
    // ★ アニメーション用変数
    private int currentAnimFrame = 0;
    private float animTimer = 0f;
    private bool isAnimated = false;
    // 収束用フラグ
    private bool isConverging = false;
    private Vector3 initialOffset;
    private bool isGrazeDone = false; // ★ 追加：グレイズ済みフラグ

    // ★ 追加：カウンターナイフ用の特殊状態変数
    private bool _isKnifeCounter = false;
    private float _knifeRotateSpeed = 720f; // 1秒間に720度（0.5秒で1回転）
    private float _knifeCurrentAngle = 0f;
    // 🌟 追加：EXスキルなどのホスト制御コルーチンから自動移動を完全停止させるスイッチ
    public bool isMovementSuspended = false;
    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        // 💡【不足修復】：自分自身（親オブジェクト）と、ぶら下がっている子オブジェクトのすべてのコライダーを一括取得！
        allColliders = GetComponentsInChildren<Collider2D>(true);
    }

    public void SetColliderActive(bool active)
    {
        if (allColliders == null) return;

        // 配列に入っているすべてのコライダー（親のBoxも、子のCircleも）を一括処理
        foreach (var c in allColliders)
        {
            if (c != null) c.enabled = active;
        }
    }
    /// <summary>
    /// グレイズ判定を試みる。1回目だけ true を返す。
    /// </summary>
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

        // =========================================================================
        // 🎯【スマートサイズ判定】：槍プレハブの長細い比率を自動で保護・引き継ぐ
        // =========================================================================
        // 💡 仕組み：親にBoxCollider2Dがある、または子オブジェクトが存在する場合は「槍（例外弾）」と自動認識します。
        //          槍の場合は「プレハブ本来の比率（例: 縦長）」を基準にし、通常弾の場合は「Vector3.one」を基準にリセットします。
        bool isCustomPrefabBullet = (GetComponent<BoxCollider2D>() != null || transform.childCount > 0);
        Vector3 templateScale = isCustomPrefabBullet ? transform.localScale : Vector3.one;

        float multiplier = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(templateScale.x * multiplier, templateScale.y * multiplier, templateScale.z * multiplier);

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;
        }

        // 💡 進行方向へグラフィックを自動回転
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // ★ アニメーションの有無を確認
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

        // 通常弾プレハブ（CircleCollider2D）の場合のみデータ駆動でコライダーの半径を決定
        if (col != null && col is CircleCollider2D circleCol && data != null)
        {
            circleCol.radius = data.radius;
            circleCol.offset = data.colliderOffset;
        }

        // 出撃の瞬間に、すべてのコライダーを完全にONにする
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

        isInitialized = true;
        isActive = true;
        isGrazeDone = false;
    }

    // 🔵 カウンターナイフ専用の初期化メソッド（サイズ汚染 ＆ プレハブ比率潰れバグを完全根治）
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

        // =========================================================================
        // 🎯【スマートサイズ判定】：カウンターナイフ側でも槍やプレハブの形を自動保護
        // =========================================================================
        bool isCustomPrefabBullet = (GetComponent<BoxCollider2D>() != null || transform.childCount > 0);
        Vector3 templateScaleKn = isCustomPrefabBullet ? transform.localScale : Vector3.one;

        float multiplierKn = (data != null && data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(templateScaleKn.x * multiplierKn, templateScaleKn.y * multiplierKn, templateScaleKn.z * multiplierKn);

        if (sr != null && data != null)
        {
            sr.sprite = data.bulletSprite;
            if (data.material != null) sr.material = data.material;
        }

        _knifeCurrentAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0, 0, _knifeCurrentAngle - 90f);

        if (sr != null) sr.enabled = true;

        // ナイフ待機中は物理当たり判定を一律すべて眠らせる
        SetColliderActive(false);

        // カウンターナイフ用着色オーラインフラ
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
            auraSR.sortingOrder = sr.sortingOrder - 1;

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
    void FixedUpdate()
    {
        if (!isInitialized || !isActive) return;
        // ★ アニメーション更新
        if (isAnimated)
        {
            UpdateAnimation();
        }


        // --- ディレイ（待機・収束）フェーズ ---
        // --- ディレイ（待機・収束・カウンター一回転）フェーズ ---
        if (delayFrames > 0)
        {
            // ★ カウンターナイフ特有の「その場でくるくる回転」を処理
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
                if (col != null) col.enabled = true; // 当たり判定を有効化
                if (activeDelayEffect != null) Destroy(activeDelayEffect);

                // ★ 待機終了の瞬間、敵プレイヤーの座標へ正確に銃口を向ける（ロックオン）
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

        // ★ 修正1：直接 Destroy せず、エフェクト無効（false）で Deactivate を呼ぶ形に統一
        if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
        {
            Deactivate(false);
        }
    }
    // ターゲットへの角度を逆算するヘルパー関数
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

            // 💡 本体のスプライトをアニメーション更新
            sr.sprite = currentData.animationSprites[currentAnimFrame];

            // 🎯【オーラアトラス同期レイヤー】：
            // 💡 もし背後に生成したオーラ用の子オブジェクト（PureColorAuraObject）があれば、
            // 💡 そのSpriteRendererの画像も本体のアニメーションの動きに完全に連動させます！
            Transform auraChild = transform.Find("PureColorAuraObject");
            if (auraChild != null)
            {
                SpriteRenderer childSR = auraChild.GetComponent<SpriteRenderer>();
                if (childSR != null)
                {
                    // 💡 オーラ側も同じアニメーションコマへと追従
                    childSR.sprite = sr.sprite;
                }
            }
        }
    }
    private IEnumerator DelayEffectRoutine(float delay, BulletData data)
    {
        if (effectPrefab != null && data.delaySprite != null)
        {
            activeDelayEffect = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            // 魔法陣を弾に追従させる
            activeDelayEffect.transform.SetParent(this.transform);

            SpriteRenderer effSr = activeDelayEffect.GetComponent<SpriteRenderer>();
            if (effSr != null)
            {
                effSr.sprite = data.delaySprite;
                // 弾より少し手前に表示
                effSr.sortingOrder = sr.sortingOrder + 1;
            }

            ShotEffect logic = activeDelayEffect.GetComponent<ShotEffect>();
            if (logic != null)
            {
                // ShotEffect側のPlayDelayコルーチンを実行
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
            // ヒット時はエフェクトを出す
            collision.SendMessage("OnHit", currentData.damage, SendMessageOptions.DontRequireReceiver);
            Deactivate(true);
        }
    }
    // =========================================================================
    // 🟥【重複排除・完全開通版】：消滅・プール返却メソッド群（ここが1つずつになれば治ります）
    // =========================================================================

    /// <summary>
    /// 引数あり版：エフェクトの再生有無を指定して弾を非アクティブ化し、プールへ返却する
    /// </summary>
    public void Deactivate(bool playBreakEffect)
    {
        isActive = false;
        if (activeDelayEffect != null) Destroy(activeDelayEffect);

        // 画面外（9.5f以上）にいる場合は、エフェクトを強制的にオフ
        if (Mathf.Abs(transform.position.x) > 9.5f || Mathf.Abs(transform.position.y) > 9.5f)
        {
            playBreakEffect = false;
        }

        // 消滅エフェクト（ShotEffect）の生成
        if (playBreakEffect && effectPrefab != null && currentData != null)
        {
            GameObject eff = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            SpriteRenderer effSr = eff.GetComponent<SpriteRenderer>();
            if (effSr != null && sr != null) effSr.sortingOrder = sr.sortingOrder + 1;

            ShotEffect logic = eff.GetComponent<ShotEffect>();
            if (logic != null)
                logic.StartCoroutine(logic.PlayBreakAnimation(currentData.breakColor, transform.localScale.x));
        }

        // 🚨【重要セーフティ】：プールへ戻る前に、親Box / 子Circleすべてのコライダーを完全に眠らせる
        SetColliderActive(false);
        transform.rotation = Quaternion.identity;

        // BulletPool（オブジェクトプール）へのクリーン返却
        if (currentData != null && currentData.bulletPrefab != null && BulletPool.Instance != null)
        {
            BulletPool.Instance.Release(currentData.bulletPrefab, gameObject);
        }
        else
        {
            // プールが見つからない場合のフォールバック
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 引数なし版：外部クラスやタイムアウト等からシンプルに呼び出された場合、エフェクトなしで安全にプールへ返却する
    /// </summary>
    public void Deactivate()
    {
        // 上の「引数あり版」に false（エフェクトなし）を渡して処理をスマートに共通集約
        Deactivate(false);
    }
}
