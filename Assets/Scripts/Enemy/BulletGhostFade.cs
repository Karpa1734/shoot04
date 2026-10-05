using UnityEngine;

/// <summary>
/// 👻 生成された残像スプライトを数フレームで透明にしながら自動消滅させる限定ヘルパー
/// </summary>
class BulletGhostFade : MonoBehaviour
{
    private SpriteRenderer sr;
    private float alpha = 0.5f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        alpha -= Time.deltaTime * 1.0f; // 約0.12秒で完全に消滅
        if (alpha <= 0f)
        {
            Destroy(gameObject);
        }
        else if (sr != null)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}