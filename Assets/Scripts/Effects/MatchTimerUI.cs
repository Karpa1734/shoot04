using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MatchTimerUI : MonoBehaviour
{
    public static MatchTimerUI Instance;

    [Header("Match Settings")]
    public float currentMatchTime = 99f; // 現在の残り時間
    private bool isTimerRunning = false;
    private bool isTimerStopped = false; // ★ 追加：タイマー停止フラグ
    [Header("References")]
    public TextMeshProUGUI timerText;
    public CanvasGroup canvasGroup;

    [Header("Color Settings")]
    private Color normalColor = Color.white;
    private Color warningColor = new Color(1f, 128f / 255f, 128f / 255f);
    private Color dangerColor = new Color(1f, 64f / 255f, 64f / 255f);

    private RectTransform rectTransform;
    private int lastIntSecond = -1;
    private Vector3 originalScale;

    void Awake()
    {
        if (Instance == null) Instance = this;
        rectTransform = GetComponent<RectTransform>();
        canvasGroup.alpha = 1f;
        originalScale = rectTransform.localScale;
    }

    public void ResetRoundTimer(float duration)
    {
        currentMatchTime = duration;
        isTimerRunning = false;
        isTimerStopped = false; // ラウンド開始時にリセット
        // ★修正：初期値も UI表示（Ceil）に合わせて同期
        lastIntSecond = Mathf.CeilToInt(currentMatchTime);
        UpdateUI(currentMatchTime);
    }
    // ★ 追加：外部からタイマーを止めるためのメソッド
    public void StopTimer()
    {
        isTimerStopped = true;
    }
    void Update()
    {
        // ★ 修正：isTimerStopped が false の時だけカウントダウンする
        if (PlayerMove.CanInput && currentMatchTime > 0 && !isTimerStopped)
        {
            currentMatchTime -= Time.deltaTime;

            if (currentMatchTime <= 0)
            {
                currentMatchTime = 0;
                HandleTimeUp();
            }
        }
        UpdateUI(currentMatchTime);

        // --- SEとPop演出の同期修正 ---
        // UIが表示する「整数」を取得（例: 10.1秒なら表示は11）
        int displaySec = Mathf.CeilToInt(currentMatchTime);

        // 表示されている数字が変わった瞬間、かつ10秒以下の時に実行
        if (displaySec <= 10 && displaySec != lastIntSecond && currentMatchTime > 0)
        {
            StartCoroutine(PopRoutine());
            PlayCountSE(displaySec);
            lastIntSecond = displaySec;
        }
    }

    void UpdateUI(float time)
    {
        int displaySec = Mathf.CeilToInt(time);
        timerText.text = displaySec.ToString();

        if (time < 5f) timerText.color = dangerColor;
        else if (time < 10f) timerText.color = warningColor;
        else timerText.color = normalColor;
    }

    void PlayCountSE(int sec)
    {
        // 4秒以下で音が緊迫したものに変わる
        string clipPath = (sec > 4) ? SEPath.TIMER1 : SEPath.TIMER2;
        SEManager.Instance.Play(clipPath, 0.5f);
    }

    IEnumerator PopRoutine()
    {
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 popScale = originalScale * 1.3f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rectTransform.localScale = Vector3.Lerp(originalScale, popScale, elapsed / duration);
            yield return null;
        }
        rectTransform.localScale = originalScale;
    }

    private void HandleTimeUp()
    {
        // 1. 全ての入力を即座に停止
        PlayerMove.CanInput = false;

        // 2. ★追加：画面上のすべての弾幕をエフェクト付きで消去
        ClearAllBulletsOnField();

        PlayerStatusManager p1 = null;
        PlayerStatusManager p2 = null;

        foreach (var p in PlayerMove.AllPlayers)
        {
            var status = p.GetComponent<PlayerStatusManager>();
            if (status != null)
            {
                if (status.playerId == 1) p1 = status;
                else if (status.playerId == 2) p2 = status;
            }
        }

        if (p1 != null && p2 != null)
        {
            // HP比較による勝敗判定
            if (p1.currentHP > p2.currentHP)
            {
                TriggerTimeUpWin(p2); // P2の負け演出（P1 Wins!が表示される）
            }
            else if (p2.currentHP > p1.currentHP)
            {
                TriggerTimeUpWin(p1); // P1の負け演出（P2 Wins!が表示される）
            }
            else
            {
                // ドロー（引き分け）の場合は両者をHit状態にする等の処理が必要ならここへ
            }
        }
    }

    /// <summary>
    /// 画面内の全弾丸を一括消去する
    /// </summary>
    private void ClearAllBulletsOnField()
    {
        DanmakuBullet[] pBullets = Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var b in pBullets) b.Deactivate(true);

        EnemyBullet[] eBullets = Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var b in eBullets) b.Deactivate(true);
    }

    private void TriggerTimeUpWin(PlayerStatusManager loserStatus)
    {
        PlayerHitHandler loserHandler = loserStatus.GetComponentInChildren<PlayerHitHandler>();
        if (loserHandler != null)
        {
            // ★ 修正：直接ゲーム終了を呼ぶのではなく、被弾時と同じ「ストック確認ルーチン」を呼ぶ
            loserHandler.currentState = PlayerHitHandler.PlayerState.Hit;
            loserHandler.StartCoroutine("ExplosionAndStunRoutine");
        }
    }
}