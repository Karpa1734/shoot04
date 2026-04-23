using UnityEngine;

/// <summary>
/// プレイヤーのスキル設定に基づき、実際に弾幕を生成・射出するクラス
/// 1vs1対戦対応：奇数弾は自機狙い、偶数弾は自機外しを自動計算
/// </summary>
public class PlayerDanmakuEmitter : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("攻撃対象（相手）のタグ")]
    public string targetTag = "Player";

    private GameObject _rootOwner;

    private void Awake()
    {
        _rootOwner = transform.root.gameObject;
    }

    /// <summary>
    /// 相手プレイヤーへの角度を取得する。相手がいない場合は正面(90度)を返す。
    /// </summary>
    private float GetAngleToTarget()
    {
        Transform target = null;

        // PlayerMoveのリストを走査（Awake/OnDestroy管理になったので、スタン中の敵も含まれる）
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != _rootOwner)
            {
                target = p.transform;
                break;
            }
        }

        if (target != null)
        {
            // 相手がスタン中で画面外（旧仕様）にいても、現在の座標で計算する
            Vector3 dir = target.position - transform.position;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        // ターゲットが見つからない場合、1vs1なら「自分の反対側」を向くようにすると自然です
        // 例: P1(左)なら右(0度)、P2(右)なら左(180度)
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus != null && myStatus.playerId == 2) return 180f;
        return 0f;
    }

    public void Fire(PlayerSkillData.SkillSettings s)
    {
        // 1. 自分自身の状態（スタン中かどうか）をチェックする
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();

        // 2. 全体的な射撃禁止（ラウンド開始前など）をチェック
        if (!PlayerMove.CanShoot) return;

        // 3. ★修正：自分自身が Normal 状態でない（自分がスタン中など）時だけ、射撃を中止する
        // これにより、相手がスタンしていても自分は撃ち続けることができます
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;

        // --- 以降のコード（s.bulletData のチェックや switch文）はそのまま ---
        if (s.bulletData == null || s.bulletData.bulletPrefab == null) return;

        // ★ 修正：ベースの角度を「相手の方向」にする
        float targetAngle = GetAngleToTarget();
        float baseAngle = targetAngle + s.angleOffset;
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
                for (int i = 0; i < s.count; i++)
                    CreateShot(s.bulletData, pos, s.speed + (i * 0.4f), baseAngle, s.delay);
                break;

            case SkillPatternType.Custom:
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

        // ★ 偶数弾なら中央を空ける（自機外し）、奇数弾なら中央に飛ばす（自機狙い）
        if (count % 2 == 0)
        {
            // Even: 中央の角度（baseAngle）を挟んで左右に弾を配置
            wayAngle = s.wideAngle / count;
            startAngle = baseAngle - (s.wideAngle / 2f) + (wayAngle / 2f);
        }
        else
        {
            // Odd: 中央の角度（baseAngle）に必ず1発飛ぶ
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

        // ★ 全方位でも偶数なら正面（baseAngle方向）を避ける
        float rotationOffset = (count % 2 == 0) ? (step / 2f) : 0f;

        for (int i = 0; i < count; i++)
            CreateShot(s.bulletData, pos, s.speed, baseAngle + rotationOffset + (step * i), s.delay);
    }

    private void ExecuteConvergePattern(PlayerSkillData.SkillSettings s, Vector3 pos, float baseAngle)
    {
        int count = Mathf.Max(1, s.count);
        float step = 360f / count;
        float spawnDistance = 2.5f;

        // 収束パターンでも偶数なら正面を避けるオフセットを適用
        float rotationOffset = (count % 2 == 0) ? (step / 2f) : 0f;

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + rotationOffset + (step * i);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 spawnPos = pos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * spawnDistance;

            CreateShot(s.bulletData, spawnPos, s.speed, angle, s.delay, true);
        }
    }

    private void CreateShot(BulletData data, Vector3 pos, float speed, float angle, float delay, bool isConverge = false)
    {
        GameObject obj = Instantiate(data.bulletPrefab, pos, Quaternion.identity);
        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();

        if (bullet != null)
        {
            bullet.Initialize(_rootOwner, targetTag, speed, angle, 0, speed, 0, delay, data, isConverge);
        }
    }

    private void ExecutePolygon(PlayerSkillData.SkillSettings s, Vector3 pos, float startAngle)
    {
        int edges = Mathf.Max(3, s.count);
        int bulletCount = 32;
        float segmentAngle = 360f / edges;

        // 多角形も頂点が偶数なら正面を避けるように回転
        float rotationOffset = (edges % 2 == 0) ? (segmentAngle / 2f) : 0f;
        float finalStartAngle = startAngle + rotationOffset;

        for (int i = 0; i < bulletCount; i++)
        {
            float angleDeg = i * (360f / bulletCount) + finalStartAngle;
            float relativeAngle = ((angleDeg - finalStartAngle) % segmentAngle) - (segmentAngle / 2f);
            float speedMult = 1f / Mathf.Cos(relativeAngle * Mathf.Deg2Rad);

            CreateShot(s.bulletData, pos, s.speed * speedMult, angleDeg, s.delay);
        }
    }
}