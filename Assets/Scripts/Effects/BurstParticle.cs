using UnityEngine;

public class BurstParticle : MonoBehaviour
{
    [Header("動きの設定")]
    public Vector3 velocity;
    public float rotationSpeed = 360f;
    // ★外部から上書きされる変数
    public float lifespan = 1.0f;

    private float timer = 0f;
    private SpriteRenderer spriteRenderer;
    private Vector3 currentRotation;
    private Vector3 startScale;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        // ★実行開始時のスケールを記録（外部から scale が設定された後に実行される）
        startScale = transform.localScale;

        currentRotation = new Vector3(
            Random.Range(-rotationSpeed, rotationSpeed),
            Random.Range(-rotationSpeed, rotationSpeed),
            Random.Range(-rotationSpeed, rotationSpeed)
        );
    }

    void Update()
    {
        transform.position += velocity * Time.deltaTime;
        transform.Rotate(currentRotation * Time.deltaTime);

        timer += Time.deltaTime;
        // ★外部から設定された lifespan を使用して進捗を計算
        float progress = Mathf.Clamp01(timer / lifespan);

        transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);

        if (spriteRenderer != null)
        {
            float alpha = 1.0f - progress;
            Color newColor = spriteRenderer.color;
            newColor.a = Mathf.Max(0f, alpha);
            spriteRenderer.color = newColor;
        }

        if (timer >= lifespan)
        {
            Destroy(gameObject);
        }
    }
}