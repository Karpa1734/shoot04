using UnityEngine;

[RequireComponent(typeof(EnemyBullet))]
public class BulletReflector : MonoBehaviour
{
    private int remainingReflects = 0;
    private EnemyBullet bullet;

    // 判定境界（ステージのサイズに合わせて調整してください）
    private float minX = -4.5f, maxX = 0.5f; // 戦場（左側）の範囲
    private float minY = -4.5f, maxY = 4.5f;

    void Awake()
    {
        bullet = GetComponent<EnemyBullet>();
    }

    public void SetReflectCount(int count)
    {
        remainingReflects = count;
    }

    void Update()
    {
        if (remainingReflects <= 0) return;

        Vector3 pos = transform.position;
        float currentAngle = bullet.GetAngle();
        bool reflected = false;

        // 左右の壁での反射
        if ((pos.x < minX && Mathf.Cos(currentAngle * Mathf.Deg2Rad) < 0) ||
            (pos.x > maxX && Mathf.Cos(currentAngle * Mathf.Deg2Rad) > 0))
        {
            bullet.UpdateAngle(180f - currentAngle); // 左右反転
            reflected = true;
        }

        // 上下の壁での反射
        if ((pos.y < minY && Mathf.Sin(currentAngle * Mathf.Deg2Rad) < 0) ||
            (pos.y > maxY && Mathf.Sin(currentAngle * Mathf.Deg2Rad) > 0))
        {
            bullet.UpdateAngle(-currentAngle); // 上下反転
            reflected = true;
        }

        if (reflected)
        {
            remainingReflects--;
            // 反射時のSEなどをここで鳴らすことも可能
        }
    }
}