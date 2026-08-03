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
    private bool isMatchStarted = false; // 🌟 試合が正式にスタートしたかを示す専用フラグ
    // =========================================================================
    // 🌟【新規追加】：元のデータを壊さないためのVJT専用ツインタイマー拡張スロット
    // =========================================================================
    [Header("--- VJT Spell Timer Expansion Slots ---")]
    [Tooltip("Unityインスペクターから、新設したVJT専用のTextMeshProUGUIをここにドラッグ＆ドロップしてください")]
    public TextMeshProUGUI vjtTimerText;

    private Coroutine vjtTimerCoroutine = null;

    // 🌟 色味の指定：微妙な灰色（64/255 = 約0.25f）をデフォルトカラーに設定
    private Color vjtDefaultColor = new Color(1,1,1, 1f);

    // 🌟 VJTタイマー専用のPopアニメーション連動ワークアセット
    private RectTransform vjtRectTransform;
    private Vector3 vjtOriginalScale = Vector3.one;
    private int vjtLastIntSecond = -1;

    void Awake()
    {
        if (Instance == null) Instance = this;
        rectTransform = GetComponent<RectTransform>();
        canvasGroup.alpha = 1f;
        originalScale = rectTransform.localScale;

        if (vjtTimerText != null)
        {
            vjtTimerText.gameObject.SetActive(false);
            vjtRectTransform = vjtTimerText.GetComponent<RectTransform>();
            vjtOriginalScale = vjtRectTransform.localScale;
        }
    }

    public void ResetRoundTimer(float duration)
    {
        currentMatchTime = duration;
        isTimerRunning = false;
        isTimerStopped = false;
        isMatchStarted = false; // 🌟 ラウンド初期化時は試合未開始状態にする
        isTimeUpHandled = false;
        lastIntSecond = Mathf.CeilToInt(currentMatchTime);
        vjtLastIntSecond = -1;

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        UpdateUI(currentMatchTime);

        StopVJTVisualTimer();
    }
    /// <summary>
    /// 🌟 カウントダウン終了後、正式にバトルのタイマーを開始する
    /// </summary>
    public void StartMatchTimer()
    {
        isMatchStarted = true;
        isTimerStopped = false;
        Debug.Log("<color=lime>⏱️ [MatchTimerUI] 正式にバトルのタイマー駆動を開始します。</color>");
    }
    // ★ 修正：外部からタイマーを止めるためのメソッド（タイムアップ時は横棒を完全ガード）
    public void StopTimer()
    {
        // 🌟【最重要修正】：ストーリーモードのときは、スペルカード（領域）中であってもメインタイマーを絶対に止めない！
        if (GameModeManager.IsStoryMode)
        {
            return;
        }

        // 🚨【タイムアップ最優先ガード】：試合時間が既に 0 以下、またはタイムアップ処理が完了している場合は、
        // 🚨 VJT中の Update からの上書き呼び出しを完全にシャットアウト（無視）して、通常数字を死守します！
        if (currentMatchTime <= 0f || isTimeUpHandled) return;

        // 多重実行防止
        if (isTimerStopped) return;

        isTimerStopped = true;

        // 🌟【仕様適合】：メインタイマーを半透明(アルファ0.4)にし、TMPの<s>タグで美しい横棒を描画！
        if (canvasGroup != null) canvasGroup.alpha = 0.4f;

        if (!isInfiniteTimer && timerText != null)
        {
            int displaySec = Mathf.CeilToInt(currentMatchTime);
            timerText.text = $"<s>{displaySec}</s>"; // <s>はTextMeshProの取り消し線タグ
        }

        // 🌟【ツインタイマー起動】：現在発動したプレイヤーの内部タイマーを自動スキャンしてミリ秒同期開始
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            var status = p.GetComponent<PlayerStatusManager>();
            if (status != null && status.isSpellCardActive)
            {
                StartVJTVisualTimer(status);
                break;
            }
        }
    }
    public void ResumeTimer()
    {
        isTimerStopped = false;

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        UpdateUI(currentMatchTime);

        StopVJTVisualTimer();
    }

    void Update()
    {
        // 🌟【最重要修正】：ResetRoundTimer直後やカウントダウン中（isMatchStarted = false）の間は、
        // どんな条件であってもタイマーを絶対に進めない！
        bool isBattleTimeActive = isMatchStarted && currentMatchTime > 0 && !isTimerStopped && !isInfiniteTimer;

        if (isBattleTimeActive)
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
            UpdateUI(currentMatchTime);
        }
        else
        {
            isTimerRunning = false;
        }

        if (isBattleTimeActive)
        {
            // 🌟 ここも Mathf.CeilToInt から Mathf.FloorToInt へ合わせる
            int displaySec = Mathf.FloorToInt(currentMatchTime);
            if (displaySec <= 10 && displaySec != lastIntSecond && currentMatchTime > 0)
            {
                StartCoroutine(PopRoutine(rectTransform, originalScale));
                PlayCountSE(displaySec);
                lastIntSecond = displaySec;
            }
        }
    }
    /// <summary>
    /// 🌟 対戦終了時、会話中やリザルト中を含めてタイマーを完全に終了・停止させる
    /// </summary>
    public void StopMatch()
    {
        isMatchStarted = false; // 試合中フラグを強制折りにする
        isTimerStopped = true;
        Debug.Log("<color=orange>⏱️ [MatchTimerUI] 対戦終了を検知。タイマーの駆動を完全に停止しました。</color>");
    }
    void UpdateUI(float time)
    {
        if (isTimerStopped) return;

        if (isInfiniteTimer)
        {
            timerText.text = "∞";
            timerText.color = normalColor;
            return;
        }

        // 🌟【修正】：CeilToInt（切り上げ）だと0秒の手前で長い時間「1」になってしまうため、
        // RoundToInt（四捨五入）または FloorToInt（切り捨て）に変更して0秒のタイミングを正確にします。
        int displaySec = Mathf.FloorToInt(time); // または Mathf.RoundToInt(time)
        timerText.text = displaySec.ToString();

        if (time < 5f) timerText.color = dangerColor;
        else if (time < 10f) timerText.color = warningColor;
        else timerText.color = normalColor;
    }

    // =========================================================================
    // 🌟【ツインタイマー制御コア ＆ 小数点リッチテキスト動的縮小エンジン】
    // =========================================================================
    private void StartVJTVisualTimer(PlayerStatusManager activeVJTPlayer)
    {
        // 🌟【最重要修正】：ストーリーモードのときは、領域用（VJT）ツインタイマーを使用しないため即座にシャットアウト！
        if (GameModeManager.IsStoryMode)
        {
            if (vjtTimerText != null) vjtTimerText.gameObject.SetActive(false);
            return;
        }

        if (vjtTimerCoroutine != null) StopCoroutine(vjtTimerCoroutine);
        vjtTimerCoroutine = StartCoroutine(VJTSpeelTimerRoutine(activeVJTPlayer));
    }

    private void StopVJTVisualTimer()
    {
        if (vjtTimerCoroutine != null)
        {
            StopCoroutine(vjtTimerCoroutine);
            vjtTimerCoroutine = null;
        }
        if (vjtTimerText != null)
        {
            vjtTimerText.gameObject.SetActive(false);
        }
        vjtLastIntSecond = -1;
    }

    // =========================================================================
    // 🔮【リアルタイム完全独立駆動エンジン】：
    // 💡 相手が被弾して Time.timeScale = 0.3f のスローモーション演出に入っても、
    // 💡 領域展開の残り時間だけは Time.unscaledDeltaTime を用いて現実世界の1秒等速で正確に減算します！
    // =========================================================================
    private IEnumerator VJTSpeelTimerRoutine(PlayerStatusManager activeVJTPlayer)
    {
        // 🌟【念のための二重ガード】：ストーリーモードであればコルーチンを即座に安全脱出
        if (GameModeManager.IsStoryMode || vjtTimerText == null) yield break;

        vjtTimerText.color = vjtDefaultColor;
        vjtTimerText.gameObject.SetActive(true);
        vjtLastIntSecond = -1;

        while (activeVJTPlayer != null && activeVJTPlayer.isSpellCardActive && activeVJTPlayer.spellHP > 0f)
        {
            // 🚨【重要】：相手の被弾スロー中に、プレイヤー自身の内部タイマー（spellTimer）も
            // 🚨 現実世界の絶対時間で等速減算されるように、ここで直接 unscaledDeltaTime を用いて手動調停・上書き減算します！
            if (Time.timeScale < 1.0f && Time.timeScale > 0f)
            {
                // スローモーション中（0.3倍）に失われる「リアルな実時間」の差分を逆算して直接タイマーを消費させます
                activeVJTPlayer.spellTimer -= (Time.unscaledDeltaTime - Time.deltaTime);
            }

            float remainingTime = activeVJTPlayer.spellTimer;

            // 負の数に入らないように安全クランプ
            if (remainingTime < 0f) remainingTime = 0f;

            int seconds = Mathf.FloorToInt(remainingTime);
            int fraction = Mathf.FloorToInt((remainingTime - seconds) * 100f);

            // 小数点以下2桁を70%に縮小表示する美麗リッチテキスト
            vjtTimerText.text = $"{seconds}.<size=70%>{fraction:D2}</size>";

            // 残り時間が10秒以下になった時の演出同期
            int displayVjtSec = Mathf.CeilToInt(remainingTime);
            if (displayVjtSec <= 10 && remainingTime > 0f)
            {
                if (remainingTime < 5f) vjtTimerText.color = dangerColor;
                else if (remainingTime < 10f) vjtTimerText.color = warningColor;

                if (displayVjtSec != vjtLastIntSecond)
                {
                    if (vjtRectTransform != null)
                    {
                        // 拡縮アニメーションもタイムスケールを無視して unscaled 制御へ
                        StartCoroutine(PopUnscaledRoutine(vjtRectTransform, vjtOriginalScale));
                    }
                    PlayCountSE(displayVjtSec);
                    vjtLastIntSecond = displayVjtSec;
                }
            }
            else
            {
                vjtTimerText.color = vjtDefaultColor;
            }

            // 💡 スローモーション中であっても、毎フレーム「現実世界の処理速度」のまま最速でループを回します
            yield return null;
        }

        vjtTimerText.gameObject.SetActive(false);
        vjtTimerCoroutine = null;
    }

    /// <summary>
    /// 💡 新設：Time.timeScaleの影響を受けずに、10秒以下のタイマーを美しくPop拡縮させるリアルタイム専用コルーチン
    /// </summary>
    private IEnumerator PopUnscaledRoutine(RectTransform targetRect, Vector3 origScale)
    {
        if (targetRect == null) yield break;
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 popScale = origScale * 1.3f;
        while (elapsed < duration)
        {
            // Time.deltaTime ではなく Time.unscaledDeltaTime を使うことで、スロー中も等速でアニメーションが回ります！
            elapsed += Time.unscaledDeltaTime;
            targetRect.localScale = Vector3.Lerp(origScale, popScale, elapsed / duration);
            yield return null;
        }
        targetRect.localScale = origScale;
    }

    public void SetInfiniteMode(bool infinite)
    {
        isInfiniteTimer = infinite;
        UpdateUI(currentMatchTime);
    }

    void PlayCountSE(int sec)
    {
        string clipPath = (sec > 4) ? SEPath.TIMER1 : SEPath.TIMER2;
        SEManager.Instance.Play(clipPath, 0.5f);
    }

    // 🌟 汎用化したPop拡縮コルーチン（メイン・VJTの双方で安全に使い回します）
    IEnumerator PopRoutine(RectTransform targetRect, Vector3 origScale)
    {
        if (targetRect == null) yield break;
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 popScale = origScale * 1.3f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            targetRect.localScale = Vector3.Lerp(origScale, popScale, elapsed / duration);
            yield return null;
        }
        targetRect.localScale = origScale;
    }

    private void HandleTimeUp()
    {
        PlayerMove.CanInput = false;
        ClearAllBulletsOnField();

        if (GameModeManager.IsStoryMode)
        {
            HandleStoryTimeUp();
            return;
        }

        // =========================================================================
        // 🧠【強化学習専用：タイムアップ即死即リセットインフラ】
        // =========================================================================
        bool isTrainingMode = false;
        foreach (var p in PlayerMove.AllPlayers)
        {
            // 💡【修正】：エージェントがシーンにいて、かつ外部学習通信（IsCommunicatorOn）がONの時だけリセットを実行
            if (p != null && p.GetComponentInChildren<DanmakuAgent>() != null && Unity.MLAgents.Academy.Instance.IsCommunicatorOn)
            {
                isTrainingMode = true;
            }
        }

        if (isTrainingMode)
        {
            // 1. スローモーションを完全に潰し、コマンド通りの爆速を維持
            Time.timeScale = 1.0f;

            // 2. 領域（VJT）の強制解除
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null) continue;
                PlayerStatusManager status = p.GetComponent<PlayerStatusManager>();
                if (status != null && status.isSpellCardActive) status.DeactivateSpellCard(false);
            }

            // 3. エージェントにエピソード終了を通達（時間切れドロー、またはこの時点のHP割合でペナルティを振り分けてもOK）
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null) continue;
                DanmakuAgent agent = p.GetComponentInChildren<DanmakuAgent>();
                if (agent != null) agent.EndEpisode(); // エピソード終了

                // ステータス、マナを全回復
                PlayerStatusManager status = p.GetComponent<PlayerStatusManager>();
                if (status != null) status.currentHP = status.maxHP;
                SkillManager sm = p.GetComponentInChildren<SkillManager>();
                if (sm != null) sm.InstantFullRecovery();
            }

            // 4. お互いを一瞬で初期配置にワープ（巡航演出スキップ）
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p == null) continue;
                PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>();
                if (ps != null)
                {
                    float targetX = (ps.playerId == 2) ? 3.5f : -3.5f;
                    p.transform.position = new Vector3(targetX, 0f, 0f);
                }
            }

            // 5. カウントダウンなしで即座に次の試合を開始！
            PlayerMove.CanInput = true;
            PlayerMove.CanShoot = true;
            ResetRoundTimer(99f);
            return; // 💡 演出ルートを完全遮断してここで脱出！
        }

        // =========================================================================
        // 🎬【通常ルート】（人間用のタイムアップ判定）
        // =========================================================================
        if (PlayerMove.AllPlayers != null && PlayerMove.AllPlayers.Count > 0)
        {
            PlayerHitHandler referee = PlayerMove.AllPlayers[0].GetComponentInChildren<PlayerHitHandler>();
            if (referee != null)
            {
                Debug.Log("<color=orange>⏳ [MatchTimerUI] タイムアップを検知。PlayerHitHandlerの割合ジャッジシステムを起動します。</color>");
                referee.EvaluateTimeUpVictory();
            }
            else
            {
                // 万が一コンポーネントが見つからなかった場合の安全なフォールバック（ドロー救済）
                Debug.LogWarning("PlayerHitHandler not found on TimeUp. Failsafe draw triggered.");
                foreach (var p in PlayerMove.AllPlayers)
                {
                    var h = p.GetComponentInChildren<PlayerHitHandler>();
                    if (h != null) { h.StartCoroutine("TriggerDrawSequence"); break; }
                }
            }
        }
    }

    private void HandleStoryTimeUp()
    {
        PlayerMove.CanInput = false;
        PlayerMove.CanShoot = false;
        ClearAllBulletsOnField();

        EnemyStatus boss = BossTimerUI.Instance != null ? BossTimerUI.Instance.targetStatus : null;

        if (boss != null)
        {
            PlayerHitHandler bossHandler = boss.GetComponentInChildren<PlayerHitHandler>();
            if (bossHandler != null)
            {
                bossHandler.currentState = PlayerHitHandler.PlayerState.Hit;
                bossHandler.StartCoroutine("ExplosionAndStunRoutine");
            }
        }
        else
        {
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

    private void ClearAllBulletsOnField()
    {
        DanmakuBullet[] pBullets = Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        // 💡 force: true を渡して、ラウンド終了時は不滅弾も一斉強制消去！
        foreach (var b in pBullets) b.Deactivate(true,true);

        EnemyBullet[] eBullets = Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var b in eBullets) b.Deactivate(true);
    }

    private void TriggerTimeUpWin(PlayerStatusManager loserStatus)
    {
        PlayerHitHandler loserHandler = loserStatus.GetComponentInChildren<PlayerHitHandler>();
        if (loserHandler != null)
        {
            loserHandler.currentState = PlayerHitHandler.PlayerState.Hit;
            loserHandler.StartCoroutine("ExplosionAndStunRoutine");
        }
    }
}