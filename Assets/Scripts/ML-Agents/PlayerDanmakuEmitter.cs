using UnityEngine;

public class PlayerDanmakuEmitter : MonoBehaviour
{
    public void Fire(PlayerSkillData.SkillSettings s)
    {
        if (s.bulletData == null || s.bulletData.bulletPrefab == null) return;

        // 正面（上向き）を90度として計算
        float baseAngle = 90f + s.angleOffset;
        Vector3 pos = transform.position;

        switch (s.patternType)
        {
            case SkillPatternType.Standard:
                CreateShot(s.bulletData, pos, s.speed, baseAngle);
                break;

            case SkillPatternType.nWay:
                float wayAngle = s.count > 1 ? s.wideAngle / (s.count - 1) : 0;
                float startAngle = baseAngle - (s.wideAngle / 2f);
                for (int i = 0; i < s.count; i++)
                    CreateShot(s.bulletData, pos, s.speed, startAngle + (wayAngle * i));
                break;

            case SkillPatternType.Round:
                float step = 360f / Mathf.Max(1, s.count);
                for (int i = 0; i < s.count; i++)
                    CreateShot(s.bulletData, pos, s.speed, baseAngle + (step * i));
                break;

            case SkillPatternType.Polygon:
                ExecutePolygon(s, pos, baseAngle);
                break;

            case SkillPatternType.Line:
                for (int i = 0; i < s.count; i++)
                    CreateShot(s.bulletData, pos, s.speed + (i * 0.4f), baseAngle);
                break;
        }
    }

    private void CreateShot(BulletData data, Vector3 pos, float speed, float angle)
    {
        GameObject obj = Instantiate(data.bulletPrefab, pos, Quaternion.identity);
        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();

        if (bullet != null)
        {
            // 自分自身を owner として渡し、自爆を防ぐ
            bullet.Initialize(gameObject, speed, angle, 0, speed, 0, 0, data);
        }
    }

    private void ExecutePolygon(PlayerSkillData.SkillSettings s, Vector3 pos, float startAngle)
    {
        int edges = Mathf.Max(3, s.count);
        int bulletCount = 32;
        float segmentAngle = 360f / edges;
        for (int i = 0; i < bulletCount; i++)
        {
            float angleDeg = i * (360f / bulletCount) + startAngle;
            float relativeAngle = ((angleDeg - startAngle) % segmentAngle) - (segmentAngle / 2f);
            float speedMult = 1f / Mathf.Cos(relativeAngle * Mathf.Deg2Rad);
            CreateShot(s.bulletData, pos, s.speed * speedMult, angleDeg);
        }
    }
}