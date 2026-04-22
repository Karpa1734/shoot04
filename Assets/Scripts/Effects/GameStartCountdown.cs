// --- GameStartCountdown.cs 修正版 ---
using UnityEngine;
using TMPro;
using System.Collections;

public class GameStartCountdown : MonoBehaviour
{
    public static GameStartCountdown Instance;

    public TextMeshProUGUI countdownText;
    public float fontSizePulse = 1.2f;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // 最初の開始（既にInstanceはあるのでそのまま呼ぶ）
        StartCountdown();
    }

    public void StartCountdown()
    {
        // 動作中のコルーチンを全て止めてから開始
        StopAllCoroutines();
        StartCoroutine(CountdownRoutine());
        // ★ 追加：カウントダウン中、弾幕を消し続けるループを開始
        StartCoroutine(ConstantClearRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        PlayerMove.CanShoot = false; // ショット禁止
        PlayerMove.CanInput = true;  // ★ 移動は許可！

        // テキストをアクティブにして初期化
        countdownText.gameObject.SetActive(true);
        countdownText.text = "";

        // ★重要：アルファ値を1（不透明）に強制リセット
        Color c = countdownText.color;
        c.a = 1f;
        countdownText.color = c;

        // 暗転明け直後の待機（必要に応じて調整）
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(AnimateText("3"));
        yield return StartCoroutine(AnimateText("2"));
        yield return StartCoroutine(AnimateText("1"));

        PlayerMove.CanInput = true; // ここで操作解禁

        PlayerMove.CanShoot = true;
        yield return StartCoroutine(AnimateText("GO !!", 1.5f));

        countdownText.gameObject.SetActive(false);
    }
    // ★ 追加：ショット禁止期間中に弾幕を掃除し続ける
    private IEnumerator ConstantClearRoutine()
    {
        while (!PlayerMove.CanShoot)
        {
            ClearAllBulletsOnField();
            // 0.1秒おきに画面内の弾を探して消去
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    private void ClearAllBulletsOnField()
    {
        // プレイヤーの弾
        var bullets = Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var b in bullets) b.Deactivate(false); // エフェクトなしで静かに消す

        // 敵・AIの弾（もしあれば）
        var eBullets = Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var b in eBullets) b.Deactivate(false);
    }
    IEnumerator AnimateText(string text, float scaleMult = 1f)
    {
        countdownText.text = text;

        // 開始時に色を不透明に戻す
        Color c = countdownText.color;
        c.a = 1f;
        countdownText.color = c;

        Vector3 baseScale = Vector3.one * scaleMult;
        float elapsed = 0;
        float duration = 0.8f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // スケールアニメーション
            countdownText.transform.localScale = baseScale * Mathf.Lerp(fontSizePulse, 1f, t);

            // フェードアウト演出
            if (elapsed > 0.5f)
            {
                float fadeT = (elapsed - 0.5f) / (duration - 0.5f);
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                countdownText.color = c;
            }

            elapsed += Time.unscaledDeltaTime; // スロー中でも一定速度で動かす
            yield return null;
        }
    }
}