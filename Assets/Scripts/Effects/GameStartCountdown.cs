using UnityEngine;
using TMPro;
using System.Collections;
using KanKikuchi.AudioManager;

public class GameStartCountdown : MonoBehaviour
{
    public TextMeshProUGUI countdownText;
    public float fontSizePulse = 1.2f; // 表示時の拡大倍率

    void Start()
    {
        countdownText.gameObject.SetActive(false);
        StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        // 開始時は入力をロック
        PlayerMove.CanInput = false;
        yield return new WaitForSeconds(1f); // 少し待ってからカウントダウン開始  
        countdownText.gameObject.SetActive(true);
        // 3 -> 2 -> 1
        yield return StartCoroutine(AnimateText("3"));
        yield return StartCoroutine(AnimateText("2"));
        yield return StartCoroutine(AnimateText("1"));

        // GO Shoot!! (ここから動ける)
        PlayerMove.CanInput = true;
        yield return StartCoroutine(AnimateText("GO Shoot!!", 1.5f));

        countdownText.gameObject.SetActive(false);
    }

    IEnumerator AnimateText(string text, float scaleMult = 1f)
    {
        // テキストとアルファ値の初期化
        countdownText.text = text;
        Color initialColor = countdownText.color;
        initialColor.a = 1f; // 完全に表示
        countdownText.color = initialColor;

        float elapsed = 0;
        float duration = 0.8f;      // 1文字あたりの合計時間
        float fadeStartTime = 0.5f; // フェードアウトを開始するタイミング
        Vector3 baseScale = Vector3.one * scaleMult;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // --- 演出1: スケール（パンチ演出） ---
            // 最初は大きく、徐々に baseScale へ
            countdownText.transform.localScale = baseScale * Mathf.Lerp(fontSizePulse, 1f, t);

            // --- 演出2: フェードアウト ---
            // fadeStartTime を過ぎたら透明度を下げていく
            if (elapsed > fadeStartTime)
            {
                float fadeT = (elapsed - fadeStartTime) / (duration - fadeStartTime);
                Color c = countdownText.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                countdownText.color = c;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 念のため最後は完全に透明にする
        Color finalColor = countdownText.color;
        finalColor.a = 0f;
        countdownText.color = finalColor;
    }
}