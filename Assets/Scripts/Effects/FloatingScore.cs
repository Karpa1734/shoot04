using UnityEngine;
using TMPro;

public class FloatingScore : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float lifeTime = 0.8f;

    private TextMeshPro textMesh;
    private Color originalColor;
    private float timer = 0f;

    void Awake()
    {
        // 子要素からTMPを取得
        textMesh = GetComponentInChildren<TextMeshPro>();
        if (textMesh != null) originalColor = textMesh.color;
    }

    void Update()
    {
        // 1. 上に移動
        transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);

        // 2. 徐々に透明にする
        timer += Time.deltaTime;
        float alpha = 1f - (timer / lifeTime);
        if (textMesh != null)
        {
            textMesh.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
        }

        // 3. 指定時間で消滅
        if (timer >= lifeTime) Destroy(gameObject);
    }
}