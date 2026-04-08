using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーの被弾、食らいボム、復活処理を管理するクラス
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

    // ★ 1vs1対戦対応：自分の残機を管理するマネージャーを個別に指定
    // インスペクターで、P1にはP1用、P2にはP2用のマネージャーをセットしてください
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
        // 1. アイテム取得
        if (collision.CompareTag("Item"))
        {
            if (itemHandler != null) itemHandler.HandleItemCollision(collision);
            return;
        }

        // 2. 被弾判定（敵弾、敵本体、レーザー、または対戦相手）
        if (collision.CompareTag("EnemyBullet") || collision.CompareTag("Enemy") ||
            collision.CompareTag("Laser") || collision.CompareTag("Player"))
        {
            // ★【自爆防止】弾のオーナーが「自分」ならヒットを無視する
            DanmakuBullet bullet = collision.GetComponent<DanmakuBullet>();
            if (bullet != null)
            {
                // transform.root は Player オブジェクトの最上位
                if (bullet.owner == transform.root.gameObject)
                {
                    return;
                }
            }

            // 無敵中や既に被弾状態なら無視
            if (playerMove.IsInvincible || currentState != PlayerState.Normal) return;

            // 被弾確定
            currentState = PlayerState.DeathBomb;
            StartCoroutine(CheckDeathBombRoutine());

            // 弾を消す（任意）
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

        // 食らいボム成功時は死亡を回避
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

        // 演出：爆発と弾消し
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, deathPos, Quaternion.identity);
        if (bulletClearPrefab != null)
        {
            GameObject clearObj = Instantiate(bulletClearPrefab);
            clearObj.SendMessage("StartClearing", deathPos, SendMessageOptions.DontRequireReceiver);
        }

        // キャラを一旦隠す
        playerMove.enabled = false;
        transform.parent.position = new Vector3(-2.0f, -100f, 0); // 画面外
        if (characterRenderer != null) characterRenderer.enabled = false;

        // ★【1vs1対応】Instance ではなく紐付けられたマネージャーから残機を減らす
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
        transform.parent.position = new Vector3(-2.0f, -6.0f, 0); // 登場位置
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
}