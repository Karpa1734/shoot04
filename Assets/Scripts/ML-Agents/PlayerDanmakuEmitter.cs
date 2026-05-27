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
            // ★ 新しく追加
            case SkillPatternType.RotatingAccelRound:
                StartCoroutine(RotatingAccelRoundRoutine(s));
                break;
            // ★ 新しく強欲スキルを追加
            case SkillPatternType.GreedTaxPossession:
                StartCoroutine(GreedTaxPossessionRoutine(s));
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
        _activeSkillCoroutines++; //
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>(); //
        float radiusX = 1.5f; //
        float radiusY = 0.4f; //
        int wayCount = 3; //
        bool currentDirectionReversed = _isArcReversed; //
        _isArcReversed = !_isArcReversed; //
        float startOffset = currentDirectionReversed ? 90f : -90f; //
        float endOffset = currentDirectionReversed ? -90f : 90f; //
        float step = currentDirectionReversed ? -20f : 20f; //
        float centerTargetAngle = GetAngleToTarget(transform.position); //
        PlayerMove myMove = GetComponentInParent<PlayerMove>(); //
        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier; //

        for (float offset = startOffset;
             (step > 0 ? offset <= endOffset : offset >= endOffset);
             offset += step)
        {
            // ★ 修正：yield break ではなく break にしてループの下（クリーンアップ処理）へ流す
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                break;

            float spawnAngleRad = (centerTargetAngle + offset) * Mathf.Deg2Rad; //
            Vector3 ellipseOffset = new Vector3(Mathf.Cos(spawnAngleRad) * radiusX, Mathf.Sin(spawnAngleRad) * radiusY, 0); //
            Vector3 spawnPos = transform.position + ellipseOffset; //
            float realAimAngle = GetAngleToTarget(spawnPos) + s.angleOffset; //
            float currentWideAngle = 60f; //
            float startAngle = realAimAngle - (currentWideAngle / 2f); //
            float stepAngle = (wayCount > 1) ? currentWideAngle / (wayCount - 1) : 0; //
            PlaySkillSE(s.sePath); //
            for (int i = 0; i < wayCount; i++) //
            {
                CreateShot(s.bulletData, spawnPos, s.speed, startAngle + (stepAngle * i), s.delay); //
            }
            for (int f = 0; f < 2; f++) yield return new WaitForFixedUpdate(); //
        }

        // これでガード句に引っかかった際も、確実にここを通ってリセットされます
        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f; //
        _activeSkillCoroutines--; //
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
        int laserCount = Mathf.Max(1, 32); // 18本
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
    private bool _isRoundRotReversed = false; // ★ 追加：全方位弾の回転方向反転用フラグ
    /// <summary>
    /// 自機外し全方位弾を、射角を回転させ、段階的に弾速を上げながら連射する
    /// 1回使うごとに回転方向が交互に反転する
    /// </summary>
    private IEnumerator RotatingAccelRoundRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 実行中カウント（MP回復停止）
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>(); //
        PlayerMove myMove = GetComponentInParent<PlayerMove>(); //

        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier; //

        Vector3 pos = transform.position; //

        // 1. 1波あたりの弾数を設定（インスペクターのCountを使用）
        int bulletCount = Mathf.Max(2, s.count); //
        if (bulletCount % 2 != 0) bulletCount++; //

        float step = 360f / bulletCount; //
        float evenWayOffset = step / 2f; //

        // 2. 連射設定と ★回転方向の交互反転ロジック
        int waveLoops = 12; //
        float currentSpeed = s.speed; // 初速（インスペクターのSpeed）

        // ★ 現在の状態を取得し、フラグを反転させて次回に備える
        bool currentRotReversed = _isRoundRotReversed;
        _isRoundRotReversed = !_isRoundRotReversed;

        // フラグに応じて回転方向を 1.0 または -1.0 にする
        float rotDirection = currentRotReversed ? -1f : 1f;
        float angleIncrement = 13f * rotDirection; // ★ 1波ごとの回転角の向きを決定

        // 射撃開始時のターゲットへの基本角度を算出
        float targetAngle = GetAngleToTarget(); //
        float baseAngle = targetAngle + s.angleOffset + evenWayOffset; //

        // 3. バースト連射ループ
        for (int w = 0; w < waveLoops; w++)
        {
            // 被弾時やラウンド終了時の安全ガード
            if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) break; //

            PlaySkillSE(s.sePath); //

            // 1波分（全方位）の弾を生成
            for (int i = 0; i < bulletCount; i++)
            {
                // ベース角 + 全方位分割角 + wによる回転量を加算
                float finalAngle = baseAngle + (step * i) + (angleIncrement * w); //
                CreateShot(s.bulletData, pos, currentSpeed, finalAngle, s.delay); //
            }

            // 次の波の弾速を上げる（段階的加速）
            currentSpeed += 0.5f; //

            // 波と波の間の時間差（1フレーム待機）
            for (int f = 0; f < 3; f++) //
            {
                yield return new WaitForFixedUpdate(); //
            }
        }

        // 次のキャストまでのクールタイム待機
        yield return new WaitForSeconds(s.cooldown); //

        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f; //
        _activeSkillCoroutines--; //
    }
    /// <summary>
    /// 強欲：グリード・タックス＆ポゼッション
    /// 敵弾をかき消して必殺ゲージに変え、その場に一回転するカウンターナイフを生成する防御フィールドを展開
    /// </summary>
    private IEnumerator GreedTaxPossessionRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;

        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();
        if (myMove != null && s.moveSpeedMultiplier < 1.0f)
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        }

        PlaySkillSE(s.sePath);

        // 1. スキルデータに登録された「フィールドプレハブ」を生成
        GameObject fieldObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);

        // 2. 所属チームに応じたタグとレイヤーを生成の瞬間に割り当てる
        var myStatus = GetComponentInParent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");

        fieldObj.tag = assignedTag;
        fieldObj.layer = assignedLayer;
        SetLayerRecursive(fieldObj, assignedLayer);

        // 3. プレハブにあらかじめ付いている GreedTaxPossessionField コンポーネントを取得
        GreedTaxPossessionField fieldLogic = fieldObj.GetComponent<GreedTaxPossessionField>();

        if (fieldLogic != null)
        {
            // ★ ブーメランビットと同様、アタッチされたコンポーネントに必要な参照を渡して初期化
            fieldLogic.Initialize(transform, _rootOwner, targetTag, this);
        }
        else
        {
            Debug.LogError("フィールド用プレハブに GreedTaxPossessionField が付いていません！");
        }

        // 4. フィールドの有効持続時間分、Emitter側も安全に同期待機
        yield return new WaitForSeconds(3.0f + 0.2f);

        if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }

    /// <summary>
    /// 独立したEX枠のデータを受け取り、パターン（s.patternType）に応じて固有の必殺技をキックする
    /// </summary>
    public void FireEX(PlayerSkillData.SkillSettings s)
    {
        if (!PlayerMove.CanShoot) return;

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;

        // 🌟 共通インフラ（硬直制御・例外安全ライフサイクル）を開始
        StartCoroutine(ExecuteEXInfrastructureRoutine(s));
    }

    /// <summary>
    /// EX/超必殺の共通インフラ（器）
    /// </summary>
    private IEnumerator ExecuteEXInfrastructureRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        // 発動中の移動速度制限（タメ硬直）をインスペクターの値から適用
        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        PlaySkillSE(s.sePath);

        try
        {
            // 領域展開（強化ステート）中かどうかのフラグ（将来用）
            bool isZoneActive = false;

            // 🌟 キャラクターや設定アセットの patternType に応じて完全に美しく分岐
            switch (s.patternType)
            {
                // 【キャラA専用EX】陰陽宝玉・公転慣性ホーミング（旧ボム挙動）
                case SkillPatternType.Custom:
                    yield return StartCoroutine(CharA_SealOrbEXPattern(s, myHH, isZoneActive));
                    break;

                // 🌟【新規追加：キャラB専用EX】時間停止ナイフ・一回転ロックオン超高速直線突撃！
                // ※ インスペクター（PlayerSkillData）側で、キャラBのEXの patternType を「Line」などに設定してください
                case SkillPatternType.Line:
                case SkillPatternType.GreedTaxPossession: // 必要に応じて空いているスロットへマッピング
                    yield return StartCoroutine(CharB_KnifeEXPattern(s, myHH, isZoneActive));
                    break;

                case SkillPatternType.Standard:
                    yield return StartCoroutine(CharA_SealOrbEXPattern(s, myHH, isZoneActive));
                    break;

                default:
                    Debug.LogWarning($"[FireEX] 未実装のEXパターンタイプです: {s.patternType}");
                    break;
            }
        }
        finally
        {
            // 被弾早期脱出時も100%安全にリセット
            if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
        }
    }
    /// <summary>
    /// 【キャラA専用EX】陰陽オーブ公転・ホーミング追従アサルト（公転終了時に自機の硬直速度制限を最速解除する最適化版）
    /// </summary>
    private IEnumerator CharA_SealOrbEXPattern(PlayerSkillData.SkillSettings s, PlayerHitHandler myHH, bool isZoneActive)
    {
        int totalOrbs = s.count > 0 ? s.count : 6;
        if (isZoneActive) totalOrbs = Mathf.RoundToInt(totalOrbs * 1.5f);

        List<ExOrbTrackData> activeOrbs = new List<ExOrbTrackData>();
        float baseAngleStep = 360f / totalOrbs;

        // --- パラメータ定義（旧SealOrb.csの定数を完全再現） ---
        const float CONST_SPREAD_SPEED = 0.02f;
        const float CONST_ROTATION_SPEED = 4f;

        float enemyHomingSpeed = isZoneActive ? s.speed * 1.0f : s.speed;
        float playerReturnSpeed = enemyHomingSpeed * 0.8f;
        SEManager.Instance.Play(SEPath.SLASH, 0.3f);
        SEManager.Instance.Play(SEPath.LASER7, 0.3f);

        // =========================================================================
        // --- 段階1：オーブを一斉に実体化（クッキリ光る加算合成・赤維持） ---
        // =========================================================================
        for (int i = 0; i < totalOrbs; i++)
        {
            float startAngle = s.angleOffset + (baseAngleStep * i);
            GameObject bulletObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);

            SpriteRenderer orbSR = bulletObj.GetComponent<SpriteRenderer>();
            if (orbSR == null) orbSR = bulletObj.GetComponentInChildren<SpriteRenderer>();

            if (orbSR != null && s.bulletData != null)
            {
                if (s.bulletData.bulletSprite != null) orbSR.sprite = s.bulletData.bulletSprite;

                Color baseColor = orbSR.color;
                baseColor.a = 1.0f;

                if (s.bulletData.material != null)
                {
                    orbSR.material = s.bulletData.material;
                }
                else
                {
                    Shader additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
                    if (additiveShader != null)
                    {
                        orbSR.material = new Material(additiveShader);
                    }
                }
            }

            var myStatus = GetComponentInParent<PlayerStatusManager>();
            int ownerId = (myStatus != null) ? myStatus.playerId : 1;
            string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
            int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
            bulletObj.tag = assignedTag;
            bulletObj.layer = assignedLayer;
            SetLayerRecursive(bulletObj, assignedLayer);

            activeOrbs.Add(new ExOrbTrackData
            {
                tx = bulletObj.transform,
                angle = startAngle,
                radius = 0.2f,
                currentSpeed = 0f
            });

            if (i % 2 == 0) PlaySkillSE(s.sePath);
            yield return new WaitForFixedUpdate();
        }

        // =========================================================================
        // --- 段階2：自機の周りを公転しながらじわじわと外に広がる（60フレーム） ---
        // =========================================================================
        int orbitDurationFrames = 60;

        for (int f = 0; f < orbitDurationFrames; f++)
        {
            Vector3 playerPos = transform.position;
            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                var orb = activeOrbs[i];
                if (orb.tx == null) { activeOrbs.RemoveAt(i); continue; }

                Vector3 posBefore = orb.tx.position;

                orb.angle += CONST_ROTATION_SPEED;
                orb.radius += CONST_SPREAD_SPEED;

                float rad = orb.angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orb.radius;
                orb.tx.position = playerPos + offset;

                if (Time.fixedDeltaTime > 0)
                {
                    orb.currentSpeed = (orb.tx.position - posBefore).magnitude / Time.fixedDeltaTime;
                }

                orb.tx.rotation = Quaternion.Euler(0, 0, orb.angle + 90f);
            }

            yield return new WaitForFixedUpdate();
        }

        // =========================================================================
        // 🌟 修正の核心：公転展開フェーズが終了した瞬間に、自機の移動速度倍率を最速解放！！
        // =========================================================================
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        if (myMove != null)
        {
            // インfra層の終了を待たず、この瞬間に1.0倍（等速通常巡航スピード）に強制復旧！
            myMove.skillSpeedMultiplier = 1.0f;
            Debug.Log("<color=green>⚡【EXインフラ最適化】公転フェーズ終了。自機のタメ硬直を即座に全面解除しました！</color>");
        }

        // =========================================================================
        // --- 段階3：【完全再現】回転の慣性ベクトルを維持したまま、滑らかに追尾へ遷移 ---
        // =========================================================================
        Debug.Log("<color=cyan>【EXスキル】ロックオン完了！ 公転ベクトルを引き継いで滑らかな追尾を開始します！</color>");

        for (int i = 0; i < activeOrbs.Count; i++)
        {
            var orb = activeOrbs[i];
            if (orb.tx == null) continue;

            DanmakuBullet bullet = orb.tx.GetComponent<DanmakuBullet>();
            if (bullet == null) bullet = orb.tx.gameObject.AddComponent<DanmakuBullet>();

            float currentHomingAngle = GetAngleToTarget(orb.tx.position) + s.angleOffset;

            bullet.Initialize(_rootOwner, targetTag, enemyHomingSpeed, currentHomingAngle, 0f, enemyHomingSpeed, 0f, 0f, s.bulletData, true);
            bullet.isMovementSuspended = true;

            orb.angle += 90f;
        }

        PlaySkillSE(s.sePath);

        float homingTimer = 0;
        float maxHomingTime = 180f;

        while (homingTimer < maxHomingTime && activeOrbs.Count > 0)
        {
            if (!PlayerMove.CanShoot) yield break;

            float dt = Time.fixedDeltaTime;

            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                var orb = activeOrbs[i];
                if (orb.tx == null) { activeOrbs.RemoveAt(i); continue; }

                Vector3 destination;
                float homingDamp;
                float targetSpeed;

                PlayerMove targetPlayer = null;
                foreach (var p in PlayerMove.AllPlayers)
                {
                    if (p != null && p.gameObject != _rootOwner)
                    {
                        targetPlayer = p;
                        break;
                    }
                }

                if (targetPlayer != null)
                {
                    destination = targetPlayer.transform.position;
                    homingDamp = 24f;
                    targetSpeed = enemyHomingSpeed;
                }
                else
                {
                    destination = _rootOwner != null ? _rootOwner.transform.position : transform.position;
                    homingDamp = 12f;
                    targetSpeed = playerReturnSpeed;
                }

                Vector3 diff = destination - orb.tx.position;
                float targetAngleRad = Mathf.Atan2(diff.y, diff.x);
                float judgangle = Mathf.Sin(targetAngleRad - (orb.angle * Mathf.Deg2Rad));

                if (Mathf.Abs(judgangle) > 0.05f)
                    orb.angle += Mathf.Asin(judgangle) * Mathf.Rad2Deg / homingDamp;
                else
                    orb.angle = targetAngleRad * Mathf.Rad2Deg;

                orb.currentSpeed = Mathf.Lerp(orb.currentSpeed, targetSpeed, 0.15f);

                orb.tx.rotation = Quaternion.Euler(0, 0, orb.angle);
                float rad = orb.angle * Mathf.Deg2Rad;
                orb.tx.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orb.currentSpeed * dt;
            }

            homingTimer++;
            yield return new WaitForFixedUpdate();
        }

        // =========================================================================
        // --- 段階4：時間切れ慣性直進フェーズ ---
        // =========================================================================
        Debug.Log("<color=gray>[EXスキル] 追尾時間が終了しました。残存オーブを慣性直進へ解放します。</color>");

        while (activeOrbs.Count > 0)
        {
            if (!PlayerMove.CanShoot) yield break;

            float dt = Time.fixedDeltaTime;

            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                var orb = activeOrbs[i];
                if (orb.tx == null) { activeOrbs.RemoveAt(i); continue; }

                float rad = orb.angle * Mathf.Deg2Rad;
                orb.tx.position += new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * orb.currentSpeed * dt;

                if (Mathf.Abs(orb.tx.position.x) > 10.0f || Mathf.Abs(orb.tx.position.y) > 10.0f)
                {
                    DanmakuBullet bullet = orb.tx.GetComponent<DanmakuBullet>();
                    if (bullet != null)
                    {
                        bullet.isMovementSuspended = false;
                        bullet.Deactivate(false);
                    }
                    else
                    {
                        Destroy(orb.tx.gameObject);
                    }
                    activeOrbs.RemoveAt(i);
                }
            }

            yield return new WaitForFixedUpdate();
        }
        activeOrbs.Clear();
    }
    private IEnumerator CharB_KnifeEXPattern(PlayerSkillData.SkillSettings s, PlayerHitHandler myHH, bool isZoneActive)
    {
        // -------------------------------------------------------------------------
        // 🌟 パラメータ設定（ここを変えるだけで弾数や回転数を自由に変更可能）
        // -------------------------------------------------------------------------
        int totalWaves = 15;          // 連射するサイクル数（対角2Wayなので計60発）
        float angleIncrement = 16f;    // 1ウェーブごとの発射角の傾き（渦巻きの密度）
        float stopDelayTime = 0.5f;   // 💡発射されてから空間停止するまでの時間（0.5秒）

        float initialSpeed = 5f;      // 弾幕データ初期速度
        float dashSpeed = isZoneActive ? (initialSpeed * 1.5f) * 1.3f : initialSpeed * 3.5f; // 突撃時は1.5倍速
        // 🌟 追加：現在の発射主の PlayerMove から、共通インフラが設定してくれた EX移動倍率をスタック
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        float currentAngle = UnityEngine.Random.Range(1f, 360f);
        SEManager.Instance.Play(SEPath.SLASH, 0.3f);
        SEManager.Instance.Play(SEPath.LASER7, 0.3f);
        Debug.Log("<color=pink>【EXスキル】キャラB：インフラ活用型・対角2Wayナイフアサルトを開始</color>");

        // =========================================================================
        // --- 段階1：対角2Wayで射出（DanmakuBulletの標準直進をそのまま利用） ---
        // =========================================================================
        for (int i = 0; i < totalWaves; i++)
        {
            //if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) yield break;
            // 💡 リアルタイム減速：連射のループ中、常にインスペクターから指定された低速倍率（例: 0.2倍）を上書き固定ホールド
            if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
            for (int j = 0; j < 2; j++)
            {
                float finalAngle = currentAngle + (j * 180f);
                Vector3 spawnPos = transform.position;

                GameObject bulletObj = Instantiate(s.bulletData.bulletPrefab, spawnPos, Quaternion.identity);

                // チームの所属タグ・レイヤーの動的自動結合
                var myStatus = GetComponentInParent<PlayerStatusManager>();
                int ownerId = (myStatus != null) ? myStatus.playerId : 1;
                string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
                int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
                bulletObj.tag = assignedTag;
                bulletObj.layer = assignedLayer;
                SetLayerRecursive(bulletObj, assignedLayer);

                DanmakuBullet bullet = bulletObj.GetComponent<DanmakuBullet>();
                if (bullet == null) bullet = bulletObj.AddComponent<DanmakuBullet>();

                // 🌟 核心：isMovementSuspended を使わず、普通に初期物理を直進させます！
                // 🌟 これにより、生まれた瞬間から initialSpeed で綺麗に前進を始めます。
                bullet.isMovementSuspended = false;
                bullet.Initialize(_rootOwner, targetTag, initialSpeed, finalAngle, 0f, initialSpeed, 0f, 0f, s.bulletData, false);

                // 【画像そのまま】アセット指定のビジュアルを完全維持
                SpriteRenderer knifeSR = bulletObj.GetComponent<SpriteRenderer>();
                if (knifeSR == null) knifeSR = bulletObj.GetComponentInChildren<SpriteRenderer>();
                if (knifeSR != null && s.bulletData != null)
                {
                    if (s.bulletData.bulletSprite != null) knifeSR.sprite = s.bulletData.bulletSprite;
                    if (s.bulletData.material != null) knifeSR.material = s.bulletData.material;
                }

                // 🌟【非同期タイマーコルーチンを弾単体に個別に持たせる】
                // これにより、このEmitter側のメインループは2フレーム間隔の連射だけに100%集中できます！
                StartCoroutine(IndividualKnifeRoutine(bullet, bulletObj.GetComponent<CircleCollider2D>(), finalAngle, stopDelayTime, dashSpeed, s.bulletData));
            }

            currentAngle += angleIncrement;
            PlaySkillSE(s.sePath);

            // 正確に2フレーム待機して次の2Wayを放つ
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        yield return null;
    }

    /// <summary>
    /// 💡 新設：生まれた1発1発のナイフが、自分自身の時間軸で勝手に「0.5秒進んで止まる➔回る➔突撃」を行う独立AIルーチン
    /// </summary>
    private IEnumerator IndividualKnifeRoutine(DanmakuBullet bullet, CircleCollider2D col, float spawnAngle, float delay, float speed2, BulletData data)
    {
        // 1. 【発射➔0.5秒待機フェーズ】
        // 弾は内部のFixedUpdateで勝手に初速5で前に進んでいるので、ここではただ0.5秒待つだけ！
        yield return new WaitForSeconds(delay);
        if (bullet == null) yield break;

        // 2. 【0.5秒後にその場でピタッと停止 ➔ 当たり判定OFF】
        // 移動処理のみをサスペンド(ON)に切り替えて自動前進をロックし、判定を消す
        bullet.isMovementSuspended = true;
        if (col != null) col.enabled = false;

        // 3. 【その場で360度カチカチと回転スピン】
        // 過去コードの「for (int i = 0; i < 360; i += 12)」を完全再現
        for (int rotationDelta = 0; rotationDelta < 360; rotationDelta += 12)
        {
            // 一時停止や時間停止を考慮した、ゲーム全体のFixedUpdateフレーム同期
            yield return new WaitForFixedUpdate();
            if (bullet == null) yield break;

            float currentRotAngle = spawnAngle + rotationDelta;
            bullet.transform.rotation = Quaternion.Euler(0, 0, currentRotAngle - 90f);
        }

        // 4. 【ロックオン ➔ 判定復旧 ➔ 1.5倍速一直線突撃】
        // 最も近い敵を探索
        float finalAimAngle = spawnAngle;
        Transform nearestEnemy = null;
        float minDistance = Mathf.Infinity;

        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != _rootOwner)
            {
                float dist = Vector3.Distance(bullet.transform.position, p.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestEnemy = p.transform;
                }
            }
        }

        if (nearestEnemy != null)
        {
            Vector3 dir = nearestEnemy.position - bullet.transform.position;
            finalAimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        // 刃先をターゲットへ向け、判定をONに戻す
        bullet.transform.rotation = Quaternion.Euler(0, 0, finalAimAngle - 90f);
        if (col != null) col.enabled = true;

        // 🌟 物理の完全解放：サスペンドを解除し、新しい「突撃角度」と「1.5倍の超高速」で直進物理をリスタート！
        bullet.isMovementSuspended = false;
        bullet.Initialize(_rootOwner, targetTag, speed2, finalAimAngle, 0f, speed2, 0f, 0f, data, false);


    }

    // 拡張した内部データ管理クラス
    private class ExOrbTrackData
    {
        public Transform tx;
        public float angle;
        public float radius;
        public float currentSpeed; // 慣性等速ホーミング用の速度スタック
    }

    private void PlaySkillSE(string path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
            SEManager.Instance.Play(clip, 0.4f);
    }
}