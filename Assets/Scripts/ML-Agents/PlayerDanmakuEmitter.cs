using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
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
    // 現在アクティブなコルーチンの数をカウント[cite: 7]
    private int _activeSkillCoroutines = 0;

    // スキル使用中（コルーチンが1つ以上動いている）かどうかを返すプロパティ
    public bool IsAnySkillActive => _activeSkillCoroutines > 0;
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
        // ★ 追加：スキル使用時に超必殺技ゲージを溜める
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();
        if (myMove != null)
        {
            // インスペクターで設定した ultimateGain 分だけ加算
            myMove.AddUltimateEnergy(s.ultimateGain);
        }
        // ★ 修正：DefensiveField も SE再生を遅延させるため、ここでの再生対象から外す
        if (s.patternType != SkillPatternType.MovingArc &&
            s.patternType != SkillPatternType.RandomRound &&
            s.patternType != SkillPatternType.DefensiveField)
        {
            PlaySkillSE(s.sePath);
        }
        if (s.patternType != SkillPatternType.DefensiveField &&
        s.patternType != SkillPatternType.MovingArc &&
        s.moveSpeedMultiplier < 1.0f)
        {
            StartCoroutine(TemporarySlow(s.moveSpeedMultiplier, 0.2f)); // 0.2秒間だけ減速
        }
        float targetAngle = GetAngleToTarget();
        float baseAngle = targetAngle + s.angleOffset;
        Vector3 pos = transform.position;
        switch (s.patternType)
        {
            case SkillPatternType.Standard:
                if (s.bulletData.isLaser) StartCoroutine(LaserRoutine(s, false));
                else CreateShot(s.bulletData, pos, s.speed, baseAngle, s.delay);
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
                StartCoroutine(ShootBoomerangRoutine(s));
                break;
            case SkillPatternType.DefensiveField:
                // ★ 修正：即時実行ではなく、チャージ演出コルーチンを開始する
                StartCoroutine(ChargeAndExecuteDefensiveField(s));
                break;
            case SkillPatternType.ChainRandomAim:
                StartCoroutine(ChainRandomAimRoutine(s));
                break;
            case SkillPatternType.RotatingAllWayLaser:
                StartCoroutine(RotatingAllWayLaserRoutine(s));
                break;
        }
    }
    private IEnumerator ChainRandomAimRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行中カウントを増やす（エネルギー回復停止）

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        // 1. スキル使用中の減速を適用
        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        if (PlayerMove.CanShoot && (myHH == null || myHH.currentState == PlayerHitHandler.PlayerState.Normal))
        {
            // --- セット開始時の初期化 ---
            // 自機周辺のランダムな位置を弾源に設定
            float radius = 1.8f;
            Vector2 randomCircle = Random.insideUnitCircle.normalized * radius;
            Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, randomCircle.y, 0);

            // ★ セット内で角度を固定：弾源から敵機への基本角度を一度だけ計算
            float targetAngle = GetAngleToTarget(spawnPos) + Random.Range(-3.0f,3.0f);
            float baseAngle = targetAngle + s.angleOffset;

            // 規定回数（6回）を連射
            int burstCount = 6;
            for (int i = 0; i < burstCount; i++)
            {
                // --- N-way（扇形）の生成ロジック ---
                int wayCount = Mathf.Max(1, s.count); // 3way, 5wayなど
                float spread = s.wideAngle;

                if (wayCount <= 1)
                {
                    // 1-wayの場合は正面のみ
                    CreateShot(s.bulletData, spawnPos, s.speed, baseAngle, s.delay);
                }
                else
                {
                    // 複数wayの場合は扇形に展開
                    float startAngle = baseAngle - (spread / 2f);
                    float stepAngle = spread / (wayCount - 1);

                    for (int j = 0; j < wayCount; j++)
                    {
                        float finalAngle = startAngle + (stepAngle * j);
                        CreateShot(s.bulletData, spawnPos, s.speed, finalAngle, s.delay);
                    }
                }

                PlaySkillSE(s.sePath);

                // 2フレーム待機 (FixedUpdate 2回分)
                for (int j = 0; j < 7; j++)
                {
                    yield return new WaitForFixedUpdate();
                }
                // 被弾中断チェック
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) break;
            }
        }

        // 次のセットまでの待機
        yield return new WaitForSeconds(s.cooldown);

        // 状態を戻す
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }
    private IEnumerator TemporarySlow(float multiplier, float duration)
    {
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        if (myMove != null) myMove.skillSpeedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
    }
    // --- ★ 追加：防御フィールド専用のチャージ演出ルーチン ---
    private IEnumerator ChargeAndExecuteDefensiveField(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        // ★ 修正：_rootOwner から確実に PlayerMove を取得する
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();

        if (myMove != null)
        {
            // デバッグログ：現在の設定値をコンソールに表示して確認
            Debug.Log($"Charge Start: Multiplier set to {s.moveSpeedMultiplier}");
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
    }
        else
        {
            Debug.LogError("PlayerMove could not be found on _rootOwner!");
        }

        // チャージ演出
        float chargeTime = 0.3f;
        if (BossEffectManager.Instance != null)
        {
            BossEffectManager.Instance.PlayChargeEffect(chargeTime, s.bulletData.breakColor, transform.position);
    }
        yield return new WaitForSeconds(chargeTime);

        SEManager.Instance.Play(SEPath.SHOT1, 0.2f);
        // スキル本体の生成
        ExecuteDefensiveField(s);

        // スキル終了まで待機（DefensiveFieldの持続時間に合わせる）
        yield return new WaitForSeconds(1.5f);

        // 倍率を戻す
        if (myMove != null)
        {
            Debug.Log("Charge End: Multiplier reset to 1.0");
            myMove.skillSpeedMultiplier = 1.0f; 
    }
        _activeSkillCoroutines--;
    }

    private IEnumerator MovingArcRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
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
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
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
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }

    private IEnumerator ExecuteRandomRoundRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行中カウントを増やす（コスト回復を止める）
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        int wayCount = 12;

        // 1. スキル使用中の減速を適用
        if (myMove != null)
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        }

        // --- 単発分（1セット）の弾幕生成ロジック ---
        if (PlayerMove.CanShoot && (myHH == null || myHH.currentState == PlayerHitHandler.PlayerState.Normal))
        {
            Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1.5f, 1.5f), 0);
            Vector3 spawnPos = transform.position + randomOffset;

            float targetAngle = GetAngleToTarget(spawnPos);
            float baseAngle = targetAngle + s.angleOffset;
            float step = 360f / wayCount;
            float rotationOffset = step / 2f;

            // 弾幕の速度をランダム化
            float randomizedBulletSpeed = s.speed + Random.Range(-1.0f, 1.0f);
            randomizedBulletSpeed = Mathf.Max(0.5f, randomizedBulletSpeed);

            PlaySkillSE(s.sePath);

            for (int i = 0; i < wayCount; i++)
            {
                float finalAngle = baseAngle + rotationOffset + (step * i);
                CreateShot(s.bulletData, spawnPos, randomizedBulletSpeed, finalAngle, s.delay);
            }
        }

        // 2. ★ 重要：次の射撃が可能になるまで（cooldown秒間）状態を維持する
        // これにより、連射中に「速度制限」と「コスト回復停止」が継続します
        float waitTime = Mathf.Max(0.1f, s.cooldown);
        yield return new WaitForSeconds(waitTime);

        // 3. 速度制限を解除し、実行中カウントを減らす
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }
    // ★ void から IEnumerator に変更
    private IEnumerator ShootBoomerangRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行カウントを増やす

        // --- 既存の生成ロジック ---
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

        // ビットの初期化
        bit.Initialize(transform, targetTransform, s.bulletData, 4.0f, this);

        // --- ここがポイント：2秒間待機 ---
        // この間 IsAnySkillActive が true になり、SkillManager 側の回復が止まります
        yield return new WaitForSeconds(2.0f);

        _activeSkillCoroutines--; // 2秒経ったらカウントを減らす
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

    /// <summary>
    /// 設置型または追従型の極太レーザーを実行する
    /// </summary>
    

    private void CreateShot(BulletData data, Vector3 pos, float speed, float angle, float delay, bool isConverge = false)
    {
        GameObject obj = Instantiate(data.bulletPrefab, pos, Quaternion.identity);

        // ★ 追加：発射したプレイヤーに応じて弾にタグとレイヤーを設定する
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        // タグの設定 (P1の弾 = PlayerBullet, P2の弾 = EnemyBullet)
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        obj.tag = assignedTag;

        // レイヤーの設定 (衝突判定の分離用)
        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        obj.layer = assignedLayer;
        SetLayerRecursive(obj, assignedLayer);

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
    /// <summary>
    /// レーザーの生成とパラメータ設定を行うコルーチン
    /// </summary>
    private IEnumerator LaserRoutine(PlayerSkillData.SkillSettings s, bool isFollow)
    {
        _activeSkillCoroutines++;

        if (BulletManager.Instance == null)
        {
            _activeSkillCoroutines--;
            yield break;
        }

        BulletManager.LaserColor color = s.bulletData.laserColor;
        var laserSet = BulletManager.Instance.GetLaserSet(color);

        // 生成
        GameObject laserObj = Instantiate(BulletManager.Instance.laserBeamPrefab, transform.position, Quaternion.identity);
        EnemyLaserBeam laser = laserObj.GetComponent<EnemyLaserBeam>();

        if (laser != null)
        {
            // ★修正：targetTag と damage を渡す
            if (isFollow)
            {
                // SetupBの実装も同様に修正が必要
            }
            else
            {
                laser.SetupA(_rootOwner, targetTag, s.bulletData.damage,
                             transform.position.x, transform.position.y, s.count, s.wideAngle,
                             color, (int)s.delay, BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite);
            }

            // 角度設定（1セット目は現在のターゲット方向）
            float angle = GetAngleToTarget() + s.angleOffset;
            laser.AddData(new EnemyLaserBeam.LaserTransformData { frame = 0, angle = angle });
            laser.Fire();

            // 持続時間（Cooldown）待機
            yield return new WaitForSeconds(s.speed);

            // 消滅命令
            if (laser != null) laser.ForceClose();
        }

        _activeSkillCoroutines--;
    }
    // --- PlayerDanmakuEmitter.cs 修正版ルーチン ---

    private IEnumerator RotatingAllWayLaserRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        if (BulletManager.Instance == null) { _activeSkillCoroutines--; yield break; }

        List<EnemyLaserBeam> spawnedLasers = new List<EnemyLaserBeam>();

        // --- 設定パラメータ ---
        int laserCount = Mathf.Max(1, 24); // 18本
        float radius = 1.6f;             // 弾源の半径
        int stopFrame = 40;              // 回転が止まり始めるフレーム
        int warningFrame = stopFrame + 60; // 完全に止まってから実線化するまでの「タメ」

        // ★ 回転方向をランダムに決定
        float rotDir = (Random.value < 0.5f) ? 1.0f : -1.0f;
        float initialRotSpeed = 5.0f * rotDir;

        // ★ 追加：回転中にかける「ズレ」の総量
        float totalDriftAngle = 30f * rotDir;
        float driftVelocity = totalDriftAngle / stopFrame;

        // ★ 修正：停止位置（目標角度）をランダムに決定
        float targetAngle = Random.Range(0f, 360f);

        // 停止位置から逆算して、開始時のベース角度を求める
        float estimatedRotation = 245f * rotDir;
        float baseAngle = targetAngle - estimatedRotation;

        BulletManager.LaserColor color = s.bulletData.laserColor;
        var laserSet = BulletManager.Instance.GetLaserSet(color);

        for (int i = 0; i < laserCount; i++)
        {
            GameObject laserObj = Instantiate(BulletManager.Instance.laserBeamPrefab, transform.position, Quaternion.identity);
            EnemyLaserBeam laser = laserObj.GetComponent<EnemyLaserBeam>();

            if (laser != null)
            {
                spawnedLasers.Add(laser);

                // ★ 発射時の自機座標を centerPos として固定する SetupB を使用
                laser.SetupB(_rootOwner, targetTag, s.bulletData.damage,
                             transform.position.x, transform.position.y,
                             s.count, s.wideAngle, color, warningFrame,
                             BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite);

                float currentStartAngle = baseAngle + (360f / laserCount * i);

                // 初期オフセット（150度は渦を巻くような大きな曲がり）
                float aimOffset = 120f * rotDir;
                float initialLaserAngle = currentStartAngle + aimOffset;

                // データ1：回転開始
                laser.AddData(new EnemyLaserBeam.LaserTransformData
                {
                    frame = 0,
                    dist = radius,                // 半径分離す
                    distAngle = currentStartAngle, // 弾源の公転角
                    laserAngle = initialLaserAngle, // レーザー自体の向き（曲げる）
                    distAngleVel = initialRotSpeed,
                    laserAngleVel = initialRotSpeed + driftVelocity, // ★徐々にズレるように自転速度を微調整
                    isSmooth = true
                });

                // データ2：停止
                laser.AddData(new EnemyLaserBeam.LaserTransformData
                {
                    frame = stopFrame,
                    distAngleVel = 0f,
                    laserAngleVel = 0f,
                    isSmooth = true
                });

                laser.Fire();
            }
        }

        // 照射終了まで待機
        yield return new WaitForSeconds((warningFrame / 60f) + s.speed);

        // 全て消去
        foreach (var laser in spawnedLasers)
        {
            if (laser != null) laser.ForceClose();
        }

        _activeSkillCoroutines--;
    }
    private void PlaySkillSE(string path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
            SEManager.Instance.Play(clip, 0.4f);
    }
}