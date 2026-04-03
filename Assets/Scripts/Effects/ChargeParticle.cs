using UnityEngine;

public class ChargeParticle : MonoBehaviour
{
    private Vector3 targetPosition; // Transform から Vector3 に変更
    [Header("Settings")]
    public float rotationSpeed = 360f;
    public float minLifespan = 0.3f;
    public float maxLifespan = 0.6f;

    private float speed;
    private float lifespan;
    private float timer = 0f;
    private SpriteRenderer spriteRenderer;
    private Vector3 currentRotation;

    public void Initialize(Vector3 targetPos, Color color)
    {
        targetPosition = targetPos;
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = color;

        // 初期半径の設定
        float initialRadius = Random.Range(4f, 6f);
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * initialRadius;

        // ターゲット座標からのオフセット位置に配置
        transform.position = targetPosition + offset;

        currentRotation = new Vector3(
            Random.Range(-rotationSpeed, rotationSpeed),
            Random.Range(-rotationSpeed, rotationSpeed),
            Random.Range(-rotationSpeed, rotationSpeed)
        );

        lifespan = Random.Range(minLifespan, maxLifespan);
        speed = initialRadius / lifespan;
    }

    void Update()
    {
        timer += Time.deltaTime;

        // ターゲット座標に向かって直進
        Vector3 direction = (targetPosition - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;

        transform.Rotate(currentRotation * Time.deltaTime);

        if (spriteRenderer != null)
        {
            float alpha = 1.0f - (timer / lifespan);
            Color newColor = spriteRenderer.color;
            newColor.a = Mathf.Max(0f, alpha);
            spriteRenderer.color = newColor;
        }

        if (timer >= lifespan || Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            Destroy(gameObject);
        }
    }
}