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
        if (UnityEngine.Object.FindAnyObjectByType<DanmakuAgent>() != null && Unity.MLAgents.Academy.Instance.IsCommunicatorOn)
        {
            PlayerMove.CanInput = true;
            PlayerMove.CanShoot = true;
            if (MatchTimerUI.Instance != null)
            {
                MatchTimerUI.Instance.ResetRoundTimer(99f);
                MatchTimerUI.Instance.StartMatchTimer(); // 🌟 学習モード時は即座にタイマー開始
            }
            return;
        }

        // 動作中のコルーチンを全て止めてから開始
        StopAllCoroutines();
        StartCoroutine(CountdownRoutine());
        StartCoroutine(ConstantClearRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        PlayerMove.CanInput = true;
        PlayerMove.CanShoot = false;

        // 🌟 カウントダウン開始時は、タイマーをリセットしつつ「停止状態（待機）」にしておく
        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.ResetRoundTimer(99f);
        }

        countdownText.gameObject.SetActive(true);
        countdownText.text = "";

        Color c = countdownText.color;
        c.a = 1f;
        countdownText.color = c;

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(AnimateText("3"));
        yield return StartCoroutine(AnimateText("2"));
        yield return StartCoroutine(AnimateText("1"));

        // ★ 修正：ここで解禁。移動とショットの両方を許可する
        PlayerMove.CanInput = true;
        PlayerMove.CanShoot = true;

        // 🌟【最重要】：カウントダウンが明け、「Go Shoot !!」の文字が出てバトルが始まったこの瞬間にタイマーを起動！
        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.StartMatchTimer();
        }

        yield return StartCoroutine(AnimateText("Go Shoot !!", 1.5f));

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
            // =========================================================================
            // 🎯【最核心修正：ポーズ時間軸完全調停ガード】
            // 💡 理由：PauseManagerによってゲームが一時停止（Time.timeScale == 0f）している間は、
            //          unscaledDeltaTimeの加算をスキップしてコルーチンをその場で完全フリーズさせます。
            //          被弾スロー（0.3f）の時は通常通り等速でカウントが進みます。
            // =========================================================================
            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                yield return null; // ポーズ中は時間の蓄積を行わず次のフレームまで待機
                continue;
            }

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