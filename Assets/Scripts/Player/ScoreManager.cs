using System.Collections;
using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI grazeText;
    public TextMeshProUGUI highScoreText; // ★追加：ハイスコア表示用UI

    [Header("Settings")]
    public float rollingDuration = 2.0f;

    private long currentScore = 0;
    private long displayScore = 0;
    private long highScore = 0;      // ★追加：内部ハイスコア
    private int grazeCount = 0;
    private Coroutine scoreCoroutine;
    // ★追加：現在画面に表示されているハイスコアの数値
    private long visualHighScore = 0;
    void Awake()
    {
        if (Instance == null) Instance = this;

        // ★追加：セーブデータからハイスコアを読み込む
        // SaveDataに "HighScore" というキーが設定されている必要があります
        highScore = SaveManager.Load<long>("HighScore");
        // ★初期化：表示用ハイスコアもロードした値に合わせる
        visualHighScore = highScore;
        UpdateScoreText(0);
        UpdateGrazeText(0);
        UpdateHighScoreText(highScore); // ★追加：ハイスコアの初期表示
    }
    public void AddGraze()
    {
        // ポーズ中などは加算しない判定
        if (Time.timeScale <= 0) return;

        grazeCount++;
        UpdateGrazeText(grazeCount);
    }

    private void UpdateGrazeText(int count)
    {
        if (grazeText != null)
        {
            grazeText.text = count.ToString();
        }
    }

    public void AddScore(long amount)
    {
        if (Time.timeScale <= 0) return;

        long adjustedAmount = (amount / 10) * 10;
        currentScore += adjustedAmount;

        // ★追加：現在のスコアがハイスコアを抜いたかチェック
        if (currentScore > highScore)
        {
            highScore = currentScore;

            // リアルタイムで保存したい場合はここでSaveを呼ぶ
            // SaveManager.Save<long>("HighScore", highScore);
        }

        if (scoreCoroutine != null) StopCoroutine(scoreCoroutine);
        scoreCoroutine = StartCoroutine(ScoreRollingRoutine());
    }

    // ★追加：ハイスコアを保存するメソッド
    // ゲームオーバー時やポーズメニューからタイトルに戻る際に呼ぶ
    public void SaveHighScore()
    {
        SaveManager.Save<long>("HighScore", highScore);
        Debug.Log($"High Score Saved: {highScore}");
    }

    private void UpdateHighScoreText(long score)
    {
        if (highScoreText != null)
        {
            highScoreText.text = score.ToString("N0");
        }
    }

    /// <summary>
    /// 指定時間かけて表示スコアを実スコアに近づける
    /// </summary>
    IEnumerator ScoreRollingRoutine()
    {
        long startScore = displayScore;
        float elapsed = 0f;

        while (elapsed < rollingDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rollingDuration;

            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            displayScore = (long)Mathf.Lerp(startScore, currentScore, easedT);
            long visualScore = (displayScore / 10) * 10;

            // 1. スコアテキストを更新
            UpdateScoreText(visualScore);

            // 2. ★ハイスコア表示の同期処理
            // 「表示上のスコア」が「表示上のハイスコア」を上回った時だけ、ハイスコアも増やす
            if (visualScore > visualHighScore)
            {
                visualHighScore = visualScore;
                UpdateHighScoreText(visualHighScore);
            }

            yield return null;
        }

        displayScore = currentScore;
        UpdateScoreText(currentScore);

        // 3. ★最後に確実にハイスコアを最終的な値に合わせる
        if (currentScore > visualHighScore)
        {
            visualHighScore = currentScore;
            UpdateHighScoreText(visualHighScore);
        }

        scoreCoroutine = null;
    }

    private void UpdateScoreText(long score)
    {
        if (scoreText != null)
        {
            // カンマ区切り（N0）で表示
            scoreText.text = score.ToString("N0");
        }
    }
}