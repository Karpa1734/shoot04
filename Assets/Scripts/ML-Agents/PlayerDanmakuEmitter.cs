using KanKikuchi.AudioManager;
using System.Collections;
using Unity.AppUI.UI;
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
    private bool _isArcReversed = false;

    private void Awake()
    {
        _rootOwner = transform.root.gameObject;
    }

    private float GetAngleToTarget()
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
            Vector3 dir = target.position - transform.position;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus != null && myStatus.playerId == 2) return 180f;
        return 0f;
    }

    private float GetAngleToTarget(Vector3 fromPos)
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
            Vector3 dir = target.position - fromPos;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
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

        // ★ 修正：DefensiveField も SE再生を遅延させるため、ここでの再生対象から外す
        if (s.patternType != SkillPatternType.MovingArc &&
            s.patternType != SkillPatternType.RandomRound &&
            s.patternType != SkillPatternType.DefensiveField)
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
            case SkillPatternType.Boomerang:
                ShootBoomerangBit(s);
                break;
            case SkillPatternType.DefensiveField:
                // ★ 修正：即時実行ではなく、チャージ演出コルーチンを開始する
                StartCoroutine(ChargeAndExecuteDefensiveField(s));
                break;
        }
    }

    // --- ★ 追加：防御フィールド専用のチャージ演出ルーチン ---
    private IEnumerator ChargeAndExecuteDefensiveField(PlayerSkillData.SkillSettings s)
    {
        float chargeTime = 0.1f; // チャージ時間

        if (BossEffectManager.Instance != null)
        {
            // 弾の breakColor を使ってチャージ粒子を生成
            for (int i = 0; i < 5; i++)
            {
                BossEffectManager.Instance.PlayChargeEffect(chargeTime, s.bulletData.breakColor, transform.position);
            }
        }

        // チャージ完了を待機
        yield return new WaitForSeconds(chargeTime+0.5f);

        // スタンなどで射撃不可になっていないか再チェック
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) yield break;

        // チャージ完了後にSEを鳴らして発動
        PlaySkillSE(s.sePath);
        ExecuteDefensiveField(s);
    }

    private IEnumerator MovingArcRoutine(PlayerSkillData.SkillSettings s)
    {
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        float radiusX = 1.5f;
        float radiusY = 0.4f;
        int wayCount = 3;
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
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) yield break;
            float spawnAngleRad = (centerTargetAngle + offset) * Mathf.Deg2Rad;
            Vector3 ellipseOffset = new Vector3(Mathf.Cos(spawnAngleRad) * radiusX, Mathf.Sin(spawnAngleRad) * radiusY, 0);
            Vector3 spawnPos = transform.position + ellipseOffset;
            float realAimAngle = GetAngleToTarget(spawnPos) + s.angleOffset;
            float currentWideAngle = 60f;
            float startAngle = realAimAngle - (currentWideAngle / 2f);
            float stepAngle = (wayCount > 1) ? currentWideAngle / (wayCount - 1) : 0;
            PlaySkillSE(s.sePath);
            for (int i = 0; i < wayCount; i++)
            {
                CreateShot(s.bulletData, spawnPos, s.speed, startAngle + (stepAngle * i), s.delay);
            }
            for (int f = 0; f < 2; f++) yield return new WaitForFixedUpdate();
        }
    }

    private IEnumerator ExecuteRandomRoundRoutine(PlayerSkillData.SkillSettings s)
    {
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        int burstCount = 7;
        int wayCount = 12;

        for (int j = 0; j < burstCount; j++)
        {
            if (!PlayerMove.CanShoot) yield break;
            if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) yield break;
            Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1.5f, 1.5f), 0);
            Vector3 spawnPos = transform.position + randomOffset;
            float targetAngle = GetAngleToTarget(spawnPos);
            float baseAngle = targetAngle + s.angleOffset;
            float speed = s.speed + (j * 0.3f);
            float step = 360f / wayCount;
            float rotationOffset = step / 2f;
            PlaySkillSE(s.sePath);
            for (int i = 0; i < wayCount; i++)
            {
                float finalAngle = baseAngle + rotationOffset + (step * i);
                CreateShot(s.bulletData, spawnPos, speed, finalAngle, s.delay);
            }
            for (int f = 0; f < 3; f++) yield return new WaitForFixedUpdate();
        }
    }

    private void ShootBoomerangBit(PlayerSkillData.SkillSettings s)
    {
        GameObject bitObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        string assignedLayer = (ownerId == 1) ? "Player1Bullet" : "Player2Bullet";
        bitObj.tag = assignedTag;
        bitObj.layer = LayerMask.NameToLayer(assignedLayer);
        SetLayerRecursive(bitObj, LayerMask.NameToLayer(assignedLayer));
        BoomerangObject bit = bitObj.GetComponent<BoomerangObject>();
        if (bit == null) bit = bitObj.AddComponent<BoomerangObject>();
        Transform targetTransform = null;
        foreach (var p in PlayerMove.AllPlayers)
            if (p != null && p.gameObject != _rootOwner) targetTransform = p.transform;
        bit.Initialize(transform, targetTransform, s.bulletData, 4.0f, this);
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    public void ExecuteSubShot(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer)
    {
        GameObject obj = Instantiate(data.bulletPrefab, pos, Quaternion.identity);
        obj.tag = tag;
        obj.layer = layer;
        SEManager.Instance.Play(SEPath.SHOT2, 0.2f);
        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
        if (bullet != null)
            bullet.Initialize(_rootOwner, targetTag, speed, angle, accel, maxSpeed, 0, 0, data);
    }

    private void ExecuteDefensiveField(PlayerSkillData.SkillSettings s)
    {
        GameObject fieldObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        var field = fieldObj.GetComponent<DefensiveField>();
        if (field == null) field = fieldObj.AddComponent<DefensiveField>();
        field.Initialize(transform, s.bulletData, 1.5f, assignedTag, assignedLayer);
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

    private void ExecuteConvergePattern(PlayerSkillData.SkillSettings s, Vector3 pos, float baseAngle)
    {
        int count = Mathf.Max(1, s.count);
        float step = 360f / count;
        float spawnDistance = 2.5f;
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
            bullet.Initialize(_rootOwner, targetTag, speed, angle, 0, speed, 0, delay, data, isConverge);
    }

    private void ExecutePolygon(PlayerSkillData.SkillSettings s, Vector3 pos, float startAngle)
    {
        int edges = Mathf.Max(3, s.count);
        int bulletCount = 32;
        float segmentAngle = 360f / edges;
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

    private void PlaySkillSE(string path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
            SEManager.Instance.Play(clip, 0.4f);
    }
}