using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

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

    [Header("Bullet Clear")]
    public GameObject bulletClearPrefab;

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
        // 1. アイテムの取得処理
        if (collision.CompareTag("Item"))
        {
            if (itemHandler != null) itemHandler.HandleItemCollision(collision);
            return;
        }

        // 2. 弾丸または敵との接触判定
        if (collision.CompareTag("EnemyBullet") || collision.CompareTag("Enemy") || collision.CompareTag("Laser"))
        {
            // ★ 自爆防止の核となる修正点：
            // 弾に DanmakuBullet スクリプトが付いているか確認し、
            // そのオーナーが自分（transform.root）なら、被弾処理をスキップする
            DanmakuBullet bullet = collision.GetComponent<DanmakuBullet>();
            if (bullet != null)
            {
                if (bullet.owner == transform.root.gameObject)
                {
                    return; // 自分の弾なので無視
                }
            }

            // 無敵中、または既に被弾状態なら無視
            if (playerMove.IsInvincible || currentState != PlayerState.Normal) return;

            // 被弾確定時の処理
            EnemyStatus boss = Object.FindFirstObjectByType<EnemyStatus>();
            if (boss != null) boss.FailSpell();

            currentState = PlayerState.DeathBomb;
            StartCoroutine(CheckDeathBombRoutine());

            // 弾を消去（DanmakuBullet側で消去処理があればそちらに任せても良い）
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

        playerMove.enabled = false;
        transform.parent.position = new Vector3(-2.0f, -100f, 0);
        if (characterRenderer != null) characterRenderer.enabled = false;

        if (PlayerStatusManager.Instance.SubtractLifeAndCheckRebirth())
        {
            yield return new WaitForSeconds(downTime);

            currentState = PlayerState.Rebirth;
            transform.parent.position = new Vector3(-2.0f, -6.0f, 0);
            if (characterRenderer != null) characterRenderer.enabled = true;

            float elapsed = 0;
            Vector3 startPos = transform.parent.position;
            Vector3 targetPos = new Vector3(-2.0f, -3.5f, 0);
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
        else
        {
            yield return new WaitForSeconds(0.5f);
            PlayerStatusManager.Instance.TriggerGameOver();
            yield break;
        }
    }

    public void StartRebirthFromContinue()
    {
        StartCoroutine(RebirthRoutine());
    }

    private IEnumerator RebirthRoutine()
    {
        currentState = PlayerState.Rebirth;
        transform.parent.position = new Vector3(-2.0f, -6.0f, 0);
        if (characterRenderer != null) characterRenderer.enabled = true;

        float elapsed = 0;
        Vector3 startPos = transform.parent.position;
        Vector3 targetPos = new Vector3(-2.0f, -3.5f, 0);

        while (elapsed < 0.6f)
        {
            transform.parent.position = Vector3.Lerp(startPos, targetPos, elapsed / 0.6f);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        playerMove.enabled = true;
        currentState = PlayerState.Normal;
        playerMove.SetInvincible(invincibilityTime);
    }
}