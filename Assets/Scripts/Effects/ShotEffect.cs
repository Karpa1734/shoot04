using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

public class ShotEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    [Header("消滅アニメーション用フレーム (8枚)")]
    public Sprite[] breakFrames;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    // 遅延エフェクト（delay.png を使用）
    public IEnumerator PlayDelay(float duration, Sprite delaySprite, float targetScale)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        // 重要な修正：遅延用の画像をここで確実にセット
        sr.sprite = delaySprite;
        sr.color = new Color(1, 1, 1, 0); // 初期状態は透明

        float elapsed = 0;
        float startScale = targetScale * 3.0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, targetScale, t);
            // delay.png は色付きなので、Alphaだけいじる
            sr.color = new Color(1, 1, 1, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    // 消滅エフェクト（etbreak_rss を色付けして使用）
    public IEnumerator PlayBreakAnimation(Color bulletColor, float scale)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        SEManager.Instance.Play(SEPath.BULLETBREAK, 0.5f);
        // 【修正】大きさを1.5倍に設定
        sr.color = bulletColor;
        transform.localScale = Vector3.one * scale * 2.0f;

        // 【修正】アニメーション速度を2倍遅く（全体フレームを2倍に）
        // 0.03f → 0.06f に変更
        float frameTime = 0.09f;

        // 8枚のフレームを順番に流す
        for (int i = 0; i < breakFrames.Length; i++)
        {
            if (sr == null) yield break;

            sr.sprite = breakFrames[i];

            // --- 修正ポイント：透明度の計算 (1.0 から 0.0 へ) ---
            // 現在のコマ数(i)に応じて、アルファ値を徐々に下げる
            float alpha = 1.0f - ((float)i / breakFrames.Length);
            sr.color = new Color(bulletColor.r, bulletColor.g, bulletColor.b, alpha);

            yield return new WaitForSeconds(frameTime);
        }

        Destroy(gameObject);
    }
}