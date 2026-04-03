using KanKikuchi.AudioManager;
using System.Collections;
using TMPro;
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

    // --- 追加：本体のSpriteRendererを特定するため ---
    private SpriteRenderer characterRenderer;
    private ItemEffectHandler itemHandler;
    void Awake()
    {
        // 親オブジェクトや他の子オブジェクトから必要なコンポーネントを自動取得
        if (playerMove == null) playerMove = GetComponentInParent<PlayerMove>();
        if (playerAnim == null) playerAnim = GetComponentInParent<PlayerAnimation>();
        itemHandler = GetComponent<ItemEffectHandler>();
        // 通常、キャラの画像は親か別の子にあるので、それを見つける
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
        // 1. 敵弾の消去（レーザーは消さない）
        if (collision.CompareTag("EnemyBullet"))
        {
            EnemyBullet bullet = collision.GetComponent<EnemyBullet>();
            if (bullet != null) bullet.Deactivate(true);
        }

        if (collision.CompareTag("Item"))
        {
            itemHandler.HandleItemCollision(collision);
            return;
        }

        if (playerMove.IsInvincible || currentState != PlayerState.Normal) return;

        // 3. 被弾開始（Laserタグを追加。ただし破壊はしない）
        if (collision.CompareTag("EnemyBullet") || collision.CompareTag("Enemy") || collision.CompareTag("Laser"))
        {
            EnemyStatus boss = Object.FindFirstObjectByType<EnemyStatus>();
            if (boss != null) boss.FailSpell();

            currentState = PlayerState.DeathBomb;
            StartCoroutine(CheckDeathBombRoutine());
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

    // --- PlayerHitHandler.cs の修正 ---

    IEnumerator ExplosionAndRebirthRoutine()
    {
        if (playerMove.IsInvincible)
        {
            currentState = PlayerState.Normal;
            yield break;
        }

        Vector3 deathPos = transform.position;
        currentState = PlayerState.Hit;

        // 1. エフェクトと弾消し（共通）
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, deathPos, Quaternion.identity);
        if (bulletClearPrefab != null)
        {
            GameObject clearObj = Instantiate(bulletClearPrefab);
            clearObj.SendMessage("StartClearing", deathPos, SendMessageOptions.DontRequireReceiver);
        }

        // --- 修正ポイント：残機に関わらず、一旦プレイヤーを画面外へ飛ばして非表示にする ---
        // これによりゲームオーバー時も「その場で止まる」のではなく「ミスして消える」演出になります
        playerMove.enabled = false;
        transform.parent.position = new Vector3(-2.0f, -100f, 0); // 画面外へ
        if (characterRenderer != null) characterRenderer.enabled = false; // 非表示

        // --- 残機チェック ---
        if (PlayerStatusManager.Instance.SubtractLifeAndCheckRebirth())
        {
            // 復活可能な場合：既存のダウンタイム待機
            yield return new WaitForSeconds(downTime);

            // 復活処理（Rebirth）
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
            // --- 修正ポイント：最後の残機だった場合 ---
            // 爆発を見てから1秒間待機する
            yield return new WaitForSeconds(0.5f);

            // ゲームオーバーUIの表示指示
            PlayerStatusManager.Instance.TriggerGameOver();
            yield break;
        }
    }
    // コンティニューボタンから呼ばれる復活開始メソッド
    public void StartRebirthFromContinue()
    {
        StartCoroutine(RebirthRoutine());
    }

    private IEnumerator RebirthRoutine()
    {
        currentState = PlayerState.Rebirth;

        // 画面下部から登場
        transform.parent.position = new Vector3(-2.0f, -6.0f, 0);
        if (characterRenderer != null) characterRenderer.enabled = true;

        float elapsed = 0;
        Vector3 startPos = transform.parent.position;
        Vector3 targetPos = new Vector3(-2.0f, -3.5f, 0); // 目標位置

        while (elapsed < 0.6f)
        {
            transform.parent.position = Vector3.Lerp(startPos, targetPos, elapsed / 0.6f);
            elapsed += Time.unscaledDeltaTime; // ポーズ解除直後のため unscaled を推奨
            yield return null;
        }

        playerMove.enabled = true;
        currentState = PlayerState.Normal;
        playerMove.SetInvincible(invincibilityTime); // 復活後の無敵付与
    }
}