using KanKikuchi.AudioManager;
using System.Collections;
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
    // ★ 追加：射撃方向を交互に切り替えるためのフラグ
    private bool _isArcReversed = false;
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
    /// <summary>
    /// 指定した座標（fromPos）からターゲットへの角度を取得する
    /// </summary>
    private float GetAngleToTarget(Vector3 fromPos) // ★ 引数を追加
    {
        Transform target = null;

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
            // ★ 修正：自機の位置ではなく、引数の fromPos からのベクトルを計算する
            Vector3 dir = target.position - fromPos;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        // ターゲットがいない場合のデフォルト
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus != null && myStatus.playerId == 2) return 180f;
        return 0f;
    }
    public void Fire(PlayerSkillData.SkillSettings s)
    {
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (!PlayerMove.CanShoot) return;
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;
        if (s.bulletData == null || s.bulletData.bulletPrefab == null) return;

        // ★ 追加：コルーチンを使わない即時発射パターンの場合はここでSEを鳴らす
        if (s.patternType != SkillPatternType.MovingArc && s.patternType != SkillPatternType.RandomRound)
        {
            PlaySkillSE(s.sePath);
        }

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
            case SkillPatternType.MovingArc:
                StartCoroutine(MovingArcRoutine(s));
                break;
            case SkillPatternType.RandomRound:
                StartCoroutine(ExecuteRandomRoundRoutine(s));
                break;
        }
    }
    /// <summary>
    /// 弾源を円弧上で動かしながら連射する（実行ごとに方向が反転）
    /// </summary>
    private IEnumerator MovingArcRoutine(PlayerSkillData.SkillSettings s)
    {
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();

        // ★ 修正：半径を X（横）と Y（縦）で個別に定義する
        // 横長にするために radiusX > radiusY に設定します
        float radiusX = 1.5f; // 横の広がり
        float radiusY = 0.4f; // 縦の厚み
        int wayCount = 5;

        bool currentDirectionReversed = _isArcReversed;
        _isArcReversed = !_isArcReversed;

        float startOffset = currentDirectionReversed ? 90f : -90f;
        float endOffset = currentDirectionReversed ? -90f : 90f;
        float step = currentDirectionReversed ? -20f : 20f;

        float centerTargetAngle = GetAngleToTarget(transform.position);

        for (float offset = startOffset;
             (step > 0 ? offset <= endOffset : offset >= endOffset);
             offset += step)
        {
            // 自分がスタンしたり射撃禁止になったら中断
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) yield break;

            float spawnAngleRad = (centerTargetAngle + offset) * Mathf.Deg2Rad;

            // ★ 修正：X成分に radiusX、Y成分に radiusY を個別に掛けて楕円座標を算出
            Vector3 ellipseOffset = new Vector3(
                Mathf.Cos(spawnAngleRad) * radiusX,
                Mathf.Sin(spawnAngleRad) * radiusY,
                0
            );
            Vector3 spawnPos = transform.position + ellipseOffset;

            // 弾源（spawnPos）から敵への角度を再計算
            float realAimAngle = GetAngleToTarget(spawnPos) + s.angleOffset;

            float currentWideAngle = 70f;
            float startAngle = realAimAngle - (currentWideAngle / 2f);
            float stepAngle = (wayCount > 1) ? currentWideAngle / (wayCount - 1) : 0;
            PlaySkillSE(s.sePath);
            for (int i = 0; i < wayCount; i++)
            {
                CreateShot(s.bulletData, spawnPos, s.speed, startAngle + (stepAngle * i), s.delay);
            }

            // 2フレーム待機
            for (int f = 0; f < 2; f++) yield return new WaitForFixedUpdate();
        }
    }
    /// <summary>
    /// 自機周辺のランダムな座標から18-way全方位弾を7回連射する
    /// </summary>
    private IEnumerator ExecuteRandomRoundRoutine(PlayerSkillData.SkillSettings s)
    {
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        int burstCount = 10; // 7発（7セット）
        int wayCount = 18;  // 18-way（偶数）

        for (int j = 0; j < burstCount; j++)
        {
            // 1. 中断チェック（スタンや射撃禁止）
            if (!PlayerMove.CanShoot) yield break;
            if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) yield break;

            // 2. 弾源の座標をランダムに決定 (X, Y それぞれ -1.0 ～ 1.0 の範囲)
            Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1.5f, 1.5f), 0);
            Vector3 spawnPos = transform.position + randomOffset;

            // 3. 弾源から見た敵への角度を取得
            float targetAngle = GetAngleToTarget(spawnPos);
            float baseAngle = targetAngle + s.angleOffset;
            float speed = s.speed + (j * 0.3f); // 連射ごとに少しずつ速くする
            // 4. 18-way 全方位弾（自機外し）の計算
            float step = 360f / wayCount;
            // 18は偶数なので、真正面（baseAngle）を避けるために step/2 のオフセットを入れる
            float rotationOffset = step / 2f;
            PlaySkillSE(s.sePath);
            for (int i = 0; i < wayCount; i++)
            {
                float finalAngle = baseAngle + rotationOffset + (step * i);
                CreateShot(s.bulletData, spawnPos, speed, finalAngle, s.delay);
            }

            // 5. 2フレーム待機（FixedUpdate基準）
            for (int f = 0; f < 3; f++) yield return new WaitForFixedUpdate();
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
    // ★ SE再生用のヘルパーメソッド
    private void PlaySkillSE(string path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(clip, 0.4f);
        }
    }
}