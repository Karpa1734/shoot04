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

    [Header("--- Hit Cap Settings (VJT Density Counter) ---")]
    [Tooltip("1フレーム内に同時にヒットしていい弾の最大数。全方位弾などの一瞬の全壊を防ぎます")]
    public int maxHitsPerFrame = 2;
    private int currentHitsInThisFrame = 0;
    private int lastProcessedFrame = -1;

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

    void LateUpdate()
    {
        if (Time.frameCount != lastProcessedFrame)
        {
            currentHitsInThisFrame = 0;
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

        // 🌟【バグ修正句】：全体のガード句を撤廃し、
        // 🌟 バリアが全壊・解除されたまさにその瞬間（同じUnityフレーム内）に重なっていた弾のみをピンポイントで消音処理
        if (myStatusManager != null && myStatusManager.isOverheated && Time.frameCount == lastProcessedFrame)
        {
            // 解除されたフレームの残弾は、スタンの二重発生や余計なノイズを防ぐため、ダメージ計算をせずにここで弾き飛ばす
            return;
        }

        // フレーム内多段ヒット上限のインターセプト判定
        if (Time.frameCount == lastProcessedFrame)
        {
            currentHitsInThisFrame++;

            if (currentHitsInThisFrame > maxHitsPerFrame)
            {
                return;
            }
        }
        else
        {
            lastProcessedFrame = Time.frameCount;
            currentHitsInThisFrame = 1;
        }

        bool isDown = false;

        if (myStatusManager != null)
        {
            DanmakuAgent agent = GetComponentInParent<DanmakuAgent>();
            if (agent != null)
            {
                agent.GiveHitPenalty();
            }

            // ダメージを適用
            isDown = myStatusManager.ApplyDamage(damage);
        }

        // 🌟 聖少女領域（VJT）発動中の演出＆SE専用排他スイッチ（常時アーマー維持）
        if (myStatusManager != null && myStatusManager.isSpellCardActive)
        {
            if (SEManager.Instance != null)
            {
                SEManager.Instance.Play(SEPath.SE_DAMAGE00, 0.2f);
            }
            return;
        }

        // 🔷 プレーンな通常時（およびクールダウンデバフ中）：通常の被弾演出をしっかりと実行！
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);
        if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SE_PLAYER_COLLISION, 0.3f);

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

    IEnumerator DamageStunRoutine()
    {
        currentState = PlayerState.Hit;

        if (playerMove != null) playerMove.enabled = false;
        playerMove.SetInvincible(1.0f);

        yield return new WaitForSeconds(0.4f);

        if (playerMove != null) playerMove.enabled = true;
        currentState = PlayerState.Normal;
    }

    IEnumerator SmallHitRoutine()
    {
        playerMove.SetInvincible(0.5f);
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

        if (playerMove.IsInvincible) yield break;

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
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.BOSS_END_END, 0.3f);
        }

        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, hitPos, Quaternion.identity);

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

                playerMove.SetInvincible(invincibilityTime);

                PlayerMove.CanShoot = false;
                if (GameStartCountdown.Instance != null)
                {
                    GameStartCountdown.Instance.StartCountdown();
                }
                else
                {
                    PlayerMove.CanShoot = true;
                    if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.ResumeTimer();
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(2.0f);
                Time.timeScale = 1.0f;

                yield return new WaitForSecondsRealtime(1.0f);
                yield return StartCoroutine(RoundResetSequence());
            }
        }
        else
        {
            SetPlayerActiveState(false);
            yield return StartCoroutine(PerformKORoundEndRoutine());
        }
    }

    public IEnumerator TriggerDrawSequence()
    {
        if (myStatusManager != null && myStatusManager.countdownText != null)
        {
            myStatusManager.countdownText.text = "DRAW";
            myStatusManager.countdownText.color = Color.white;
            myStatusManager.countdownText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(2.0f);

        if (myStatusManager != null && myStatusManager.countdownText != null)
        {
            myStatusManager.countdownText.gameObject.SetActive(false);
        }

        yield return StartCoroutine(RoundResetSequence());
    }

    IEnumerator RoundResetSequence()
    {
        Time.timeScale = 1.0f;

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

        int playerCount = 0;
        foreach (var p in PlayerMove.AllPlayers) if (p != null) playerCount++;

        Coroutine[] recoveryCoroutines = new Coroutine[playerCount];
        int idx = 0;

        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p == null) continue;
            PlayerStatusManager ps = p.GetComponent<PlayerStatusManager>();
            if (ps != null)
            {
                recoveryCoroutines[idx] = StartCoroutine(ps.GradualHealthRecovery(1.0f));
                idx++;
            }
        }

        for (int i = 0; i < idx; i++)
        {
            if (recoveryCoroutines[i] != null) yield return recoveryCoroutines[i];
        }

        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.ResetRoundTimer(99f);
        }

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

    IEnumerator PerformKORoundEndRoutine()
    {
        Time.timeScale = 0.2f;

        if (myStatusManager != null)
        {
            yield return myStatusManager.StartCoroutine(myStatusManager.PlayKOAnimation());
        }

        yield return new WaitForSecondsRealtime(0.8f);

        Time.timeScale = 1.0f;

        if (myStatusManager != null && myStatusManager.koText != null)
        {
            yield return myStatusManager.StartCoroutine(myStatusManager.FadeOutKOAnimation(0.5f));
        }

        yield return new WaitForSeconds(1.0f);

        ShowWinMessage();

        yield return new WaitForSecondsRealtime(4.0f);

        if (myStatusManager != null) myStatusManager.TriggerGameOver();
    }

    private void SetPlayerActiveState(bool active)
    {
        Renderer[] renderers = transform.parent.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = active;

        Collider2D[] colliders = transform.parent.GetComponentsInChildren<Collider2D>();
        foreach (var c in colliders) c.enabled = active;

        playerMove.enabled = active;
    }

    private void ClearAllBullets()
    {
        DanmakuBullet[] playerBullets = Object.FindObjectsByType<DanmakuBullet>(FindObjectsSortMode.None);
        foreach (var b in playerBullets)
        {
            b.Deactivate(true);
        }

        EnemyBullet[] enemyBullets = Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None);
        foreach (var b in enemyBullets)
        {
            b.Deactivate(true);
        }
    }

    public void StartRebirthFromContinue()
    {
        StartCoroutine(RebirthRoutine());
    }

    private IEnumerator RebirthRoutine()
    {
        currentState = PlayerState.Rebirth;
        PlayerMove.CanShoot = false;

        float spawnX = (myStatusManager != null && myStatusManager.playerId == 2) ? 8.0f : -8.0f;
        float targetX = (myStatusManager != null && myStatusManager.playerId == 2) ? 3.5f : -3.5f;

        transform.parent.position = new Vector3(spawnX, 0, 0);

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

        if (MatchTimerUI.Instance != null)
        {
            MatchTimerUI.Instance.StopTimer();
        }

        if (GameStartCountdown.Instance != null)
        {
            GameStartCountdown.Instance.StartCountdown();
        }
        else
        {
            PlayerMove.CanShoot = true;
            if (MatchTimerUI.Instance != null) MatchTimerUI.Instance.ResumeTimer();
        }
    }

    private void ShowWinMessage()
    {
        PlayerMove winner = playerMove.Opponent;

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

        if (winner != null && myStatusManager.winText != null)
        {
            PlayerStatusManager winnerStatus = winner.GetComponent<PlayerStatusManager>();

            string winnerName = (winnerStatus != null && winnerStatus.characterData != null)
                ? winnerStatus.characterData.characterName
                : "Player";

            myStatusManager.winText.text = winnerName + " Wins!";
            myStatusManager.winText.gameObject.SetActive(true);

            if (winnerStatus != null && winnerStatus.characterData != null)
            {
                myStatusManager.winText.color = winnerStatus.characterData.imageColor;
            }
        }
        else
        {
            Debug.LogWarning("Winner could not be identified.");
        }
    }
}