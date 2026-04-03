using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BossStarItem : MonoBehaviour
{
    private Image img;

    void Awake() => img = GetComponent<Image>();

    // 出現演出: 30フレームでフェードイン [cite: 101, 102]
    public void FadeIn()
    {
        StartCoroutine(FadeRoutine(0, 1, 1.0f, 0.5f, false));
    }

    // 消滅演出: 30フレームでフェードアウト + 拡大 [cite: 103, 105]
    public void Break()
    {
        StartCoroutine(FadeRoutine(1, 0, 2.0f, 0.5f, true));
    }

    IEnumerator FadeRoutine(float startAlpha, float endAlpha, float targetScale, float duration, bool destroyAtEnd)
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // アルファ値の変更 [cite: 102, 105]
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(startAlpha, endAlpha, t));

            // スケールの変更 
            transform.localScale = Vector3.Lerp(startScale, Vector3.one * targetScale, t);

            yield return null;
        }

        if (destroyAtEnd) Destroy(gameObject);
    }
}