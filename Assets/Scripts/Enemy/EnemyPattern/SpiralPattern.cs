using KanKikuchi.AudioManager;
using UnityEngine;

public class SpiralPattern : MonoBehaviour
{
    [Header("Bullet Settings")]
    public BulletData bulletData;   // GameObject‚Ì‘ã‚í‚è‚ÉBulletData‚ðŽQÆ
    public float bulletSpeed = 3f;
    public int wayCount = 4;
    public float rotationSpeed = 10f;
    public float fireInterval = 0.1f;

    private float angle = 0f;
    private float timer = 0f;

    void Update()
    {
        if (bulletData == null) return;

        timer += Time.deltaTime;
        angle += rotationSpeed * Time.deltaTime * 60f;

        if (timer >= fireInterval)
        {
            FireSpiral();
            timer = 0f;
        }
    }

    void FireSpiral()
    {
        SEManager.Instance.Play(SEPath.SHOT1, 0.5f);
        for (int i = 0; i < wayCount; i++)
        {
            float currentAngle = angle + (i * 360f / wayCount);

            // --- C³Fˆø”‚ð3‚Â“n‚· (prefab, position, rotation) ---
            GameObject bullet = BulletPool.Instance.Get(
                bulletData.bulletPrefab,
                transform.position,
                Quaternion.identity
            );

            EnemyBullet script = bullet.GetComponent<EnemyBullet>();
            if (script != null)
            {
                script.Initialize(bulletSpeed, currentAngle, 0, bulletSpeed, 0, 0, bulletData);
            }
        }
    }
}