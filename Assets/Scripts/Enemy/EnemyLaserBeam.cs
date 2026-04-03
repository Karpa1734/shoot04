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

    private float targetDistAngleVel;
    private bool useSmoothStop = false;
    private float targetLaserAngleVel;

    private float lengthVel, angle, angVel, moveSpeed, moveAngle;
    private float dist, distVel, distAngle, distAngleVel, laserAngle, laserAngleVel;

    private SpriteRenderer sourceEffectSr;
    private GameObject sourceEffectInstance;
    private List<LaserTransformData> transformQueue = new List<LaserTransformData>();
    private float closingStartWidth;

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

    public void SetupA(float x, float y, float length, float width, BulletManager.LaserColor color, int delay, GameObject sourcePrefab, Sprite sourceSprite)
    {
        type = LaserType.A_Stationary;
        transform.position = new Vector3(x, y, 0);
        SpawnSourceEffect(sourcePrefab, sourceSprite);
        InitializeBase(length, width, color, delay);
    }

    public void SetupB(float length, float width, BulletManager.LaserColor color, int delay, Transform boss, GameObject sourcePrefab, Sprite sourceSprite)
    {
        type = LaserType.B_FollowBoss;
        bossTransform = boss;
        if (bossTransform != null) transform.position = bossTransform.position;
        SpawnSourceEffect(sourcePrefab, sourceSprite);
        InitializeBase(length, width, color, delay);
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

        if (transformQueue.Count > 0 && elapsedFrames >= transformQueue[0].frame)
        {
            ApplyTransform(transformQueue[0]);
            transformQueue.RemoveAt(0);
        }

        // 回転の補間処理
        if (useSmoothStop)
        {
            laserAngleVel = Mathf.Lerp(laserAngleVel, targetLaserAngleVel, 0.1f);
            distAngleVel = Mathf.Lerp(distAngleVel, targetDistAngleVel, 0.1f);
        }
        else
        {
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
        // ボスがいなくなったらフェードアウト開始
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
        transform.position = bossTransform.position + offset;
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
        transform.localScale = Vector3.one;

        if (laserVisualTrans != null)
        {
            laserVisualTrans.localScale = new Vector3(w, currentLength, 1f);
        }

        if (col != null)
        {
            float hitboxWidthScale = 0.2f;
            col.size = new Vector2(w * hitboxWidthScale, currentLength);
            col.offset = new Vector2(0, currentLength * 0.5f);
        }

        if (sourceEffectInstance != null && sourceEffectSr != null)
        {
            float effectRatio = 1f;

            if (isClosing)
            {
                effectRatio = Mathf.Clamp01(w / targetWidth);
            }
            // 予告中(elapsedFrames < delayFrames)は effectRatio = 1.0f のまま維持

            float dynamicScale = 1.5f * effectRatio;
            sourceEffectInstance.transform.localScale = new Vector3(dynamicScale, dynamicScale, 1f);

            Color c = sourceEffectSr.color;
            c.a = sr.color.a * effectRatio;
            sourceEffectSr.color = c;

            sourceEffectInstance.transform.Rotate(0, 0, 400f * Time.deltaTime);
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
            if (t.angVel != -999f) angVel = t.angVel;
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