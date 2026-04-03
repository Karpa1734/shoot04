using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SpellRingEffect_Line : MonoBehaviour
{
    [Header("References")]
    public EnemyStatus bossStatus;
    private LineRenderer line;

    [Header("Shape Settings")]
    public int segments = 90;          // 円の滑らかさ
    public float maxRadius = 4.5f;     // 開始時の半径
    public float minRadius = 1.2f;     // 終了直前の半径
    public float ringWidth = 0.4f;     // リングの太さ

    [Header("Texture Settings")]
    [Tooltip("画像が円一周で何回繰り返されるか。数値を小さくすると1つ1つの画像が大きくなります")]
    public float textureTiling = 8.0f; // ★ここを 5～10 くらいにすると画像が大きく見えます
    public float scrollSpeed = 2.0f;   // テクスチャが流れる速度

    private float initialTimeLimit;
    private bool isActive = false;
    private float appearanceProgress = 0f;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = segments + 1;
        line.loop = true;
        line.enabled = false;

        // --- 追加：ローカル座標を使用するように設定 ---
        // これにより、(x, y) が「このオブジェクトの中心からの距離」になります
        line.useWorldSpace = false;

        line.textureMode = LineTextureMode.Tile;
    }

    void Update()
    {
        if (!isActive || bossStatus == null) return;

        // --- 追加：常にボスの位置にこのオブジェクトを移動させる ---
        transform.position = bossStatus.transform.position;

        // 1. 画像の大きさとアニメーションを制御
        float offset = -Time.time * scrollSpeed;
        line.material.mainTextureScale = new Vector2(textureTiling, 1);
        line.material.mainTextureOffset = new Vector2(offset, 0);

        // 2. 出現演出 [cite: 58-61]
        if (appearanceProgress < 1.0f)
        {
            appearanceProgress += Time.deltaTime * 1.5f;
        }

        // 3. 半径の計算 [cite: 63-65, 175-177]
        float timeRate = Mathf.Clamp01(bossStatus.currentTimer / initialTimeLimit);
        float currentRadius = Mathf.Lerp(minRadius, maxRadius, timeRate) * appearanceProgress;

        DrawCircle(currentRadius);

        // 4. 消去判定 [cite: 69-70, 150-155]
        if (bossStatus.currentTimer <= 0) Deactivate();
    }
    public void Activate(float timeLimit)
    {
        initialTimeLimit = timeLimit;
        isActive = true;
        line.enabled = true;
        appearanceProgress = 0f;

        // 線幅をリセット
        line.startWidth = ringWidth;
        line.endWidth = ringWidth;
    }

    public void Deactivate()
    {
        isActive = false;
        line.enabled = false;
    }

    void DrawCircle(float radius)
    {
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * (360f / segments) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            line.SetPosition(i, new Vector3(x, y, 0));
        }
    }
}