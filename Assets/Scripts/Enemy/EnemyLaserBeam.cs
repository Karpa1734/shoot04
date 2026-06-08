using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyLaserBeam : MonoBehaviour
{
    private const int ANIM_FRAMES = 10;

    public enum LaserType { A_Stationary, B_FollowBoss }
    private LaserType type;

    private SpriteRenderer sr;
    private BoxCollider2D col;
    private BulletManager.LaserSet visualSet;
    private Transform bossTransform;
    private Transform laserVisualTrans;

    private float targetWidth, currentLength;
    private int delayFrames, elapsedFrames, closingFrames;
    private bool isFired = false;
    private bool isClosing = false;
    /// <summary>
    /// ★ AI連携用：現在レーザーが「予告線（プレビュー）」の状態であれば true を返す
    /// </summary>
    public bool IsPreviewing => elapsedFrames < delayFrames && !isClosing;

    /// <summary>
    /// ★ AI連携用：現在のレーザーのワールド座標上の長さを取得
    /// </summary>
    public float CurrentLength => currentLength;
    private float targetDistAngleVel;
    private bool useSmoothStop = false;
    private float targetLaserAngleVel;
    private string _targetTag; // ★追加：攻撃対象のタグ
    private int _damage;       // ★追加：ダメージ量
    private float _hitTimer = 0f; // 多段ヒット用タイマー
    private float lengthVel, angle, angVel, moveSpeed, moveAngle;
    private float dist, distVel, distAngle, distAngleVel, laserAngle, laserAngleVel;
    private float targetAngVel; // ★追加：目標とする角速度
    private SpriteRenderer sourceEffectSr;
    private GameObject sourceEffectInstance;
    private List<LaserTransformData> transformQueue = new List<LaserTransformData>();
    private float closingStartWidth;
    private Vector3 _centerPos; // ★追加：回転の中心となる固定座標
    // ★ 追加：エラーの原因となっていた変数を宣言
    private GameObject _rootOwner;
    [System.Serializable]
    public class LaserTransformData
    {
        public int frame;
        public float angle = -999f, angVel = -999f, lengthVel = -999f;
        public float moveSpeed = -999f, moveAngle = -999f;
        public float dist = -999f, distVel = -999f, distAngle = -999f, distAngleVel = -999f, laserAngle = -999f, laserAngleVel = -999f;
        public bool startClosing = false;
        public bool isSmooth = false;
    }

    void Awake()
    {
        // 既存のAwake処理
        Transform child = transform.Find("Visual");
        if (child != null)
        {
            laserVisualTrans = child;
            sr = child.GetComponent<SpriteRenderer>();
        }
        else
        {
            laserVisualTrans = transform;
            sr = GetComponent<SpriteRenderer>();
        }
        col = GetComponent<BoxCollider2D>();
        sr.enabled = false;
    }

    // ★ 修正：SetupA
    // ★修正：引数に targetTag と damage を追加
    public void SetupA(GameObject shooter, string target, int damage, float x, float y, float length, float width, BulletManager.LaserColor color, int delay, GameObject sourcePrefab, Sprite sourceSprite)
    {
        this.type = LaserType.A_Stationary;
        this._rootOwner = shooter;
        this._targetTag = target;
        this._damage = damage;
        transform.position = new Vector3(x, y, 0);

        ApplyTeamSettings(shooter);
        SpawnSourceEffect(sourcePrefab, sourceSprite);
        InitializeBase(length, width, color, delay);
    }

    // ★ 修正：SetupB (shooter引数を追加)
    // --- EnemyLaserBeam.cs 修正箇所 ---

    public void SetupB(GameObject shooter, string target, int damage, float x, float y, float length, float width, BulletManager.LaserColor color, int delay, GameObject sourcePrefab, Sprite sourceSprite)
    {
        this.type = LaserType.B_FollowBoss;
        this._rootOwner = shooter;
        this._targetTag = target;
        this._damage = damage;
        this.bossTransform = shooter.transform;

        // ★重要：発射時の座標を中心に固定する
        this._centerPos = new Vector3(x, y, 0);
        transform.position = _centerPos;

        ApplyTeamSettings(shooter);
        SpawnSourceEffect(sourcePrefab, sourceSprite);
        InitializeBase(length, width, color, delay);
    }
    // ★ 追加：チーム（P1/P2）に応じた設定を適用するヘルパー関数
    private void ApplyTeamSettings(GameObject shooter)
    {
        // グレイズ判定用にタグは "Laser" 固定にする
        gameObject.tag = "Laser";

        var myStatus = shooter.GetComponentInParent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;
        int layer = LayerMask.NameToLayer(ownerId == 1 ? "Player1Bullet" : "Player2Bullet");
        gameObject.layer = layer;
    }

    private void InitializeBase(float length, float width, BulletManager.LaserColor color, int delay)
    {
        visualSet = BulletManager.Instance.GetLaserSet(color);
        this.currentLength = length;
        this.targetWidth = width;
        this.delayFrames = delay;
        this.elapsedFrames = 0;
        this.closingFrames = 0;
        this.isClosing = false;
        this.laserAngleVel = 0;
        this.targetLaserAngleVel = 0;
        this.sr.sprite = visualSet.mainSprite;
        this.sr.material = BulletManager.Instance.additiveMaterial;
        this.sr.color = new Color(1, 1, 1, 0.4f);
        this.col.enabled = false;

        UpdateVisuals(targetWidth * 0.5f);
    }

    public void AddData(LaserTransformData d)
    {
        transformQueue.Add(d);
        transformQueue.Sort((a, b) => a.frame.CompareTo(b.frame));

        if (d.frame == 0)
        {
            ApplyTransform(d);
            if (type == LaserType.A_Stationary) UpdateA();
            else UpdateB();
        }
    }

    public void Fire()
    {
        isFired = true;
        sr.enabled = true;
    }

    public void ForceClose()
    {
        if (isClosing) return;

        closingStartWidth = GetCurrentWidth(); // 現在の太さを記憶
        isClosing = true;
        col.enabled = false;
        lengthVel = 0;
        closingFrames = 0;
    }

    private float GetCurrentWidth()
    {
        if (elapsedFrames < delayFrames)
            return targetWidth * 0.5f;

        if (elapsedFrames < delayFrames + ANIM_FRAMES)
        {
            float t = (float)(elapsedFrames - delayFrames) / ANIM_FRAMES;
            return Mathf.Lerp(targetWidth * 0.5f, targetWidth, t);
        }

        return targetWidth;
    }

    void FixedUpdate()
    {
        if (!isFired) return;
        // ★ 追加：ラウンド終了（タイムアップ等）を検知したら即座に閉じる
        // PlayerMove.CanShoot が false になった瞬間に ForceClose を実行します
        if (!PlayerMove.CanShoot && !isClosing)
        {
            ForceClose();
        }
        if (transformQueue.Count > 0 && elapsedFrames >= transformQueue[0].frame)
        {
            ApplyTransform(transformQueue[0]);
            transformQueue.RemoveAt(0);
        }

        // 回転の補間処理（Type A の angVel も対象に含める）
        if (useSmoothStop)
        {
            angVel = Mathf.Lerp(angVel, targetAngVel, 0.1f); // ★追加
            laserAngleVel = Mathf.Lerp(laserAngleVel, targetLaserAngleVel, 0.1f);
            distAngleVel = Mathf.Lerp(distAngleVel, targetDistAngleVel, 0.1f);
        }
        else
        {
            angVel = targetAngVel; // ★追加
            laserAngleVel = targetLaserAngleVel;
            distAngleVel = targetDistAngleVel;
        }

        float widthToSet = 0;

        if (isClosing)
        {
            closingFrames++;
            float t = (float)closingFrames / ANIM_FRAMES;
            widthToSet = Mathf.Lerp(closingStartWidth, 0, t);

            if (closingFrames >= ANIM_FRAMES)
            {
                Destroy(gameObject);
                return;
            }
        }
        else if (elapsedFrames < delayFrames)
        {
            widthToSet = targetWidth * 0.5f;
        }
        else if (elapsedFrames < delayFrames + ANIM_FRAMES)
        {
            float t = (float)(elapsedFrames - delayFrames) / ANIM_FRAMES;
            widthToSet = Mathf.Lerp(targetWidth * 0.5f, targetWidth, t);
        }
        else
        {
            widthToSet = targetWidth;
        }

        if (elapsedFrames == delayFrames && !isClosing)
        {
            sr.color = Color.white;
            col.enabled = true;
        }

        if (type == LaserType.A_Stationary) UpdateA();
        else UpdateB();

        UpdateVisuals(widthToSet);
        elapsedFrames++;

        if (!isClosing && currentLength < 0.1f) ForceClose();
    }

    private void UpdateA()
    {
        angle += angVel;
        if (!isClosing) currentLength += lengthVel;
        Vector3 move = new Vector3(Mathf.Cos(moveAngle * Mathf.Deg2Rad), Mathf.Sin(moveAngle * Mathf.Deg2Rad), 0) * moveSpeed;
        transform.position += move;
        transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }

    private void UpdateB()
    {
        // 発射元（オーナー）がいなくなった場合の生存確認としてのみ bossTransform を使用
        if (bossTransform == null)
        {
            ForceClose();
            return;
        }

        dist += distVel;
        distAngle += distAngleVel;
        laserAngle += laserAngleVel;

        if (!isClosing) currentLength += lengthVel;

        // ★修正：bossTransform.position ではなく、記録した _centerPos を基準にする
        Vector3 offset = new Vector3(Mathf.Cos(distAngle * Mathf.Deg2Rad), Mathf.Sin(distAngle * Mathf.Deg2Rad), 0) * dist;
        transform.position = _centerPos + offset;

        transform.rotation = Quaternion.Euler(0, 0, laserAngle - 90f);
    }

    private void OnDestroy()
    {
        if (sourceEffectInstance != null) Destroy(sourceEffectInstance);
    }

    private void SpawnSourceEffect(GameObject prefab, Sprite sprite)
    {
        if (prefab != null)
        {
            sourceEffectInstance = Instantiate(prefab, transform.position, Quaternion.identity);
            sourceEffectInstance.transform.SetParent(this.transform);
            sourceEffectSr = sourceEffectInstance.GetComponent<SpriteRenderer>();
            if (sourceEffectSr != null) sourceEffectSr.sprite = sprite;
            sourceEffectInstance.transform.localScale = Vector3.one * 1.5f;
        }
    }

    private void UpdateVisuals(float w)
    {
        if (sr == null || sr.sprite == null) return;

        // 1. スプライトの元のサイズ（Unityの単位：Unit）を取得
        float spriteOriginalHeight = sr.sprite.bounds.size.y;
        float spriteOriginalWidth = sr.sprite.bounds.size.x;

        // 2. 正規化したスケールを計算
        float finalScaleY = currentLength / spriteOriginalHeight;
        float finalScaleX = w / spriteOriginalWidth;

        if (laserVisualTrans != null)
        {
            laserVisualTrans.localScale = new Vector3(finalScaleX, finalScaleY, 1f);
        }

        if (col != null)
        {
            float hitboxWidthMultiplier = 0.7f;

            // 現在のレーザーの向き（angle または laserAngle）を取得
            float currentFacingAngle = (type == LaserType.A_Stationary) ? angle : laserAngle;

            // 角度を 0〜360度 にクランプ
            float checkAngle = (currentFacingAngle % 360f + 360f) % 360f;


            // 通常の縦型レーザー（それ以外の角度）の時は、既存の標準計算を安全にフォールバック
            col.size = new Vector2(w * hitboxWidthMultiplier, currentLength);
            col.offset = new Vector2(0, currentLength * 0.5f);


            // 予告中は判定を消し、発射中のみ有効化
            if (elapsedFrames >= delayFrames && !isClosing) col.enabled = true;
        }

        // 弾源エフェクト等の回転処理（既存のまま）
        if (sourceEffectInstance != null && sourceEffectSr != null)
        {
            float effectRatio = Mathf.Clamp01(w / targetWidth);
            float dynamicScale = 1.5f * effectRatio;
            sourceEffectInstance.transform.localScale = new Vector3(dynamicScale, dynamicScale, 1f);
            sourceEffectInstance.transform.Rotate(0, 0, 400f * Time.deltaTime);
        }
    }

    // ★追加：ダメージ判定（多段ヒット）
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!isFired || isClosing || elapsedFrames < delayFrames) return;

        // ターゲットタグに一致するか判定
        if (collision.CompareTag(_targetTag) && _hitTimer <= 0)
        {
            collision.SendMessage("OnHit", _damage, SendMessageOptions.DontRequireReceiver);
            _hitTimer = 0.1f; // 6フレーム（約0.1秒）に1回ヒット
        }
    }

    private void ApplyTransform(LaserTransformData t)
    {
        // 消滅フラグが立ったら即座に ForceClose 
        if (t.startClosing && !isClosing)
        {
            ForceClose();
            return;
        }

        this.useSmoothStop = t.isSmooth;
        if (t.lengthVel != -999f) lengthVel = t.lengthVel;

        if (type == LaserType.A_Stationary)
        {
            if (t.angle != -999f) angle = t.angle; 
            if (t.angVel != -999f) targetAngVel = t.angVel; // ★修正：直接代入ではなく target に入れる
            if (t.moveSpeed != -999f) moveSpeed = t.moveSpeed;
            if (t.moveAngle != -999f) moveAngle = t.moveAngle;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }
        else
        {
            if (t.dist != -999f) dist = t.dist;
            if (t.distVel != -999f) distVel = t.distVel;
            if (t.distAngle != -999f) distAngle = t.distAngle;
            if (t.distAngleVel != -999f) targetDistAngleVel = t.distAngleVel;
            if (t.laserAngle != -999f) laserAngle = t.laserAngle;

            if (t.laserAngleVel != -999f)
            {
                targetLaserAngleVel = t.laserAngleVel;
                // 0フレーム目やパッと止まる設定なら即座に反映
                if (t.frame == 0 || !useSmoothStop)
                {
                    laserAngleVel = t.laserAngleVel;
                    distAngleVel = t.distAngleVel;
                }
            }
            transform.rotation = Quaternion.Euler(0, 0, laserAngle - 90f);
        }
    }
}