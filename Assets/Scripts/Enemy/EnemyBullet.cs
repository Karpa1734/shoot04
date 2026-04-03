using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    public GameObject originPrefab;
    public GameObject effectPrefab;

    private SpriteRenderer sr;
    private CircleCollider2D col;
    private BulletData currentData;
    private GameObject activeDelayEffect;

    private float speed, angle, accel, maxSpeed, angularVelocity;
    private bool isInitialized = false;
    private bool isFiring = false;
    private bool isActive = true;

    private int delayFrameCount = 0;
    private int currentSortingOrder;
    private bool isGrazed = false;

    // --- 追加：弾のタイプ定義 ---
    public enum BulletType { A1_Standard, A3_Reflect, A4_Gravity }
    private BulletType bulletType = BulletType.A1_Standard;

    // --- 追加：反射(A3)用パラメータ ---
    private int reflectCount = 0;
    private int maxReflectCount = 0;
    public bool reflectL, reflectR, reflectU, reflectD; // 上下左右の反射有効フラグ
    public float accelAfterReflect;    // 反射時の加減速量
    public BulletData nextDataAfterReflect; // 反射後に変化する弾のデータ

    // --- 追加：軌道変化（Transform）用パラメータ ---
    private bool willTransform = false;
    private float transformTimer = 0;
    private float tSpeed, tAngle, tAccel, tMaxSpeed, tAngVel;
    private BulletData tNextData;
    private bool tAimAtPlayer; // 変化時に自機を狙うか

    // --- 追加：軌道変化データの定義 ---
    [System.Serializable]
    public class BulletTransformData
    {
        public float triggerTime;
        public float speed = -999f;    // ★デフォルト値を -999f に設定
        public float angle = -999f;    // ★デフォルト値を -999f に設定
        public float accel = -999f;    // ★デフォルト値を -999f に設定
        public float maxSpeed = -999f; // ★デフォルト値を -999f に設定
        public float angVel = -999f;   // ★デフォルト値を -999f に設定
        public BulletData nextData;
        public bool aimAtPlayer;
    }

    private List<BulletTransformData> transformQueue = new List<BulletTransformData>();
    private float timeSinceFiring = 0f;

    // --- 追加：重力(A4)用パラメータ ---
    private float gravityAccel = 0;
    private float gravityAngle = 0;
    private float gravityMaxSpeed = 0;
    private float minX = -6f, maxX = 2f, minY = -4.7f, maxY = 4.7f; // 画面外判定用の範囲

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<CircleCollider2D>();
    }

    // --- シンプルな弾幕スクリプト（SpiralPattern等）から呼ばれる用 ---
    public void SetVelocity(Vector2 v)
    {
        // ベクトルから速度と角度を逆算して、内部パラメータに同期させる
        speed = v.magnitude;
        angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        // 進行方向に向ける（画像が上向き前提なら -90f）
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        isInitialized = true;
        isFiring = true; // SetVelocity時は遅延なしで即発射
        isActive = true;
        delayFrameCount = 0;

        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = true;
    }
    // 複数回変化用の初期化関数
    public void InitializeMultiTransform(float speed, float angle, float delay, BulletData data, List<BulletTransformData> transforms, Material overrideMat = null)
    {
        // 基本初期化（加速などは最初の変化が来るまで0にするなどの運用が可能）
        Initialize(speed, angle, 0, speed, 0, delay, data, overrideMat);

        this.transformQueue = new List<BulletTransformData>(transforms);
        // トリガー時間順にソートしておく（念のため）
        this.transformQueue.Sort((a, b) => a.triggerTime.CompareTo(b.triggerTime));
        this.timeSinceFiring = 0f;
    }
    // 軌道変化用の初期化関数
    public void InitializeTransform(float speed, float angle, float accel, float maxSpeed, float angVel, float delay, BulletData data,
        float timeToChange, float newSpeed, float newAngle, float newAccel, float newMaxSpeed, float newAngVel, BulletData nextData = null, bool aimAtPlayer = false, Material overrideMat = null)
    {
        // まず基本初期化
        Initialize(speed, angle, accel, maxSpeed, angVel, delay, data, overrideMat);

        // 変化パラメータのセット
        this.willTransform = true;
        this.transformTimer = timeToChange;
        this.tSpeed = newSpeed;
        this.tAngle = newAngle;
        this.tAccel = newAccel;
        this.tMaxSpeed = newMaxSpeed;
        this.tAngVel = newAngVel;
        this.tNextData = nextData;
        this.tAimAtPlayer = aimAtPlayer;
    }
    // --- 高度な弾幕生成（Danmakufu風）から呼ばれる用 ---
    public void Initialize(float speed, float angle, float accel, float maxSpeed, float angVel, float delay, BulletData data, Material overrideMat = null)
    {
        // --- プールの再利用に備えた完全リセット ---
        this.bulletType = BulletType.A1_Standard;
        this.reflectCount = 0;
        this.maxReflectCount = 0;
        this.reflectL = this.reflectR = this.reflectU = this.reflectD = false;
        this.accelAfterReflect = 0;
        this.nextDataAfterReflect = null;
        this.gravityAccel = 0;
        this.gravityMaxSpeed = 0;
        this.isGrazed = false;

        // ★追加：軌道変化（Transform）関連のリセット
        this.willTransform = false;
        this.transformQueue.Clear();
        this.timeSinceFiring = 0f;


        currentData = data;
        this.speed = speed;
        this.angle = angle;
        this.accel = accel;
        this.maxSpeed = maxSpeed;
        this.angularVelocity = angVel;

        // 見た目と当たり判定の設定（重複を整理）
        sr.sprite = data.bulletSprite;
        sr.color = Color.white;
        if (data.material != null) sr.material = data.material;
        col.radius = data.radius;
        // ★加算合成などのマテリアル上書き対応
        if (overrideMat != null)
            sr.material = overrideMat;
        else if (data.material != null)
            sr.material = data.material;
        // 描画順の決定
        currentSortingOrder = BulletSortingManager.GetNextOrder(data.sizeType);
        sr.sortingOrder = currentSortingOrder;

        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // 遅延フレーム設定
        this.delayFrameCount = Mathf.RoundToInt(delay);

        if (delay > 0)
        {
            StartCoroutine(DelayEffectRoutine(delay, data));
            isFiring = false;
        }
        else
        {
            isFiring = true;
            sr.enabled = true;
            col.enabled = true;
        }

        isInitialized = true;
        isActive = true;
    }
    // EnemyBullet.cs

    // 引数に反射方向フラグ、反射後加速、次データを追加
    public void InitializeAdvanced(float speed, float angle, BulletType type, int reflectLimit,
        float gAccel, float gAngle, float gMax, float delay, BulletData data,
        bool L = true, bool R = true, bool U = true, bool D = true, // ★追加
        float accelRef = 0, BulletData nextData = null, Material overrideMat = null) // ★追加
    {
        // 1. 先に基本初期化を呼び出す（ここですべてが A1_Standard / 反射OFF にリセットされる）
        Initialize(speed, angle, 0, speed, 0, delay, data, overrideMat);

        // 2. その後に、今回の特殊パラメータで「上書き」する
        this.bulletType = type;
        this.maxReflectCount = reflectLimit;
        this.gravityAccel = gAccel;
        this.gravityAngle = gAngle;
        this.gravityMaxSpeed = gMax;
        this.reflectCount = 0;

        // ★追加：反射固有のパラメータをここでセット
        this.reflectL = L;
        this.reflectR = R;
        this.reflectU = U;
        this.reflectD = D;
        this.accelAfterReflect = accelRef;
        this.nextDataAfterReflect = nextData;

    }
    void FixedUpdate()
    {
        if (!isInitialized || !isActive) return;
      // ディレイカウントダウン
        if (delayFrameCount > 0)
        {
            delayFrameCount--;
            return;
        }

        // --- 軌道変化のチェック ---
        if (isFiring && transformQueue.Count > 0)
        {
            timeSinceFiring += Time.deltaTime;
            // リストの先頭にあるデータの時間を過ぎたか判定
            if (timeSinceFiring >= transformQueue[0].triggerTime)
            {
                ApplyTransform(transformQueue[0]);
                transformQueue.RemoveAt(0); // 実行したデータを削除
            }
        }


        // 動き出しの瞬間
        if (!isFiring)
        {
            isFiring = true;
            sr.enabled = true;
            col.enabled = true;
            if (activeDelayEffect != null) Destroy(activeDelayEffect);
        }

        float dt = 1f / 60f;

        // --- タイプ別の特殊計算 ---
        if (bulletType == BulletType.A4_Gravity)
        {
            // 重力加速
            float gRad = gravityAngle * Mathf.Deg2Rad;
            Vector2 gVec = new Vector2(Mathf.Cos(gRad), Mathf.Sin(gRad)) * gravityAccel * dt * 60f;

            // 現在の速度ベクトルに加算
            float curRad = angle * Mathf.Deg2Rad;
            Vector2 velocity = new Vector2(Mathf.Cos(curRad), Mathf.Sin(curRad)) * speed;
            velocity += gVec;

            // 速度と角度を更新
            speed = velocity.magnitude;
            if (gravityMaxSpeed > 0 && speed > gravityMaxSpeed) speed = gravityMaxSpeed;
            angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        }

        // 基本移動
        angle += angularVelocity * dt * 60f;
        speed += accel * dt * 60f;
        if (accel != 0 && speed > maxSpeed) speed = maxSpeed;

        float rad = angle * Mathf.Deg2Rad;
        Vector3 moveVec = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * speed * dt;
        transform.position += moveVec;

        // --- 反射判定 (A3) ---
        if (bulletType == BulletType.A3_Reflect && (maxReflectCount == -1 || reflectCount < maxReflectCount))
        {
            HandleReflection();
        }

        // 進行方向に向ける
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // 画面外判定
        if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
            Deactivate(false);
    }
    private void ApplyTransform(BulletTransformData t)
    {
        // ★-999f でない場合のみ、現在の値を更新（上書き）する
        if (t.speed != -999f) this.speed = t.speed;
        if (t.accel != -999f) this.accel = t.accel;
        if (t.maxSpeed != -999f) this.maxSpeed = t.maxSpeed;
        if (t.angVel != -999f) this.angularVelocity = t.angVel;

        if (t.aimAtPlayer && PlayerMove.Instance != null)
        {
            this.angle = DanmakuFunctions.GetGapAngle(transform.position, PlayerMove.Instance.transform.position);
        }
        else if (t.angle != -999f) // 角度も -999f でなければ更新
        {
            this.angle = t.angle;
        }

        if (t.nextData != null) ApplyNewData(t.nextData);

        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }
    private void HandleReflection()
    {
        if (reflectCount >= maxReflectCount && maxReflectCount >= 0) return;

        Vector3 pos = transform.position;
        bool reflected = false;

        // 埋まり防止のための押し戻し量
        float offset = 0.05f;

        // --- 左右の反射判定と押し戻し ---
        if (pos.x < minX && reflectL)
        {
            angle = 180f - angle;
            pos.x = minX + offset; // 壁の内側（右）へ押し戻す
            reflected = true;
        }
        else if (pos.x > maxX && reflectR)
        {
            angle = 180f - angle;
            pos.x = maxX - offset; // 壁の内側（左）へ押し戻す
            reflected = true;
        }

        // --- 上下の反射判定と押し戻し ---
        if (pos.y > maxY && reflectU)
        {
            angle = -angle;
            pos.y = maxY - offset; // 壁の内側（下）へ押し戻す
            reflected = true;
        }
        else if (pos.y < minY && reflectD)
        {
            angle = -angle;
            pos.y = minY + offset; // 壁の内側（上）へ押し戻す
            reflected = true;
        }

        if (reflected)
        {
            reflectCount++;

            // 1. 反射時の速度変化
            speed += accelAfterReflect;

            // 2. 弾の種類の変化
            if (nextDataAfterReflect != null)
            {
                ApplyNewData(nextDataAfterReflect);
            }

            // 修正後の座標を反映
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }
    }
    // 途中でデータ（見た目や判定）を差し替えるヘルパー
    private void ApplyNewData(BulletData data)
    {
        currentData = data;
        sr.sprite = data.bulletSprite;
        col.radius = data.radius;
        if (data.material != null) sr.material = data.material;
    }

    IEnumerator DelayEffectRoutine(float delayFrames, BulletData data)
    {
        sr.enabled = false;
        col.enabled = false;

        if (delayFrames > 0 && effectPrefab != null && data.delaySprite != null)
        {
            activeDelayEffect = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            SpriteRenderer effSr = activeDelayEffect.GetComponent<SpriteRenderer>();
            if (effSr != null)
            {
                effSr.sortingOrder = currentSortingOrder + 1;
                effSr.sprite = data.delaySprite;
            }

            ShotEffect logic = activeDelayEffect.GetComponent<ShotEffect>();
            if (logic != null)
                logic.StartCoroutine(logic.PlayDelay(delayFrames / 60f, data.delaySprite, transform.localScale.x));
        }
        yield return null;
    }

    public void Deactivate(bool playBreakEffect)
    {
        isActive = false;
        if (activeDelayEffect != null) Destroy(activeDelayEffect);

        if (playBreakEffect && effectPrefab != null && currentData != null)
        {
            GameObject eff = Instantiate(effectPrefab, transform.position, Quaternion.identity);
            SpriteRenderer effSr = eff.GetComponent<SpriteRenderer>();
            if (effSr != null) effSr.sortingOrder = currentSortingOrder + 1;

            ShotEffect logic = eff.GetComponent<ShotEffect>();
            if (logic != null)
                logic.StartCoroutine(logic.PlayBreakAnimation(currentData.breakColor, transform.localScale.x));
        }

        isInitialized = false;
        isFiring = false;

        if (BulletPool.Instance != null && originPrefab != null)
        {
            // 自分のコピー元(originPrefab)を指定してプールに戻す
            BulletPool.Instance.Release(originPrefab, gameObject);
        }
        else
        {
            Destroy(gameObject); // プールがない、またはプレハブ指定がない場合は破壊
        }
    }
    // EnemyBullet.cs に追加
    public void UpdateAngle(float newAngle)
    {
        this.angle = newAngle;
        // 進行方向に合わせてスプライトの向きも更新
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }

    // 角度を外部から参照できるようにゲッターも用意
    public float GetAngle() => angle;
    public bool TryGraze()
    {
        if (isGrazed) return false;
        isGrazed = true;
        return true;
    }

    // 弾がプールに戻る（消える）際にフラグをリセットするようにしてください
    public void OnDisable()
    {
        isGrazed = false;
    }
}