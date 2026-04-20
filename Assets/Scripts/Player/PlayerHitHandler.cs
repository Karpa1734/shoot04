using KanKikuchi.AudioManager;
using System.Collections;
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
    public float invincibilityTime = 3.0f;
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
        // ★重要：無敵中やスタン（Hit/Down状態）中は即座にリターンして判定を無視する
        if (playerMove.IsInvincible || currentState != PlayerState.Normal) return;

        bool isDown = false;
        if (myStatusManager != null)
        {
            // ダメージを適用し、HPが0になった（ダウンした）か確認
            isDown = myStatusManager.ApplyDamage(damage);
        }

        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);
        SEManager.Instance.Play(SEPath.SE_PLAYER_COLLISION, 0.3f);
        if (isDown)
        {
            // HP 0：撃墜演出へ
            currentState = PlayerState.Hit;
            StartCoroutine(ExplosionAndStunRoutine());
        }
        else
        {
            // 被弾：短い無敵時間を与えて連続ヒットを防止
            playerMove.SetInvincible(1.5f);
        }
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
    IEnumerator ExplosionAndStunRoutine()
    {
        Vector3 hitPos = transform.position;

        // 画面内の全弾消去
        ClearAllBullets();

        // 爆発エフェクト
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);

        // キャラクターを非表示にする
        SetPlayerActiveState(false);

        // ★重要：決着かどうかを「即座に」判定する（これで被弾した瞬間の演出にする）
        bool canContinueMatch = false;
        if (myStatusManager != null)
        {
            // ここでストックを減らし、残機があるか確認
            canContinueMatch = myStatusManager.SubtractLifeAndCheckRebirth();
        }

        if (canContinueMatch)
        {
            // まだ戦える場合：従来の2秒スタンを待ってから復活
            yield return new WaitForSeconds(stunTime);
            yield return StartCoroutine(RebirthRoutine());
        }
        else
        {
            // ★決着の場合：待機せず即座に K.O. 演出ルーチンを開始
            yield return StartCoroutine(PerformKORoundEndRoutine());
        }
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
    private void ShowWinMessage()
    {
        // PlayerMove.cs に実装した Opponent プロパティを使用して相手を取得
        PlayerMove winner = playerMove.Opponent;

        if (winner != null && myStatusManager.winText != null)
        {
            // 勝者のステータスマネージャーからキャラ名を取得
            PlayerStatusManager winnerStatus = winner.GetComponent<PlayerStatusManager>();
            string winnerName = (winnerStatus != null && winnerStatus.characterData != null)
                ? winnerStatus.characterData.characterName
                : "Opponent";

            // メッセージを設定して表示
            myStatusManager.winText.text = winnerName + " Wins!";
            myStatusManager.winText.gameObject.SetActive(true);

            // 勝者のイメージカラーを文字色に反映させるとより良いです
            if (winnerStatus != null && winnerStatus.characterData != null)
            {
                myStatusManager.winText.color = winnerStatus.characterData.imageColor;
            }
        }
    }
}