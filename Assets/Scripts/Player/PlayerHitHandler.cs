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

        if (collision.CompareTag("EnemyBullet") || collision.CompareTag("Enemy") ||
            collision.CompareTag("Laser") || collision.CompareTag("Player"))
        {
            DanmakuBullet bullet = collision.GetComponent<DanmakuBullet>();
            if (bullet != null)
            {
                if (bullet.owner == transform.root.gameObject) return;
            }

            if (playerMove.IsInvincible || currentState != PlayerState.Normal) return;

            currentState = PlayerState.DeathBomb;
            StartCoroutine(CheckDeathBombRoutine());

            if (bullet != null) bullet.Deactivate();
        }
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

        StartCoroutine(ExplosionAndRebirthRoutine());
    }

    IEnumerator ExplosionAndRebirthRoutine()
    {
        Vector3 deathPos = transform.position;
        currentState = PlayerState.Hit;

        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, deathPos, Quaternion.identity);
        if (bulletClearPrefab != null)
        {
            GameObject clearObj = Instantiate(bulletClearPrefab);
            clearObj.SendMessage("StartClearing", deathPos, SendMessageOptions.DontRequireReceiver);
        }

        // キャラを一旦隠す（画面外 Y-100へ）
        playerMove.enabled = false;
        transform.parent.position = new Vector3(0, -100f, 0);
        if (characterRenderer != null) characterRenderer.enabled = false;

        bool canRebirth = false;
        if (myStatusManager != null)
        {
            canRebirth = myStatusManager.SubtractLifeAndCheckRebirth();
        }

        if (canRebirth)
        {
            yield return new WaitForSeconds(downTime);
            yield return StartCoroutine(RebirthRoutine());
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
            // エラー解消：myStatusManagerから直接呼ぶ
            if (myStatusManager != null) myStatusManager.TriggerGameOver();
        }
    }

    public void StartRebirthFromContinue()
    {
        StartCoroutine(RebirthRoutine());
    }

    private IEnumerator RebirthRoutine()
    {
        currentState = PlayerState.Rebirth;

        // --- 横STG・1vs1用の登場座標計算 ---
        float spawnX = -8.0f; // デフォルト：画面左外
        float targetX = -3.5f; // デフォルト：画面左側の待機位置
        float spawnY = 0f;     // Y座標は0固定

        // IDが2なら画面右から登場させる
        if (myStatusManager != null && myStatusManager.playerId == 2)
        {
            spawnX = 8.0f;  // 画面右外
            targetX = 3.5f; // 画面右側の待機位置
        }

        transform.parent.position = new Vector3(spawnX, spawnY, 0);
        if (characterRenderer != null) characterRenderer.enabled = true;

        float elapsed = 0;
        Vector3 startPos = transform.parent.position;
        Vector3 targetPos = new Vector3(targetX, spawnY, 0);

        // 0.6秒かけて滑らかに画面内にスライド移動
        while (elapsed < 0.6f)
        {
            transform.parent.position = Vector3.Lerp(startPos, targetPos, elapsed / 0.6f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerMove.enabled = true;
        currentState = PlayerState.Normal;
        playerMove.SetInvincible(invincibilityTime);
    }
}