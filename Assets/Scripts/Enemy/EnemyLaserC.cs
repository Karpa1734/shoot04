using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
public class EnemyLaserC : MonoBehaviour
{
    private LineRenderer lr;
    private EdgeCollider2D edgeCol;
    private List<Vector2> points = new List<Vector2>(); // 軌跡の記録

    // 設定用
    private int maxLength; // 残像フレーム数
    private float width;
    private bool isFired = false;
    private bool isActive = false;
    private float delayTimer;

    // ヘッドの移動用（EnemyBulletの移動ロジックを流用）
    private float speed, angle, accel, maxSpeed, angVel;
    private List<EnemyBullet.BulletTransformData> transformQueue = new List<EnemyBullet.BulletTransformData>();
    private float timeSinceFired = 0f;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        edgeCol = GetComponent<EdgeCollider2D>();
        lr.enabled = false;
        edgeCol.enabled = false;
    }

    public void Setup(float x, float y, float width, int length, BulletData data, float delay)
    {
        transform.position = new Vector3(x, y, 0);
        this.width = width;
        this.maxLength = length;
        this.delayTimer = delay;

        // 見た目の設定
        lr.startWidth = lr.endWidth = width;
        if (data.material != null) lr.material = data.material;
        lr.textureMode = LineTextureMode.Tile;
    }

    public void AddTransformData(EnemyBullet.BulletTransformData tData)
    {
        transformQueue.Add(tData);
        transformQueue.Sort((a, b) => a.triggerTime.CompareTo(b.triggerTime));
    }

    public void Fire()
    {
        isFired = true;
        StartCoroutine(LaserRoutine());
    }

    private IEnumerator LaserRoutine()
    {
        // 遅延時間
        if (delayTimer > 0)
        {
            // ここに設置予告線などの演出を入れても良い
            yield return new WaitForSeconds(delayTimer);
        }

        isActive = true;
        lr.enabled = true;
        edgeCol.enabled = true;
    }

    void FixedUpdate()
    {
        if (!isActive) return;

        timeSinceFired += Time.deltaTime;
        float dt = 1f / 60f;

        // 1. ヘッドの軌道変化処理
        if (transformQueue.Count > 0 && timeSinceFired >= transformQueue[0].triggerTime)
        {
            ApplyTransform(transformQueue[0]);
            transformQueue.RemoveAt(0);
        }

        // 2. ヘッドの移動
        angle += angVel * dt * 60f;
        speed += accel * dt * 60f;
        if (accel != 0 && speed > maxSpeed) speed = maxSpeed;

        float rad = angle * Mathf.Deg2Rad;
        Vector3 moveVec = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * speed * dt;
        transform.position += moveVec;

        // 3. 軌跡の更新
        points.Insert(0, transform.position); // 先頭に現在の位置を追加
        if (points.Count > maxLength)
        {
            points.RemoveAt(points.Count - 1); // 末尾を削除
        }

        // 4. 描画と判定の更新（ここが重い原因）
        UpdateVisualAndCollision();
    }

    private void ApplyTransform(EnemyBullet.BulletTransformData t)
    {
        // NULL(特定の負の値など)チェックは呼び出し側で行う
        this.speed = t.speed;
        this.angle = t.angle;
        this.accel = t.accel;
        this.maxSpeed = t.maxSpeed;
        this.angVel = t.angVel;
    }

    private void UpdateVisualAndCollision()
    {
        Vector3[] drawPoints = new Vector3[points.Count];
        Vector2[] colPoints = new Vector2[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            drawPoints[i] = points[i];
            // EdgeColliderはローカル座標系なので変換
            colPoints[i] = transform.InverseTransformPoint(points[i]);
        }

        lr.positionCount = drawPoints.Length;
        lr.SetPositions(drawPoints);
        edgeCol.points = colPoints;
        edgeCol.edgeRadius = width * 0.5f;
    }
}