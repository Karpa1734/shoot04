using UnityEngine;

public class PetalLogic : MonoBehaviour
{
    [Header("動きの設定")]
    [Tooltip("この花びらの初期速度（ExplosionManagerから設定される）")]
    public Vector3 velocity;
    [Tooltip("3D回転速度の範囲 (x, y, zの各軸回りの回転速度)")]
    public Vector3 rotationSpeedRange = new Vector3(360f, 360f, 360f);
    [Tooltip("花びらの生存時間（秒）")]
    public float lifespan = 2.0f;

    private Vector3 currentRotationSpeed; // この花びらの具体的な回転速度
    private float timer = 0f;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        // 3D（立体的に）回転するように、各軸回りの回転速度をランダムに決定
        currentRotationSpeed = new Vector3(
            Random.Range(-rotationSpeedRange.x, rotationSpeedRange.x),
            Random.Range(-rotationSpeedRange.y, rotationSpeedRange.y),
            Random.Range(-rotationSpeedRange.z, rotationSpeedRange.z)
        );

        spriteRenderer = GetComponent<SpriteRenderer>();

        // 初期段階では少し「咲いている」感を出すため、ランダムな初期回転を与える
        transform.rotation = Quaternion.Euler(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f)
        );
    }

    void Update()
    {
        // --- 1. 移動の処理 ---
        // 速度に基づいて位置を更新
        transform.Translate(velocity * Time.deltaTime, Space.World);

        // --- 2. 回転の処理 ---
        // 各軸回りの回転速度に基づいて、立体的に回転
        transform.Rotate(currentRotationSpeed * Time.deltaTime, Space.Self);

        // --- 3. 生存時間とフェードアウトの処理 ---
        timer += Time.deltaTime;

        // 徐々にフェードアウト（アルファ値を下げる）
        if (spriteRenderer != null)
        {
            float alpha = 1.0f - (timer / lifespan);
            Color newColor = spriteRenderer.color;
            newColor.a = Mathf.Max(0f, alpha); // 0未満にならないようにする
            spriteRenderer.color = newColor;
        }

        // 生存時間を過ぎたらオブジェクトを削除
        if (timer >= lifespan)
        {
            Destroy(gameObject);
        }
    }
}