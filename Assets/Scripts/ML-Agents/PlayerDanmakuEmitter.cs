using UnityEngine;

/// <summary>
/// プレイヤーのスキル設定に基づき、実際に弾幕を生成・射出するクラス
/// 1vs1対戦対応版: 相手のタグ指定と自爆防止、収束パターンの完全連動
/// </summary>
public class PlayerDanmakuEmitter : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("攻撃対象（相手）のタグ")]
    public string targetTag = "Player";

    private GameObject _rootOwner;

    private void Awake()
    {
        // 自分のルート（Player本体のGameObject）を取得
        // これを渡すことで DanmakuBullet 側で「自分への当たり判定」を無視します
        _rootOwner = transform.root.gameObject;
    }

    /// <summary>
    /// SkillManagerから呼ばれ、指定された設定で弾を放つ
    /// </summary>
    public void Fire(PlayerSkillData.SkillSettings s)
    {
        if (s.bulletData == null || s.bulletData.bulletPrefab == null) return;

        // 正面（上向き）を90度とし、インスペクターでのオフセットを加味
        float baseAngle = 90f + s.angleOffset;
        Vector3 pos = transform.position;

        switch (s.patternType)
        {
            case SkillPatternType.Standard:
                CreateShot(s.bulletData, pos, s.speed, baseAngle, s.delay);
                break;

            case SkillPatternType.nWay:
                ExecuteNWay(s, pos, baseAngle);
                break;

            case SkillPatternType.Round:
                ExecuteRound(s, pos, baseAngle);
                break;

            case SkillPatternType.Polygon:
                ExecutePolygon(s, pos, baseAngle);
                break;

            case SkillPatternType.Line:
                // 少しずつ速度を変えて一直線に放つ
                for (int i = 0; i < s.count; i++)
                    CreateShot(s.bulletData, pos, s.speed + (i * 0.4f), baseAngle, s.delay);
                break;

            case SkillPatternType.Custom:
                // ★ 収束（Converge）パターンの実行
                ExecuteConvergePattern(s, pos, baseAngle);
                break;
        }
    }

    private void ExecuteNWay(PlayerSkillData.SkillSettings s, Vector3 pos, float baseAngle)
    {
        int count = Mathf.Max(1, s.count);
        if (count == 1)
        {
            CreateShot(s.bulletData, pos, s.speed, baseAngle, s.delay);
            return;
        }

        float wayAngle;
        float startAngle;

        if (count % 2 == 0)
        {
            wayAngle = s.wideAngle / count;
            startAngle = baseAngle - (s.wideAngle / 2f) + (wayAngle / 2f);
        }
        else
        {
            wayAngle = s.wideAngle / (count - 1);
            startAngle = baseAngle - (s.wideAngle / 2f);
        }

        for (int i = 0; i < count; i++)
            CreateShot(s.bulletData, pos, s.speed, startAngle + (wayAngle * i), s.delay);
    }

    private void ExecuteRound(PlayerSkillData.SkillSettings s, Vector3 pos, float baseAngle)
    {
        int count = Mathf.Max(1, s.count);
        float step = 360f / count;
        float rotationOffset = (count % 2 == 0) ? (step / 2f) : 0f;

        for (int i = 0; i < count; i++)
            CreateShot(s.bulletData, pos, s.speed, baseAngle + rotationOffset + (step * i), s.delay);
    }

    /// <summary>
    /// 外側から収束するアニメーションを伴う射出パターン
    /// DanmakuBullet の isConverging フラグを true にして生成します
    /// </summary>
    private void ExecuteConvergePattern(PlayerSkillData.SkillSettings s, Vector3 pos, float baseAngle)
    {
        int count = Mathf.Max(1, s.count);
        float step = 360f / count;
        float spawnDistance = 2.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + (step * i);
            float rad = angle * Mathf.Deg2Rad;

            // 最初は自機の周囲（spawnDistance離れた位置）に配置
            Vector3 spawnPos = pos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * spawnDistance;

            // converge 引数を true で呼び出す
            CreateShot(s.bulletData, spawnPos, s.speed, angle, s.delay, true);
        }
    }

    /// <summary>
    /// 実際にプレハブを生成し、DanmakuBulletを初期化する
    /// </summary>
    private void CreateShot(BulletData data, Vector3 pos, float speed, float angle, float delay, bool isConverge = false)
    {
        GameObject obj = Instantiate(data.bulletPrefab, pos, Quaternion.identity);
        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();

        if (bullet != null)
        {
            // DanmakuBullet.Initialize の引数順序に合わせて流し込む
            bullet.Initialize(
                _rootOwner,     // shooter (自機)
                targetTag,      // target
                speed,          // speed
                angle,          // angle
                0,              // accel
                speed,          // maxSpeed
                0,              // angVel
                delay,          // delay
                data,           // data
                isConverge      // converge フラグ
            );
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

            CreateShot(s.bulletData, pos, s.speed * speedMult, angleDeg, s.delay);
        }
    }
}