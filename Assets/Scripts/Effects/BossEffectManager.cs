using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

public class BossEffectManager : MonoBehaviour
{
    public static BossEffectManager Instance;

    [Header("Prefabs")]
    public GameObject chargeParticlePrefab;
    public GameObject burstParticlePrefab; 
    public GameObject smallBurstParticlePrefab;

    [Header("Burst Default Settings")]
    public float burstMinSpeed = 3.0f;
    public float burstMaxSpeed = 7.0f;
    public float burstLifespan = 1.0f; // ★ボス用の生存時間
    [Header("Small Burst Settings (Enemy)")]
    // ★ザコ用のデフォルト値
    public float smallBurstMinSpeed = 1.5f;
    public float smallBurstMaxSpeed = 4.0f; 
    public float smallBurstLifespan = 0.8f; // ★ザコ用の生存時間（短めに設定）
    float smallBurstScale = 1.0f; // ★追加：ザコ用エフェクトの初期サイズ
    string deathSEPath = SEPath.ENEMY_VANISH_A; // 爆発音のパス（プロジェクトに合わせて調整）
    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // --- 修正：引数に spawnPosition を追加 ---
    public void PlayChargeEffect(float duration, Color color, Vector3 spawnPosition)
    {
        StartCoroutine(ChargeRoutine(duration, color, spawnPosition));
    }

    IEnumerator ChargeRoutine(float duration, Color color, Vector3 spawnPosition)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            SpawnChargeParticle(color, spawnPosition);
            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.05f);
        }
    }

    void SpawnChargeParticle(Color color, Vector3 targetPos)
    {
        if (chargeParticlePrefab == null) return;
        // 指定された座標に生成
        GameObject p = Instantiate(chargeParticlePrefab, targetPos, Quaternion.identity);
        // ChargeParticle 側も Vector3 で座標を受け取れるように Initialize を修正
        p.GetComponent<ChargeParticle>()?.Initialize(targetPos, color);
    }

    // --- 修正：引数に spawnPosition を追加 ---
    public void PlayBurstEffect(Color color, int count, Vector3 spawnPosition)
    {
        if (burstParticlePrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(burstMinSpeed, burstMaxSpeed);
            Vector3 velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * speed;

            // 指定された座標に生成
            GameObject p = Instantiate(burstParticlePrefab, spawnPosition, Quaternion.identity);

            BurstParticle logic = p.GetComponent<BurstParticle>();
            if (logic != null)
            {
                logic.velocity = velocity;
                logic.lifespan = burstLifespan; // ★生存時間をセット
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = color;
            }
        }
    }
    public void PlayEnemyDeathEffect(Vector3 spawnPosition, Color color, int count = 8)
    {

        // 2. 効果音の再生
        SEManager.Instance.Play(deathSEPath, 0.6f);
        GameObject prefab = (smallBurstParticlePrefab != null) ? smallBurstParticlePrefab : burstParticlePrefab;
        if (prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(smallBurstMinSpeed, smallBurstMaxSpeed);
            Vector3 velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * speed;

            GameObject p = Instantiate(prefab, spawnPosition, Quaternion.identity);

            // ★追加：生成した瞬間にスケールを小さくする
            p.transform.localScale = Vector3.one * smallBurstScale;

            BurstParticle logic = p.GetComponent<BurstParticle>();
            if (logic != null)
            {
                logic.lifespan = smallBurstLifespan; // ★ザコ用の短い生存時間をセット
                logic.velocity = velocity;
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = color;
            }
        }
    }
}