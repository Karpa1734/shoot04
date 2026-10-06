using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Emitter_Sloth : PlayerDanmakuEmitter
{
    private bool _isSlothCharging = false;

    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteSlothChargeShotRoutine(s));
    }

    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(SkillTempleteX(s));
    }

    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(SkillTempleteC(s));
    }

    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(SkillTempleteV(s));
    }

    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(SkillTempleteEX(s));
    }

    /// <summary>
    /// 💤 3段階チャージ式・インジケーター非表示 ＆ 発射時ターゲット捕捉 ＆ 段階的効果音つき弾幕ルーチン
    /// </summary>
    private IEnumerator ExecuteSlothChargeShotRoutine(PlayerSkillData.SkillSettings s)
    {
        if (_isSlothCharging) yield break;
        _isSlothCharging = true;

        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();

        float chargeMoveSpeed = (s.moveSpeedMultiplier > 0f) ? s.moveSpeedMultiplier : 0.4f;
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = chargeMoveSpeed;

        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();
        if (pAnim != null)
        {
            Animator anim = pAnim.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.ResetTrigger("SkillZ2");
                anim.SetTrigger("SkillZ");
            }
            else
            {
                pAnim.TriggerSkillAnimation("SkillZ");
            }
        }

        UnityEngine.InputSystem.InputAction zAction = null;
        if (InputManager.Instance != null && myStatus != null)
        {
            var inputSet = (myStatus.playerId == 2) ? InputManager.Instance.player2 : InputManager.Instance.player1;
            if (inputSet.skillZ != null) zAction = inputSet.skillZ.action;
        }

        try
        {
            // チャージ開始音
            PlaySkillSE(SEPath.POWER_LOGO2);
            if (BossEffectManager.Instance != null && _rootOwner != null)
            {
                BossEffectManager.Instance.PlayChargeEffect(0.1f, s.bulletData.breakColor, _rootOwner.transform.position);
            }
            int elapsedFrames = 0;
            bool isKeyReleased = false;

            int tier1Frames = 25;
            int tier2Frames = 55;
            int maxChargeFrames = 90;
            int currentTier = 1;
            int previousTier = 1; // 💡 チャージ音の切り替わり検知用

            while (!isKeyReleased)
            {
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                {
                    isKeyReleased = true;
                    break;
                }

                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = chargeMoveSpeed;

                // チャージ段階の判定
                if (elapsedFrames < tier1Frames) currentTier = 1;
                else if (elapsedFrames < tier2Frames) currentTier = 2;
                else currentTier = 3;

                // 🌟【追加】：チャージ段階（Tier）が上がった瞬間に効果音を鳴らす
                if (currentTier > previousTier)
                {
                    if (BossEffectManager.Instance != null && _rootOwner != null)
                    {
                        BossEffectManager.Instance.PlayChargeEffect(0.1f, s.bulletData.breakColor, _rootOwner.transform.position);
                    }
                    PlaySkillSE(SEPath.FREEZE10);
                    previousTier = currentTier;
                }

                yield return new WaitForFixedUpdate();
                elapsedFrames++;

                DanmakuAgent agent = GetComponentInChildren<DanmakuAgent>();
                if (agent == null) agent = GetComponentInParent<DanmakuAgent>();

                if (agent != null && agent._useAutoEvadeAI)
                {
                    if (elapsedFrames >= tier2Frames) isKeyReleased = true;
                }
                else
                {
                    if (zAction != null && !zAction.IsPressed()) isKeyReleased = true;
                    else if (zAction == null && !Input.anyKey) isKeyReleased = true;
                    else if (elapsedFrames >= maxChargeFrames) isKeyReleased = true;
                }
            }

            if (pAnim != null)
            {
                Animator anim = pAnim.GetComponentInChildren<Animator>();
                if (anim != null) anim.SetTrigger("SkillZ2");
            }

            PlaySkillSE(SEPath.SHOT2);

            // 🌟【修正】：チャージ中ではなく、ボタンを離して発射した「この瞬間」の敵機方向を新しく取得する
            float fixedBaseAngle = GetAngleToTarget(transform.position) + s.angleOffset;

            float baseSpeed = s.speed > 0f ? s.speed : 5f;
            float wide = s.wideAngle > 0f ? s.wideAngle : 10f;

            // =========================================================================
            // 🚀 チャージ段階に応じた「同時射出・速度差」パターン
            // =========================================================================
            if (currentTier == 1)
            {
                // 段階1：普通の3way
                FireNWay(s.bulletData, fixedBaseAngle, wide, 3, baseSpeed, s.delay);
            }
            else if (currentTier == 2)
            {
                // 段階2：広がり係数を `1.2f` に指定して発射
                int[] waysTier2 = new int[] { 2, 3, 4 };
                float[] speedsTier2 = new float[] { baseSpeed * 1.3f, baseSpeed * 1.2f, baseSpeed * 1.1f };
                FireSimultaneousSpeedWaves(s.bulletData, fixedBaseAngle, wide, waysTier2, speedsTier2, s.delay, 1.2f);
            }
            else
            {
                // 段階3：最大の広がり（1.8f）で 2way から 7way までの最大ウェーブ
                int[] waysTier3 = new int[] { 2, 3, 4, 5, 6, 7 };
                float[] speedsTier3 = new float[] { baseSpeed * 1.6f, baseSpeed * 1.4f, baseSpeed * 1.2f, baseSpeed * 1.0f, baseSpeed * 0.85f, baseSpeed * 0.7f };
                FireSimultaneousSpeedWaves(s.bulletData, fixedBaseAngle, wide, waysTier3, speedsTier3, s.delay, 1.8f);
            }

            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;

            _isSlothCharging = false;
            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
        }
    }

    /// <summary>
    /// 🌊 先端に行くにつれて広がるレンジの最大値を引数（maxSpreadMultiplier）で切り替えられるようにしたヘルパー
    /// </summary>
    private void FireSimultaneousSpeedWaves(BulletData data, float centerAngle, float baseSpread, int[] waySequence, float[] speedSequence, float delay, float maxSpreadMultiplier)
    {
        if (data == null || waySequence == null || speedSequence == null) return;
        Vector3 pos = transform.position;

        for (int c = 0; c < waySequence.Length; c++)
        {
            int wayCount = waySequence[c];
            float shootSpeed = (c < speedSequence.Length) ? speedSequence[c] : speedSequence[speedSequence.Length - 1];

            float t = (float)c / Mathf.Max(1, waySequence.Length - 1);
            // 💡 引数で受け取った maxSpreadMultiplier を適用
            float currentSpread = Mathf.Lerp(baseSpread * 0.3f, baseSpread * maxSpreadMultiplier, t);

            if (wayCount <= 1)
            {
                CreateShot(data, pos, shootSpeed, centerAngle, delay);
            }
            else
            {
                float startAngle = centerAngle - (currentSpread / 2f);
                float stepAngle = currentSpread / (wayCount - 1);

                for (int i = 0; i < wayCount; i++)
                {
                    float finalAngle = startAngle + (stepAngle * i);
                    CreateShot(data, pos, shootSpeed, finalAngle, delay);
                }
            }
        }
    }
    /// <summary>
    /// 指定したway数とスピードで扇状の弾群を生成するヘルパー
    /// </summary>
    private void FireNWay(BulletData data, float centerAngle, float spread, int wayCount, float shootSpeed, float delay)
    {
        if (data == null) return;
        Vector3 pos = transform.position;

        if (wayCount <= 1)
        {
            CreateShot(data, pos, shootSpeed, centerAngle, delay);
        }
        else
        {
            float startAngle = centerAngle - (spread / 2f);
            float stepAngle = spread / (wayCount - 1);

            for (int i = 0; i < wayCount; i++)
            {
                float finalAngle = startAngle + (stepAngle * i);
                CreateShot(data, pos, shootSpeed, finalAngle, delay);
            }
        }
    }

    private void DrawFanMesh(MeshFilter filter, Mesh targetMesh, float spreadAngle, float centerAngle, float radius)
    {
        if (filter == null || targetMesh == null) return;
        targetMesh.Clear();

        int segments = 24;
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        float startAngle = centerAngle - (spreadAngle / 2f);
        float stepAngle = spreadAngle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float rad = (startAngle + (stepAngle * i)) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        }

        for (int i = 0; i < segments; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }

        targetMesh.vertices = vertices;
        targetMesh.triangles = triangles;
        targetMesh.RecalculateNormals();
        filter.mesh = targetMesh;
    }

    // 🔄 前回の回転方向を保持するフラグ（使用ごとに±が交互に入れ替わります）
    private bool _isSlothXReversed = false;

    protected  IEnumerator SkillTempleteX(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        if (BulletManager.Instance == null) { _activeSkillCoroutines--; yield break; }

        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();
        if (pAnim != null)
        {
            Animator anim = pAnim.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("SkillZ");
            }
        }

        int laserWay = 4;
        int laserCount = Mathf.Max(1, laserWay);
        float radius = 1.2f; // 自機から離した弾源の距離

        int warningFrame = 5;
        int rotateDurationFrames = 60; // 持続時間 1秒（60フレーム）
        int totalActiveFrames = warningFrame + rotateDurationFrames;

        _isSlothXReversed = !_isSlothXReversed;
        float rotDir = _isSlothXReversed ? -1.0f : 1.0f;

        // 🎯 敵機位置への基準角度を取得
        float targetBaseAngle = GetAngleToTarget(transform.position);

        PlaySkillSE(s.sePath);
        List<EnemyLaserBeam> spawnedLasers = new List<EnemyLaserBeam>();

        for (int i = 0; i < laserCount; i++)
        {
            EnemyLaserBeam laser = CreateLaserShot(s.bulletData, transform.position, s.speed, s.count, s.wideAngle, warningFrame, isSetupB: true);

            if (laser != null)
            {
                spawnedLasers.Add(laser);

                // 1本あたりの基本配置角度
                float slotBaseAngle = targetBaseAngle + (360f / laserCount * i);

                // 🎯 弾源（公転位置）を ±60度 ずらした位置からスタートさせる
                float initialDistAngleOffset = 60f * rotDir;
                float startDistAngle = slotBaseAngle + initialDistAngleOffset;

                // 🎯 1秒かけて ±1度 の位置まで弾源を公転させるための変化量
                float targetDistAngleOffset = -10f * rotDir;
                float totalDeltaDistAngle = targetDistAngleOffset - initialDistAngleOffset;
                float frameDistAngleVel = totalDeltaDistAngle / rotateDurationFrames;

                // 💡【修正の核心】：レーザーの向き（laserAngle）を「弾源の位置（distAngle）」と完全に一致させます。
                // EnemyLaserBeamの描画上、distAngle と laserAngle を同じにすることで、光源から外側へ真っ直ぐビームが伸びるようになります。
                float initialLaserAngle = startDistAngle;
                float frameLaserAngleVel = frameDistAngleVel; // 弾源と同じ速度でレーザーの向きも回転させる

                // データ1：予告線期間（フレーム0 ～ 5）
                laser.AddData(new EnemyLaserBeam.LaserTransformData
                {
                    frame = 0,
                    dist = radius,
                    distAngle = startDistAngle,
                    laserAngle = initialLaserAngle,
                    distAngleVel = 0f,
                    laserAngleVel = 0f,
                    isSmooth = false
                });

                // データ2：回転開始（フレーム5）：弾源とレーザーの向きを同じ速度（frameDistAngleVel）で一緒に±5度へ公転・回転させる
                laser.AddData(new EnemyLaserBeam.LaserTransformData
                {
                    frame = warningFrame,
                    distAngleVel = frameDistAngleVel,
                    laserAngleVel = frameLaserAngleVel, // 👈 弾源の移動に合わせてレーザーの向きも連動させる
                    isSmooth = true
                });

                // データ3：目標角度到達（フレーム 65）：完全に静止
                laser.AddData(new EnemyLaserBeam.LaserTransformData
                {
                    frame = totalActiveFrames,
                    distAngleVel = 0f,
                    laserAngleVel = 0f,
                    isSmooth = true
                });

                laser.Fire();
            }
        }

        yield return new WaitForSeconds(totalActiveFrames / 60f);

        foreach (var laser in spawnedLasers)
        {
            if (laser != null) laser.ForceClose();
        }

        _activeSkillCoroutines--;
    }

    protected IEnumerator SkillTempleteC(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();
        if (pAnim != null)
        {
            pAnim.TriggerSkillAnimation(s.skillName);
        }

        PlaySkillSE(s.sePath);

        if (s.bulletData != null && s.bulletData.bulletPrefab != null)
        {
            Vector3 spawnPos = transform.position;
            GameObject circleObj = Instantiate(s.bulletData.bulletPrefab, spawnPos, Quaternion.identity);

            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            int ownerId = (myStatus != null) ? myStatus.playerId : 1;
            string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
            int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");

            circleObj.tag = assignedTag;
            circleObj.layer = assignedLayer;
            SetLayerRecursive(circleObj, assignedLayer);

            SlothMagicCircle circleLogic = circleObj.GetComponent<SlothMagicCircle>();
            if (circleLogic == null) circleLogic = circleObj.AddComponent<SlothMagicCircle>();

            float fireInterval = s.speed > 0f ? s.speed : 1.2f;
            circleLogic.Initialize(_rootOwner, s.bulletData, targetTag, fireInterval);
        }

        yield return new WaitForSeconds(s.cooldown);

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
        _activeSkillCoroutines--;
    }

    private IEnumerator SkillTempleteV(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        try
        {
            PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
            if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();
            if (pAnim != null)
            {
                pAnim.TriggerSkillAnimation(s.skillName);
            }

            PlaySkillSE(s.sePath);

            if (s.bulletData != null && s.bulletData.bulletPrefab != null)
            {
                Vector3 spawnPos = transform.position;
                GameObject fieldObj = Instantiate(s.bulletData.bulletPrefab, spawnPos, Quaternion.identity);

                PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
                int ownerId = (myStatus != null) ? myStatus.playerId : 1;
                string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
                int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");

                fieldObj.tag = assignedTag;
                fieldObj.layer = assignedLayer;
                SetLayerRecursive(fieldObj, assignedLayer);

                SlothBuffField buffField = fieldObj.GetComponent<SlothBuffField>();
                if (buffField == null) buffField = fieldObj.AddComponent<SlothBuffField>();

                float duration = 10.0f;
                buffField.Initialize(_rootOwner, duration);
            }

            // 💡【修正の核心】：
            // 他の弾幕系スキルと違い、Vスキルは魔方陣を「設置して終わり」のフィールド技です。
            // s.cooldown（硬直時間）の間ずっとエミッターを占有してしまうと、SkillManager側で
            // 「スキル発動中（Active）」と判定され続け、マナの自然回復ロックが解除されなくなります。
            // そのため、生成モーションの最低限のウェイト（例: 0.1秒など）だけ挟み、速やかにコルーチンを解放します。
            yield return new WaitForSeconds(0.1f);
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
        }
    }

    protected IEnumerator SkillTempleteEX(PlayerSkillData.SkillSettings s)
    {
        _isEXSkillActive = true;
        _activeSkillCoroutines++;

        try
        {
            // 🌟【発動条件チェック】：魔方陣が規定数以上存在するか確認
            List<SlothMagicCircle> activeCircles = SlothMagicCircle.AllCircles;
            if (activeCircles == null || activeCircles.Count < 3)
            {
                Debug.Log("<color=yellow>⚠️ 魔方陣の数が足りないため、ULTを発動できません！</color>");
                yield break;
            }

            List<SlothMagicCircle> linkedCircles = new List<SlothMagicCircle>(activeCircles);

            PlaySkillSE(SEPath.SLASH);

            PlaySkillSE(SEPath.LASER7);
            // 🎬 モーションや演出の再生
            PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
            if (pAnim != null) pAnim.TriggerSkillAnimation(s.skillName);

            // 🌟 領域（Domain）オブジェクトの生成
            GameObject domainObj = new GameObject("SlothUltDomainArea");
            SlothUltDomain domainLogic = domainObj.AddComponent<SlothUltDomain>();

            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            int ownerId = (myStatus != null) ? myStatus.playerId : 1;
            domainObj.tag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";

            float duration = 3.0f;
            domainLogic.Initialize(_rootOwner, targetTag,1, linkedCircles, duration);

            // 領域が持続している間待機
            yield return new WaitForSeconds(duration);
        }
        finally
        {
            // =========================================================================
            // 🌟【追加】：ULT技が終了した際（正常終了・中断問わず）、場に出ている魔方陣をすべて消滅させる
            // =========================================================================
            // 💡 リストをコピーしてから逆順でループして安全に一斉削除・縮小消滅させる
            List<SlothMagicCircle> circlesToDestroy = new List<SlothMagicCircle>(SlothMagicCircle.AllCircles);
            foreach (var circle in circlesToDestroy)
            {
                if (circle != null)
                {
                    circle.StartDestroyRoutine(); // 縮小しながら綺麗に消滅させる
                }
            }
            SlothMagicCircle.AllCircles.Clear();

            _isEXSkillActive = false;
            _activeSkillCoroutines--;
        }
    }
}