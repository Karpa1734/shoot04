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
    // ★追加：無限タイマーフラグ
    public bool isInfiniteTimer = false;
    // ★追加：二重実行を確実に防ぐフラグ
    private bool isTimeUpHandled = false;
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
        isTimerStopped = false;
        isTimeUpHandled = false; // ★ラウンド開始時にフラグをリセット
        lastIntSecond = Mathf.CeilToInt(currentMatchTime);
        UpdateUI(currentMatchTime);
    }
    // ★ 追加：外部からタイマーを止めるためのメソッド
    public void StopTimer()
    {
        isTimerStopped = true;
    }// タイマーを途中で再開させる（ストーリーモードの復帰用）
    public void ResumeTimer()
    {
        isTimerStopped = false;
    }
    void Update()
    {
        // ★ 修正：isTimerStopped が false の時だけカウントダウンする
        // ★修正：!isInfiniteTimer かつ !isTimerStopped の時だけカウントを減らす
        if (PlayerMove.CanShoot && currentMatchTime > 0 && !isTimerStopped && !isInfiniteTimer)
        {
            isTimerRunning = true;
            currentMatchTime -= Time.deltaTime;

            if (currentMatchTime <= 0)
            {
                currentMatchTime = 0;
                isTimerRunning = false;
                if (!isTimeUpHandled)
                {
                    isTimeUpHandled = true;
                    HandleTimeUp();
                }
            }
        }
        UpdateUI(currentMatchTime);

        // ★修正：無限タイマー時はSEやPop演出を行わないようにする
        if (!isInfiniteTimer)
        {
            int displaySec = Mathf.CeilToInt(currentMatchTime);
            if (displaySec <= 10 && displaySec != lastIntSecond && currentMatchTime > 0)
            {
                StartCoroutine(PopRoutine());
                PlayCountSE(displaySec);
                lastIntSecond = displaySec;
            }
        }

    }

    void UpdateUI(float time)
    {
        // ★追加：無限タイマー時の表示切り替え
        if (isInfiniteTimer)
        {
            timerText.text = "∞";
            timerText.color = normalColor;
            return;
        }

        int displaySec = Mathf.CeilToInt(time);
        timerText.text = displaySec.ToString();

        if (time < 5f) timerText.color = dangerColor;
        else if (time < 10f) timerText.color = warningColor;
        else timerText.color = normalColor;
    }
    // ★追加：ストーリーモード開始時などに外部から呼ぶためのメソッド
    public void SetInfiniteMode(bool infinite)
    {
        isInfiniteTimer = infinite;
        UpdateUI(currentMatchTime);
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
        PlayerMove.CanInput = false;
        ClearAllBulletsOnField();

        // ストーリーモードの場合
        if (GameModeManager.IsStoryMode)
        {
            HandleStoryTimeUp();
            return;
        }
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
            if (p1.currentHP > p2.currentHP)
            {
                TriggerTimeUpWin(p2);
            }
            else if (p2.currentHP > p1.currentHP)
            {
                TriggerTimeUpWin(p1);
            }
            else
            {
                // ★ 追加：引き分け（HPが同じ）
                // P1側のHitHandlerを窓口にして引き分け演出を開始する
                PlayerHitHandler h1 = p1.GetComponentInChildren<PlayerHitHandler>();
                if (h1 != null) h1.StartCoroutine("TriggerDrawSequence");
            }
        }
    }
    private void HandleStoryTimeUp()
    {
        // 1. 全入力・射撃を即座に停止
        PlayerMove.CanInput = false;
        PlayerMove.CanShoot = false;
        ClearAllBulletsOnField();

        // 2. ボス（敵）の情報を取得
        // 通常、ストーリーモードの敵は playerId = 2 または BossTimerUI のターゲットとして存在します
        EnemyStatus boss = BossTimerUI.Instance != null ? BossTimerUI.Instance.targetStatus : null;

        if (boss != null)
        {
            // ★ 修正：HPの多寡に関わらず、タイムアップ時はボスの負けとする
            PlayerHitHandler bossHandler = boss.GetComponentInChildren<PlayerHitHandler>();
            if (bossHandler != null)
            {
                // ボスの撃墜ルーチンを開始（これによりボスの残機が減り、次のスペルへ移行する）
                bossHandler.currentState = PlayerHitHandler.PlayerState.Hit;
                bossHandler.StartCoroutine("ExplosionAndStunRoutine");
            }
        }
        else
        {
            // ボスが見つからない場合の保険：全プレイヤーから playerId 2 を探す
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null) continue;
                PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>();
                if (ps != null && ps.playerId == 2)
                {
                    PlayerHitHandler hh = p.GetComponentInChildren<PlayerHitHandler>();
                    if (hh != null)
                    {
                        hh.currentState = PlayerHitHandler.PlayerState.Hit;
                        hh.StartCoroutine("ExplosionAndStunRoutine");
                    }
                    break;
                }
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
            // 状態をHitにして爆発ルーチン開始
            loserHandler.currentState = PlayerHitHandler.PlayerState.Hit;
            loserHandler.StartCoroutine("ExplosionAndStunRoutine");
        }
    }
}