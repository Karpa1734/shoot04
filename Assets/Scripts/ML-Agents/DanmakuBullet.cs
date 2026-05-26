using System.Collections;
using UnityEngine;

public class DanmakuBullet : MonoBehaviour
{
    private SpriteRenderer sr;
    private CircleCollider2D col;

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
        col = GetComponent<CircleCollider2D>();
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

        // 🌟 修正の核心：プレハブ本来の初期スケール（1.3など）をベースとして取得！
        Vector3 baseScale = transform.localScale;
        // インスペクターの拡大率を「さらに何倍するか」の乗算チップに切り替え
        float multiplier = (data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(baseScale.x * multiplier, baseScale.y * multiplier, baseScale.z * multiplier);

        sr.sprite = data.bulletSprite;
        col.radius = data.radius;
        if (data.material != null) sr.material = data.material;

        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        // ★ アニメーションの有無を確認
        if (data.animationSprites != null && data.animationSprites.Length > 1)
        {
            isAnimated = true;
            sr.sprite = data.animationSprites[0];
        }
        else
        {
            isAnimated = false;
            sr.sprite = data.bulletSprite;
        }
        // 当たり判定の設定
        if (col != null)
        {
            col.radius = data.radius;
            // ★ 追加：データのオフセット値をコライダーに反映
            col.offset = data.colliderOffset;
        }
        if (delay > 0)
        {
            // --- 遅延エフェクト（魔法陣）の表示 ---
            StartCoroutine(DelayEffectRoutine(delay, data));
            sr.enabled = false;
            col.enabled = false;
        }
        else
        {
            sr.enabled = true;
            col.enabled = true;
        }

        isInitialized = true;
        isActive = true;
    }

    // ★ 追加：オブジェクトプールに対応したカウンターナイフ専用の初期化メソッド
    public void InitializeKnifeCounter(GameObject shooter, string target, float shootSpeed, float delayDuration, BulletData data)
    {
        this.owner = shooter;
        this.targetTag = target;
        this.currentData = data;
        this.speed = shootSpeed;
        this.accel = 0;
        this.maxSpeed = shootSpeed;
        this.angularVelocity = 0;

        // 🌟 修正の核心：時間停止ナイフ用でもプレハブ本来のサイズ（1.3など）をベースに乗算
        Vector3 baseScaleKn = transform.localScale;
        float multiplierKn = (data.bulletScale > 0f) ? data.bulletScale : 1.0f;
        transform.localScale = new Vector3(baseScaleKn.x * multiplierKn, baseScaleKn.y * multiplierKn, baseScaleKn.z * multiplierKn);

        // 秒数をFixedUpdate基準のフレーム数に変換（例: 0.5秒 ➔ 30フレーム）
        this.delayFrames = Mathf.RoundToInt(delayDuration * 60f);
        this.totalDelay = this.delayFrames;

        this.isConverging = false;
        this._isKnifeCounter = true; // ★ 強欲カウンターモードをオン
        this.isAnimated = false;

        sr.sprite = data.bulletSprite;
        if (data.material != null) sr.material = data.material;

        // 待機中の見た目を華やかにするため初期角度をランダムに
        _knifeCurrentAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0, 0, _knifeCurrentAngle - 90f);

        // ナイフは回転する姿を見せるため最初から表示するが、当たり判定は発射までオフ
        sr.enabled = true;
        if (col != null)
        {
            col.radius = data.radius;
            col.offset = data.colliderOffset;
            col.enabled = false;
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
        // 弾が非表示（ディレイ中）でも計算自体は進めておく
        animTimer += Time.fixedDeltaTime;
        float frameDuration = 1f / currentData.animationFPS;

        if (animTimer >= frameDuration)
        {
            animTimer = 0f;
            currentAnimFrame = (currentAnimFrame + 1) % currentData.animationSprites.Length;
            sr.sprite = currentData.animationSprites[currentAnimFrame];
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
    // 引数 bool playBreakEffect を追加
    // 引数付きの Deactivate メソッドを修正
    public void Deactivate(bool playBreakEffect)
    {
        isActive = false;
        if (activeDelayEffect != null) Destroy(activeDelayEffect);

        // ★ 修正2：【安全弁】もし既に画面外（例: 閾値9.5f以上）にいる場合は、
        // 衝突判定などが割り込んできて playBreakEffect が true になっていても強制的に false に上書きする
        if (Mathf.Abs(transform.position.x) > 9.5f || Mathf.Abs(transform.position.y) > 9.5f)
        {
            playBreakEffect = false;
        }

        // ★ 消滅エフェクト（ShotEffect）の生成ロジック
        if (playBreakEffect && effectPrefab != null && currentData != null)
        {
            GameObject eff = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            SpriteRenderer effSr = eff.GetComponent<SpriteRenderer>();
            if (effSr != null) effSr.sortingOrder = sr.sortingOrder + 1;

            ShotEffect logic = eff.GetComponent<ShotEffect>();
            if (logic != null)
                // BulletData の breakColor を使用してアニメーション再生（ここでSEも鳴る想定）
                logic.StartCoroutine(logic.PlayBreakAnimation(currentData.breakColor, transform.localScale.x));
        }

        // オブジェクトプール（マネージャー経由）を使用している場合は、ここをプール返却関数に差し替えてください
        Destroy(gameObject); //
    }
    public void Deactivate()
    {
        isActive = false;
        if (activeDelayEffect != null) Destroy(activeDelayEffect);
        Destroy(gameObject);
    }
}