using KanKikuchi.AudioManager;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// プレイヤーの被弾、食らいボム、復活処理を管理するクラス（横STG対戦用）
/// </summary>
public class PlayerHitHandler : MonoBehaviour
{
    public enum PlayerState { Normal, DeathBomb, Hit, Down, Rebirth }
    public PlayerState currentState = PlayerState.Normal;

    [Header("Settings")]
    public float deathBombWindow = 0.15f;
    public float invincibilityTime = 2.0f;
    public float downTime = 0.8f;
    public float stunTime = 2.0f; // スタン時間（2秒）
    [Header("References")]
    public GameObject explosionEffectPrefab;
    public PlayerAnimation playerAnim;
    public PlayerMove playerMove;
    public GameObject bulletClearPrefab;

    [Header("Multiplayer Support")]
    public PlayerStatusManager myStatusManager;

    private SpriteRenderer characterRenderer;
    private ItemEffectHandler itemHandler;

    void Awake()
    {
        if (playerMove == null) playerMove = GetComponentInParent<PlayerMove>();
        if (playerAnim == null) playerAnim = GetComponentInParent<PlayerAnimation>();

        itemHandler = GetComponent<ItemEffectHandler>();
        characterRenderer = GetComponentInParent<SpriteRenderer>();

        if (characterRenderer == null)
        {
            characterRenderer = transform.parent.GetComponentInChildren<SpriteRenderer>();
        }
    }

    void Update()
    {
        if (playerMove != null && playerAnim != null)
        {
            playerAnim.isInvincible = playerMove.IsInvincible;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Item"))
        {
            if (itemHandler != null) itemHandler.HandleItemCollision(collision);
            return;
        }
    }
    public void OnHit(int damage)
    {
        Vector3 hitPos = transform.position;

        // 無敵中や既にスタン中の場合は判定を無視
        if (playerMove.IsInvincible || currentState != PlayerState.Normal) return;

        bool isDown = false;

        if (myStatusManager != null)
        {
            // ★★★ 【ML-Agents 連携】被弾時のペナルティ通知をここに割り込ませる ★★★
            DanmakuAgent agent = GetComponentInParent<DanmakuAgent>();
            if (agent != null)
            {
                // 被弾したことをAIの脳（Agent）に直接伝え、マイナス報酬を付与
                agent.GiveHitPenalty();
            }

            // ダメージを適用
            isDown = myStatusManager.ApplyDamage(damage);
        }

        // 共通の被弾演出（爆発エフェクトとSE）
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);
        SEManager.Instance.Play(SEPath.SE_PLAYER_COLLISION, 0.3f);

        // =========================================================================
        // 🌟【スペルカードシステム拡張】：スーパーアーマー（被弾ノンスタン）の完全開通
        // =========================================================================
        if (myStatusManager != null && myStatusManager.isSpellCardActive)
        {
            // スペル発動中は、ダメージ計算と被弾VFX/SFXは通しつつ、
            // スタンコルーチンへの突入を完全に拒否（アーマー維持）してこの場で即座に処理を抜けます！
            return;
        }

        // 以下のスタン遷移は、スペルカード未発動の通常時のみ実行されます
        if (isDown)
        {
            currentState = PlayerState.Hit;
            StartCoroutine(ExplosionAndStunRoutine());
        }
        else
        {
            StartCoroutine(DamageStunRoutine());
        }
    }
    // ★新しく追加：通常の被弾（HP減少）による短いスタン演出
    IEnumerator DamageStunRoutine()
    {
        currentState = PlayerState.Hit;

        // 移動スクリプトを一時的に無効化して動けなくする
        if (playerMove != null) playerMove.enabled = false;

        // 被弾による無敵時間の付与（1.0秒など）
        playerMove.SetInvincible(1.0f);

        // 操作不能にする時間（例：0.4秒。stunTime(2秒)より短くするのが一般的です）
        // もし撃墜時と同じ長さが必要なら WaitForSeconds(stunTime) に書き換えてください
        yield return new WaitForSeconds(0.4f);

        // 状態をNormalに戻し、移動を許可する
        if (playerMove != null) playerMove.enabled = true;
        currentState = PlayerState.Normal;
    }
    // 軽い被弾用の演出（スタンはしないが、連続ヒットを防ぐための短い無敵）
    IEnumerator SmallHitRoutine()
    {
        playerMove.SetInvincible(0.5f); // 0.5秒だけ無敵に
        yield return null;
    }
    IEnumerator CheckDeathBombRoutine()
    {
        SEManager.Instance.Play(SEPath.SE_PLAYER_COLLISION, 0.3f);
        playerMove.StartDeathBombWindow(deathBombWindow);

        while (playerMove.IsInDeathBombWindow)
        {
            yield return null;
        }

        if (playerMove.IsInvincible)
        {
            currentState = PlayerState.Normal;
            yield break;
        }

        StartCoroutine(ExplosionAndStunRoutine());
    }
    // --- PlayerHitHandler.cs 修正版 ---

    // --- PlayerHitHandler.cs 修正完全版 ---

    IEnumerator ExplosionAndStunRoutine()
    {
        Vector3 hitPos = transform.position;

        // ★ 修正：無敵状態中の誤作動防止用ガード句のみに変更
        // currentState のチェックを外すことで、OnHitから遷移してきた正常な被弾シーケンスを100%通します
        if (playerMove.IsInvincible) yield break;

        // 1. 【演出開始】タイマー停止 ＆ 全体のショットを禁止
        if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.StopTimer();
        Time.timeScale = 0.3f;

        PlayerMove.CanInput = true;
        PlayerMove.CanShoot = false;

        if (myStatusManager != null)
        {
            myStatusManager.currentHP = 0;
            myStatusManager.SendMessage("UpdateUI", SendMessageOptions.DontRequireReceiver);
        }

        bool canContinueMatch = myStatusManager != null && myStatusManager.SubtractLifeAndCheckRebirth();
        ClearAllBullets();

        if (!canContinueMatch)
        {
            SEManager.Instance.Play(SEPath.BOSS_END_END, 0.3f);
        }

        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);

        // 🌟 全員のスキルリキャストと残コストを即座に全快（前回の追加ロジック）
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            SkillManager sm = p.GetComponentInChildren<SkillManager>();
            if (sm != null)
            {
                sm.InstantFullRecovery();
            }
        }

        yield return null;

        if (canContinueMatch)
        {
            // 被弾した自分側を一時操作不能にする
            currentState = PlayerState.Hit;
            if (playerMove != null) playerMove.enabled = false;

            bool isHumanPlayer = (myStatusManager != null && myStatusManager.playerId == 1);

            if (GameModeManager.IsStoryMode && isHumanPlayer)
            {
                yield return new WaitForSecondsRealtime(2.0f);
                Time.timeScale = 1.0f;
                yield return new WaitForSeconds(1.0f);

                if (myStatusManager != null) yield return StartCoroutine(myStatusManager.GradualHealthRecovery(1.0f));

                currentState = PlayerState.Normal;
                if (playerMove != null) playerMove.enabled = true;

                // 復活時の無敵を付与
                playerMove.SetInvincible(invincibilityTime);

                // 🌟【修正】ショットを禁止し、カウントダウンをキックする
                PlayerMove.CanShoot = false;
                if (GameStartCountdown.Instance != null)
                {
                    GameStartCountdown.Instance.StartCountdown();

                    // カウントダウン中はインプットやタイマーが制御されるため、
                    // カウントダウンが終了する（CanShootがTrueになる等）まで待機するか、
                    // 内部でタイマーが再開されるのを任せます。
                    // ここではカウントダウン開始をフックする形にします。
                }
                else
                {
                    // カウントダウンのインフラがない場合のフェールセーフ
                    PlayerMove.CanShoot = true;
                    if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.ResumeTimer();
                }
            }
            else
            {
                // --- CPU戦 / VSモード：仕切り直しシーケンス ---
                yield return new WaitForSecondsRealtime(2.0f);
                Time.timeScale = 1.0f;

                yield return new WaitForSecondsRealtime(1.0f);
                // 1P・2P並列での滑らかなHPバー全回復を呼び出し、仕切り直す
                yield return StartCoroutine(RoundResetSequence());
            }
        }
        else
        {
            SetPlayerActiveState(false);
            yield return StartCoroutine(PerformKORoundEndRoutine());
        }
    }

    // ★ 追加：引き分け時の演出とリセット
    public IEnumerator TriggerDrawSequence()
    {
        // 1. "DRAW" と表示する
        if (myStatusManager != null && myStatusManager.countdownText != null)
        {
            myStatusManager.countdownText.text = "DRAW";
            myStatusManager.countdownText.color = Color.white;
            myStatusManager.countdownText.gameObject.SetActive(true);
        }

        // 2秒ほど「DRAW」を見せる
        yield return new WaitForSeconds(2.0f);

        if (myStatusManager != null && myStatusManager.countdownText != null)
        {
            myStatusManager.countdownText.gameObject.SetActive(false);
        }

        // 2. ライフを減らさずにリセットシーケンスを呼ぶ
        // RoundResetSequenceは内部でHP回復、タイマーリセット、カウントダウン開始を行う
        yield return StartCoroutine(RoundResetSequence());
    }
    // --- PlayerHitHandler.cs 修正版 ---

    IEnumerator RoundResetSequence()
    {
        // タイムスケールを戻す（スロー解除）
        Time.timeScale = 1.0f;

        // 1. 各プレイヤーの状態と表示の復帰
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;

            PlayerHitHandler hh = p.GetComponentInChildren<PlayerHitHandler>();
            if (hh != null)
            {
                hh.SetPlayerActiveState(true);
                hh.currentState = PlayerState.Normal;
            }
        }

        // =========================================================================
        // 🌟 2. 【核心の修正】ストーリー・VSモード共通で「滑らかなHP全快演出」に統一！
        // =========================================================================
        int playerCount = 0;
        foreach (var p in PlayerMove.AllPlayers) if (p != null) playerCount++;

        Coroutine[] recoveryCoroutines = new Coroutine[playerCount];
        int idx = 0;

        // 1P・2P（または自機とボス）の双方に対し、1.0秒かけた滑らかなリチャージコルーチンを並列で走らせる
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>();
            if (ps != null)
            {
                // 🌟 ストーリーでもVSでも、一律で「GradualHealthRecovery」を回して瞬時上書きを完全阻止！
                recoveryCoroutines[idx] = StartCoroutine(ps.GradualHealthRecovery(1.0f));
                idx++;
            }
        }

        // 起動したすべての回復演出が完全に終了するまで、このフレームで同期待ち（ファンイン同期）
        for (int i = 0; i < idx; i++)
        {
            if (recoveryCoroutines[i] != null) yield return recoveryCoroutines[i];
        }

        // 3. タイマーのリセット
        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.ResetRoundTimer(99f);
        }

        // 4. カウントダウン演出を呼び出す
        if (GameStartCountdown.Instance != null)
        {
            GameStartCountdown.Instance.StartCountdown();
        }
        else
        {
            PlayerMove.CanInput = true;
        }

        yield return null;
    }
    // ★修正：指定された順序で演出を実行するコルーチン
    IEnumerator PerformKORoundEndRoutine()
    {
        // 1. 【被弾した瞬間】スローモーション開始 ＆ K.O. 表示
        Time.timeScale = 0.2f;

        if (myStatusManager != null)
        {
            // K.O. 出現アニメーション（スケールアップなど）
            yield return myStatusManager.StartCoroutine(myStatusManager.PlayKOAnimation());
        }

        // K.O. が表示された状態で少し待機（実時間で指定）
        yield return new WaitForSecondsRealtime(0.8f);

        // 2. 【スロー解除】 ＆ K.O. テキストを滑らかに消す
        Time.timeScale = 1.0f;

        if (myStatusManager != null && myStatusManager.koText != null)
        {
            // ★修正：滑らかに消去する（0.5秒かけてフェードアウト）
            yield return myStatusManager.StartCoroutine(myStatusManager.FadeOutKOAnimation(0.5f));
        }

        yield return new WaitForSeconds(1.0f);
        // 3. 【勝利メッセージ】「○○ Wins!」を表示
        ShowWinMessage();

        // 決着後の余韻
        yield return new WaitForSeconds(4.0f);

        // 4. ポーズメニュー（ゲームオーバー画面）を表示
        if (myStatusManager != null) myStatusManager.TriggerGameOver();
    }
    /// <summary>
    /// プレイヤー階層全体の表示・当たり判定・操作を一括で切り替える
    /// </summary>
    private void SetPlayerActiveState(bool active)
    {
        // 1. 親（Playerルート）以下の全てのRenderer（SpriteRenderer等）を切り替える
        // これにより子オブジェクトの矢印なども一括で消えます
        Renderer[] renderers = transform.parent.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = active;

        // 2. 当たり判定も一括で切り替える
        // 消えている間にアイテムを拾ったりグレイズしたりするのを防ぎます
        Collider2D[] colliders = transform.parent.GetComponentsInChildren<Collider2D>();
        foreach (var c in colliders) c.enabled = active;

        // 3. 移動スクリプトの有効化状態
        playerMove.enabled = active;
    }

    /// <summary>
    /// 画面内のすべての弾を探して消去するヘルパーメソッド
    /// </summary>
    private void ClearAllBullets()
    {
        // 1. プレイヤーの弾をエフェクト付きで消去
        DanmakuBullet[] playerBullets = Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var b in playerBullets)
        {
            b.Deactivate(true); // true を渡してエフェクトを発生させる
        }

        // 2. 敵の弾をエフェクト付きで消去
        EnemyBullet[] enemyBullets = Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var b in enemyBullets)
        {
            b.Deactivate(true); // true を渡してエフェクトを発生させる
        }
    }
    public void StartRebirthFromContinue()
    {
        StartCoroutine(RebirthRoutine());
    }

    private IEnumerator RebirthRoutine()
    {
        currentState = PlayerState.Rebirth;
        PlayerMove.CanShoot = false; // 🌟 復活演出中はショット禁止

        // 登場座標の計算
        float spawnX = (myStatusManager != null && myStatusManager.playerId == 2) ? 8.0f : -8.0f;
        float targetX = (myStatusManager != null && myStatusManager.playerId == 2) ? 3.5f : -3.5f;

        transform.parent.position = new Vector3(spawnX, 0, 0);

        // 復活時に全てを表示状態に戻す
        SetPlayerActiveState(true);

        float elapsed = 0;
        Vector3 startPos = transform.parent.position;
        Vector3 targetPos = new Vector3(targetX, 0, 0);

        // 定位置までのスライド移動（実時間または通常時間）
        while (elapsed < 0.6f)
        {
            transform.parent.position = Vector3.Lerp(startPos, targetPos, elapsed / 0.6f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        currentState = PlayerState.Normal;
        playerMove.SetInvincible(invincibilityTime);

        // 🌟【修正】定位置に付いたら、タイマーを一度リセット/停止させてカウントダウンを挟む
        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.StopTimer();
        }

        if (GameStartCountdown.Instance != null)
        {
            // カウントダウン演出を開始（演出終了時に内部でCanShoot=true、タイマーResumeが呼ばれる想定）
            GameStartCountdown.Instance.StartCountdown();
        }
        else
        {
            // カウントダウンがない場合のフェールセーフ
            PlayerMove.CanShoot = true;
            if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.ResumeTimer();
        }
    }
    /// <summary>
    /// 勝者（自分を倒した相手）の名前を取得して表示する
    /// </summary>
    // --- PlayerHitHandler.cs 修正版 ---

    /// <summary>
    /// 勝者（自分を倒した相手）の名前を取得して表示する
    /// </summary>
    private void ShowWinMessage()
    {
        // 1. まずは設定済みの対戦相手（Opponent）を勝者候補にする
        PlayerMove winner = playerMove.Opponent;

        // ★修正：もしOpponentが未設定なら、全プレイヤーリストから自分以外の相手を検索する
        // これは、ストーリーモードのCPUなどで参照設定を忘れた際の「フェールセーフ」になります
        if (winner == null)
        {
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p != null && p != playerMove)
                {
                    winner = p;
                    break;
                }
            }
        }

        // 2. 勝者が見つかり、かつUIの参照がある場合のみテキストを更新する
        if (winner != null && myStatusManager.winText != null)
        {
            PlayerStatusManager winnerStatus = winner.GetComponent<PlayerStatusManager>();

            // 勝者の名前を取得（未設定なら「Player」とする）
            string winnerName = (winnerStatus != null && winnerStatus.characterData != null)
                ? winnerStatus.characterData.characterName
                : "Player";

            // メッセージを「名前 + Wins!」の形に更新
            myStatusManager.winText.text = winnerName + " Wins!";
            myStatusManager.winText.gameObject.SetActive(true);

            // 勝者のイメージカラーを文字色に反映
            if (winnerStatus != null && winnerStatus.characterData != null)
            {
                myStatusManager.winText.color = winnerStatus.characterData.imageColor;
            }
        }
        else
        {
            // もし万が一、勝者が特定できなかった場合のデバッグ用
            Debug.LogWarning("Winner could not be identified.");
        }
    }
}