using UnityEngine;

/// <summary>
/// プレイヤーのスキル設定に基づき、実際に弾幕を生成・射出するクラス
/// </summary>
public class PlayerDanmakuEmitter : MonoBehaviour
{
    /// <summary>
    /// SkillManagerから呼ばれ、指定された設定で弾を放つ
    /// </summary>
    public void Fire(PlayerSkillData.SkillSettings s)
    {
        if (s.bulletData == null || s.bulletData.bulletPrefab == null) return;

        // 正面（上向き）を90度とし、インスペクターでのオフセットを加味
        float baseAngle = 90f + s.angleOffset;
        Vector3 pos = transform.position;

        // 対戦相手のタグ（1vs1なら相手もPlayerタグである前提）
        // 自分の弾かどうかは DanmakuBullet 側の owner チェックで判別します
        string targetTag = "Player";

        switch (s.patternType)
        {
            case SkillPatternType.Standard:
                CreateShot(s.bulletData, pos, s.speed, baseAngle, targetTag, s.delay);
                break;

            case SkillPatternType.nWay:
                {
                    int count = Mathf.Max(1, s.count);

                    if (count == 1)
                    {
                        CreateShot(s.bulletData, pos, s.speed, baseAngle, targetTag, s.delay);
                    }
                    else
                    {
                        // 偶数弾なら「自機外し」になるように角度配分をずらす（中央に弾が来ない）
                        float wayAngle;
                        float startAngle;

                        if (count % 2 == 0)
                        {
                            // even: 区間を count 等分して start を half-step 分ずらす
                            wayAngle = s.wideAngle / count;
                            startAngle = baseAngle - (s.wideAngle / 2f) + (wayAngle / 2f);
                        }
                        else
                        {
                            // odd: 従来どおり (count-1) による等間隔（中央弾が baseAngle）
                            wayAngle = s.wideAngle / (count - 1);
                            startAngle = baseAngle - (s.wideAngle / 2f);
                        }

                        for (int i = 0; i < count; i++)
                            CreateShot(s.bulletData, pos, s.speed, startAngle + (wayAngle * i), targetTag, s.delay);
                    }
                    break;
                }

            case SkillPatternType.Round:
                {
                    int count = Mathf.Max(1, s.count);
                    float step = 360f / count;
                    // 偶数弾なら半ステップ分回転させて中央方向を空ける
                    float rotationOffset = (count % 2 == 0) ? (step / 2f) : 0f;

                    for (int i = 0; i < count; i++)
                        CreateShot(s.bulletData, pos, s.speed, baseAngle + rotationOffset + (step * i), targetTag, s.delay);
                    break;
                }

            case SkillPatternType.Polygon:
                ExecutePolygon(s, pos, baseAngle, targetTag);
                break;

            case SkillPatternType.Line:
                // 少しずつ速度を変えて一直線に放つ
                for (int i = 0; i < s.count; i++)
                    CreateShot(s.bulletData, pos, s.speed + (i * 0.4f), baseAngle, targetTag, s.delay);
                break;

            case SkillPatternType.Custom:
                // ★ 収束（Converge）パターンの実行
                ExecuteConvergePattern(s, pos, baseAngle, targetTag);
                break;
        }
    }

    /// <summary>
    /// 外側から収束するアニメーションを伴う射出パターン
    /// </summary>
    private void ExecuteConvergePattern(PlayerSkillData.SkillSettings s, Vector3 pos, float baseAngle, string target)
    {
        int count = Mathf.Max(1, s.count);
        float step = 360f / count;
        float spawnDistance = 2.5f; // 収束を開始する外側の距離

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + (step * i);
            float rad = angle * Mathf.Deg2Rad;

            // 最初は外側に配置
            Vector3 spawnPos = pos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * spawnDistance;

            // 弾を生成
            GameObject obj = Instantiate(s.bulletData.bulletPrefab, spawnPos, Quaternion.identity);
            DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();

            if (bullet != null)
            {
                // Initialize を呼ぶ
                // DanmakuBullet 側で delay 中に owner へ近づく処理が実装されている場合、
                // 外側から中心へ集まるアニメーションになります。
                bullet.Initialize(
                    transform.root.gameObject,
                    target,
                    s.speed,
                    angle,
                    0,
                    s.speed,
                    0,
                    s.delay,
                    s.bulletData
                );
            }
        }
    }

    /// <summary>
    /// 実際にプレハブを生成し、オーナー情報やターゲットを渡して初期化する
    /// </summary>
    private void CreateShot(BulletData data, Vector3 pos, float speed, float angle, string target, float delay)
    {
        GameObject obj = Instantiate(data.bulletPrefab, pos, Quaternion.identity);
        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();

        if (bullet != null)
        {
            // ★重要：ownerに transform.root.gameObject を渡す
            // これにより、どの子オブジェクト（HitBox等）に当たっても自爆しなくなります。
            bullet.Initialize(
                transform.root.gameObject,
                target,
                speed,
                angle,
                0,      // 加速度 (accel)
                speed,  // 最高速度 (maxSpeed)
                0,      // 角速度 (angVel)
                delay,  // 遅延時間 (delay)
                data    // 弾データアセット
            );
        }
    }

    /// <summary>
    /// 多角形形状の弾幕を展開する
    /// </summary>
    private void ExecutePolygon(PlayerSkillData.SkillSettings s, Vector3 pos, float startAngle, string target)
    {
        int edges = Mathf.Max(3, s.count);
        int bulletCount = 32; // 1つの図形を構成する弾数
        float segmentAngle = 360f / edges;

        for (int i = 0; i < bulletCount; i++)
        {
            float angleDeg = i * (360f / bulletCount) + startAngle;
            float relativeAngle = ((angleDeg - startAngle) % segmentAngle) - (segmentAngle / 2f);

            // コサインを用いて辺を直線にするための速度倍率を計算
            float speedMult = 1f / Mathf.Cos(relativeAngle * Mathf.Deg2Rad);

            CreateShot(s.bulletData, pos, s.speed * speedMult, angleDeg, target, s.delay);
        }
    }
}