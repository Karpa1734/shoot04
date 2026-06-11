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

    // =========================================================================
    // 🎨【新設：レーザーシンクロ追従型・加算オーラインフラ変数】
    // =========================================================================
    private Transform auraVisualTrans;
    private SpriteRenderer auraSR;

    private float targetWidth, currentLength;
    private int delayFrames, elapsedFrames, closingFrames;
    private bool isFired = false;
    private bool isClosing = false;

    public bool IsPreviewing => elapsedFrames < delayFrames && !isClosing;
    public float CurrentLength => currentLength;

    private float targetDistAngleVel;
    private bool useSmoothStop = false;
    private float targetLaserAngleVel;
    private string _targetTag;
    private int _damage;
    private float _hitTimer = 0f;
    private float lengthVel, angle, angVel, moveSpeed, moveAngle;
    private float dist, distVel, distAngle, distAngleVel, laserAngle, laserAngleVel;
    private float targetAngVel;
    private SpriteRenderer sourceEffectSr;
    private GameObject sourceEffectInstance;
    private List<LaserTransformData> transformQueue = new List<LaserTransformData>();
    private float closingStartWidth;
    private Vector3 _centerPos;
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

    // 🎯【修正】：引数の末尾に BulletData data を追加して上流から結合
    public void SetupA(GameObject shooter, string target, int damage, float x, float y, float length, float width, BulletManager.LaserColor color, int delay, GameObject sourcePrefab, Sprite sourceSprite, BulletData data)
    {
        this.type = LaserType.A_Stationary;
        this._rootOwner = shooter;
        this._targetTag = target;
        this._damage = damage;
        transform.position = new Vector3(x, y, 0);

        ApplyTeamSettings(shooter);
        SpawnSourceEffect(sourcePrefab, sourceSprite);
        InitializeBase(length, width, color, delay);

        // 💡 レーザー用アセットデータを手渡ししてオーラを結合
        InjectLaserAuraLink(data);
    }

    // 🎯【修正】：引数の末尾に BulletData data を追加して上流から結合
    public void SetupB(GameObject shooter, string target, int damage, float x, float y, float length, float width, BulletManager.LaserColor color, int delay, GameObject sourcePrefab, Sprite sourceSprite, BulletData data)
    {
        this.type = LaserType.B_FollowBoss;
        this._rootOwner = shooter;
        this._targetTag = target;
        this._damage = damage;
        this.bossTransform = shooter.transform;

        this._centerPos = new Vector3(x, y, 0);
        transform.position = _centerPos;

        ApplyTeamSettings(shooter);
        SpawnSourceEffect(sourcePrefab, sourceSprite);
        InitializeBase(length, width, color, delay);

        // 💡 レーザー用アセットデータを手渡ししてオーラを結合
        InjectLaserAuraLink(data);
    }

    // =========================================================================
    // 🔮【調停完了】：白シルエット画像（auraWhiteSprite）完全対応型レーザーオーラ結合
    // =========================================================================
    private void InjectLaserAuraLink(BulletData data)
    {
        if (laserVisualTrans == null || sr == null || _rootOwner == null) return;

        // 💡 1. オーラ専用の伸縮GameObjectを動的生成して結合
        GameObject auraObj = new GameObject("LaserPureColorAura");
        auraObj.transform.SetParent(laserVisualTrans.parent);

        // 位置・回転を本体と完全シンクロ
        auraObj.transform.localPosition = laserVisualTrans.localPosition;
        auraObj.transform.localRotation = laserVisualTrans.localRotation;
        auraObj.transform.localScale = laserVisualTrans.localScale;

        auraVisualTrans = auraObj.transform;
        auraSR = auraObj.AddComponent<SpriteRenderer>();

        // 💡 2. レイヤー順（SortingOrder）を、レーザー本体の「真後ろ（-1）」へ潜り込ませる
        auraSR.sortingLayerID = sr.sortingLayerID;
        auraSR.sortingOrder = sr.sortingOrder - 1;
        auraSR.enabled = false;

        // 💡 3. 【高橋さんの指定】：弾幕と同様に auraWhiteSprite を優先的に読み込んでアサイン！
        // 💡    もしデータ側に登録されていなければ、フォールバックとしてレーザー本来の画像（visualSet.mainSprite）を適応します
        if (data != null && data.auraWhiteSprite != null)
        {
            auraSR.sprite = data.auraWhiteSprite;
        }
        else if (visualSet.mainSprite != null)
        {
            auraSR.sprite = visualSet.mainSprite;
        }
        else
        {
            auraSR.sprite = sr.sprite;
        }

        // 💡 4. 加算マテリアルの適用：データに登録された auraMaterial からResourcesを介さずに直接静的共有
        if (data != null && data.auraMaterial != null)
        {
            auraSR.material = data.auraMaterial;
        }
        else
        {
            auraSR.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        }

        // 💡 5. 大元の持ち主のカラー（imageColor）を精密抽出してオーラへ直撃注入！
        var myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        if (myStatus == null) myStatus = _rootOwner.GetComponentInParent<PlayerStatusManager>();

        if (myStatus != null && myStatus.characterData != null)
        {
            Color charImageColor = myStatus.characterData.imageColor;
            charImageColor.a = 0.2f; // 指定の通りアルファ1.0fの最大発光で流し込み
            auraSR.color = charImageColor;
        }
        else
        {
            int ownerId = (myStatus != null) ? myStatus.playerId : 1;
            Color defaultColor = (ownerId == 1) ? Color.cyan : Color.red;
            defaultColor.a = 0.2f;
            auraSR.color = defaultColor;
        }
    }

    private void ApplyTeamSettings(GameObject shooter)
    {
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
        if (auraSR != null) auraSR.enabled = true;
    }

    public void ForceClose()
    {
        if (isClosing) return;
        closingStartWidth = GetCurrentWidth();
        isClosing = true;
        col.enabled = false;
        lengthVel = 0;
        closingFrames = 0;
    }

    private float GetCurrentWidth()
    {
        if (elapsedFrames < delayFrames) return targetWidth * 0.5f;
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
        if (!PlayerMove.CanShoot && !isClosing)
        {
            ForceClose();
        }
        if (transformQueue.Count > 0 && elapsedFrames >= transformQueue[0].frame)
        {
            ApplyTransform(transformQueue[0]);
            transformQueue.RemoveAt(0);
        }

        if (useSmoothStop)
        {
            angVel = Mathf.Lerp(angVel, targetAngVel, 0.1f);
            laserAngleVel = Mathf.Lerp(laserAngleVel, targetLaserAngleVel, 0.1f);
            distAngleVel = Mathf.Lerp(distAngleVel, targetDistAngleVel, 0.1f);
        }
        else
        {
            angVel = targetAngVel;
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
        if (bossTransform == null)
        {
            ForceClose();
            return;
        }

        dist += distVel;
        distAngle += distAngleVel;
        laserAngle += laserAngleVel;

        if (!isClosing) currentLength += lengthVel;

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

        float spriteOriginalHeight = sr.sprite.bounds.size.y;
        float spriteOriginalWidth = sr.sprite.bounds.size.x;

        float finalScaleY = currentLength / spriteOriginalHeight;
        float finalScaleX = w / spriteOriginalWidth;

        if (laserVisualTrans != null)
        {
            laserVisualTrans.localScale = new Vector3(finalScaleX, finalScaleY, 1f);
        }

        // =========================================================================
        // 🎯【白アセット追従型】：レーザー形状にシンクロして横幅を1.5倍に拡張
        // =========================================================================
        if (auraVisualTrans != null && auraSR != null)
        {
            auraVisualTrans.localScale = new Vector3(finalScaleX * 1.5f, finalScaleY, 1f);
        }

        if (col != null)
        {
            float hitboxWidthMultiplier = 0.7f;
            col.size = new Vector2(w * hitboxWidthMultiplier, currentLength);
            col.offset = new Vector2(0, currentLength * 0.5f);

            if (elapsedFrames >= delayFrames && !isClosing) col.enabled = true;
        }

        if (sourceEffectInstance != null && sourceEffectSr != null)
        {
            float effectRatio = Mathf.Clamp01(w / targetWidth);
            float dynamicScale = 1.5f * effectRatio;
            sourceEffectInstance.transform.localScale = new Vector3(dynamicScale, dynamicScale, 1f);
            sourceEffectInstance.transform.Rotate(0, 0, 400f * Time.deltaTime);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!isFired || isClosing || elapsedFrames < delayFrames) return;
        if (collision.CompareTag(_targetTag) && _hitTimer <= 0)
        {
            collision.SendMessage("OnHit", _damage, SendMessageOptions.DontRequireReceiver);
            _hitTimer = 0.1f;
        }
    }

    private void ApplyTransform(LaserTransformData t)
    {
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
            if (t.angVel != -999f) targetAngVel = t.angVel;
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