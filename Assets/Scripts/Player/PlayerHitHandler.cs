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
            // ダメージを適用
            isDown = myStatusManager.ApplyDamage(damage);
        }

        // 共通の被弾演出（爆発エフェクトとSE）
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);
        SEManager.Instance.Play(SEPath.SE_PLAYER_COLLISION, 0.3f);

        if (isDown)
        {
            // 【撃墜】HP 0：ストック確認やリセットを伴う重いスタンへ
            currentState = PlayerState.Hit;
            StartCoroutine(ExplosionAndStunRoutine());
        }
        else
        {
            // ★【追加】ダメージ：撃墜ではないが、操作不能な短いスタンを発生させる
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

    // --- PlayerHitHandler.cs 修正版 ---

    IEnumerator ExplosionAndStunRoutine()
    {
        Vector3 hitPos = transform.position;

        // 1. 【演出開始】タイマー停止 ＆ 全体のショットだけ禁止する
        if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.StopTimer();
        Time.timeScale = 0.3f;

        // ★ 修正：CanInput は true のままにする（勝者は動けるように）
        PlayerMove.CanInput = true;
        PlayerMove.CanShoot = false; // ショットは全員禁止

        // 自分（やられた側）のHPを0にする演出
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

        yield return null;


        if (canContinueMatch)
        {
            // ★ 重要：やられた自分だけを操作不能にする
            currentState = PlayerState.Hit;
            if (playerMove != null) playerMove.enabled = false;

            bool isHumanPlayer = (myStatusManager != null && myStatusManager.playerId == 1);

            if (GameModeManager.IsStoryMode && isHumanPlayer)
            {
                // --- ストーリーモード：シームレス復帰 ---
                yield return new WaitForSecondsRealtime(2.0f);
                Time.timeScale = 1.0f;
                yield return new WaitForSeconds(0.2f);

                if (myStatusManager != null) yield return StartCoroutine(myStatusManager.GradualHealthRecovery(1.0f));

                // 自分の状態を戻し、個別の移動を許可する
                currentState = PlayerState.Normal;
                if (playerMove != null) playerMove.enabled = true; // 自分が動けるようになる

                playerMove.SetInvincible(invincibilityTime);
                yield return new WaitForSeconds(invincibilityTime);

                PlayerMove.CanShoot = true; // 無敵終了でショット解禁
                if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.ResumeTimer();
            }
            else
            {
                // --- CPU戦 / VSモード：カウントダウン仕切り直し ---
                yield return new WaitForSecondsRealtime(2.0f);
                Time.timeScale = 1.0f;
                yield return new WaitForSeconds(1.0f);

                if (myStatusManager != null) yield return StartCoroutine(myStatusManager.GradualHealthRecovery(1.0f));

                yield return StartCoroutine(RoundResetSequence());
            }
        }
        else
        {
            // 決着時
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
        // ★ 修正：暗転（FadeRoutine）を削除

        // タイムスケールを戻す（スロー解除）
        Time.timeScale = 1.0f;

        // 2. 各プレイヤーの状態を復帰（位置リセットは行わない）
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;

            // ★ 修正：p.transform.position の変更（位置リセット）を削除

            // 状態と描画の復帰
            PlayerHitHandler hh = p.GetComponentInChildren<PlayerHitHandler>();
            if (hh != null)
            {
                // 非表示になっていた場合は表示に戻し、操作を有効化する
                hh.SetPlayerActiveState(true);
                hh.currentState = PlayerState.Normal;
            }

            PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>();
            if (ps != null)
            {
                // ★ モードによる最終同期の分岐
                if (GameModeManager.IsStoryMode)
                {
                    // ストーリーモード：
                    // HPが満タン（敗者として回復済み）またはHPが0（敗者）のみ同期。
                    // 中途半端にHPが残っている勝者は SyncBarsImmediately を呼ばず、値を維持する。
                    if (ps.currentHP >= ps.maxHP || ps.currentHP <= 0)
                    {
                        ps.SyncBarsImmediately();
                    }
                }
                else
                {
                    // VSモード：全員全快させて同期
                    ps.SyncBarsImmediately();
                }
            }
        }

        // 3. タイマーのリセット
        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.ResetRoundTimer(99f);
        }

        // ★ 修正：画面を明るく戻すフェードを削除

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

        // 登場座標の計算
        float spawnX = (myStatusManager != null && myStatusManager.playerId == 2) ? 8.0f : -8.0f;
        float targetX = (myStatusManager != null && myStatusManager.playerId == 2) ? 3.5f : -3.5f;

        transform.parent.position = new Vector3(spawnX, 0, 0);

        // ★ 復活時に全てを表示状態に戻す
        SetPlayerActiveState(true);

        float elapsed = 0;
        Vector3 startPos = transform.parent.position;
        Vector3 targetPos = new Vector3(targetX, 0, 0);

        while (elapsed < 0.6f)
        {
            transform.parent.position = Vector3.Lerp(startPos, targetPos, elapsed / 0.6f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        currentState = PlayerState.Normal;
        playerMove.SetInvincible(invincibilityTime);
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