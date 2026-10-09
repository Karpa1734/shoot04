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
            PlaySkillSE(SEPath.POWER_LOGO2);
            if (BossEffectManager.Instance != null && _rootOwner != null)
            {
                BossEffectManager.Instance.PlayChargeEffect(0.1f, s.bulletData.breakColor, _rootOwner.transform.position);
            }
            float elapsedFrames = 0f;
            bool isKeyReleased = false;

            int tier1Frames = 25;
            int tier2Frames = 55;
            int currentTier = 1;
            int previousTier = 1;

            // 🌟【修正の核心】：AI（DanmakuAgent）からのフレーム入力、または人間の入力が離されるまで自由にチャージ時間を可変させる
            while (!isKeyReleased)
            {
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                {
                    isKeyReleased = true;
                    break;
                }

                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = chargeMoveSpeed;

                int intElapsed = Mathf.FloorToInt(elapsedFrames);
                if (intElapsed < tier1Frames) currentTier = 1;
                else if (intElapsed < tier2Frames) currentTier = 2;
                else currentTier = 3;

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

                float chargeSpeedMultiplier = (myStatus != null && myStatus.isSpellCardActive) ? 1.5f : 1.0f;
                elapsedFrames += 1f * chargeSpeedMultiplier;

                DanmakuAgent agent = GetComponentInChildren<DanmakuAgent>();
                if (agent == null) agent = GetComponentInParent<DanmakuAgent>();

                if (agent != null && agent._useAutoEvadeAI)
                {
                    // 🤖 AI操作の場合：DanmakuAgent側が決定したランダムなチャージフレーム（_aiDynamicChargeTarget）に到達したら自動でキーを離す！
                    if (elapsedFrames >= agent._aiDynamicChargeTarget)
                    {
                        isKeyReleased = true;
                    }
                }
                else
                {
                    // 人間プレイヤーの場合：キーが離されたら発射
                    if (zAction != null && !zAction.IsPressed()) isKeyReleased = true;
                    else if (zAction == null && !Input.anyKey) isKeyReleased = true;
                    else if (elapsedFrames >= 90f) isKeyReleased = true;
                }
            }

            if (pAnim != null)
            {
                Animator anim = pAnim.GetComponentInChildren<Animator>();
                if (anim != null) anim.SetTrigger("SkillZ2");
            }

            PlaySkillSE(SEPath.SHOT2);

            float fixedBaseAngle = GetAngleToTarget(transform.position) + s.angleOffset;
            float baseSpeed = s.speed > 0f ? s.speed : 5f;
            float wide = s.wideAngle > 0f ? s.wideAngle : 10f;

            if (currentTier == 1)
            {
                FireNWay(s.bulletData, fixedBaseAngle, wide, 3, baseSpeed, s.delay);
            }
            else if (currentTier == 2)
            {
                int[] waysTier2 = new int[] { 2, 3, 4 };
                float[] speedsTier2 = new float[] { baseSpeed * 1.3f, baseSpeed * 1.2f, baseSpeed * 1.1f };
                FireSimultaneousSpeedWaves(s.bulletData, fixedBaseAngle, wide, waysTier2, speedsTier2, s.delay, 1.2f);
            }
            else
            {
                int[] waysTier3 = new int[] { 2, 3, 4, 5, 6, 7 };
                float[] speedsTier3 = new float[] { baseSpeed * 1.6f, baseSpeed * 1.4f, baseSpeed * 1.2f, baseSpeed * 1.0f, baseSpeed * 0.85f, baseSpeed * 0.7f };
                FireSimultaneousSpeedWaves(s.bulletData, fixedBaseAngle, wide, waysTier3, speedsTier3, s.delay, 1.8f);
            }

            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _isSlothCharging = false;

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

    // 🔄 前回の回転方向を保持するフラグ（使用ごとに±が交互に入れ替わります）
    private bool _isSlothXReversed = false;


    protected IEnumerator SkillTempleteX(PlayerSkillData.SkillSettings s)
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

        // 🌟【修正の核心】：VJT（スペルカード）展開中なら6way、通常時は4wayに切り替える
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        int laserWay = isSpellActive ? 6 : 4;
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

                // 1本あたりの基本配置角度（6wayなら60度刻み、4wayなら90度刻みに自動分配）
                float slotBaseAngle = targetBaseAngle + (360f / laserCount * i);

                // 🎯 6wayの密度と広がり方に合わせて、開始時のずらし位置と変化量を調整
                // 6wayのときは本数が多く密集するため、開始オフセットと回転量を少し広めにダイナミックに設定します
                float initialDistAngleOffset = (isSpellActive ? 40f : 60f) * rotDir;
                float startDistAngle = slotBaseAngle + initialDistAngleOffset;

                // 🎯 1秒かけて目標角度へ公転させるための変化量
                float targetDistAngleOffset = (isSpellActive ? -10f : -10f) * rotDir;
                float totalDeltaDistAngle = targetDistAngleOffset - initialDistAngleOffset;
                float frameDistAngleVel = totalDeltaDistAngle / rotateDurationFrames;

                float initialLaserAngle = startDistAngle;
                float frameLaserAngleVel = frameDistAngleVel; // 弾源と同じ速度でレーザーの向きも連動させる

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

                // データ2：回転開始（フレーム5）
                laser.AddData(new EnemyLaserBeam.LaserTransformData
                {
                    frame = warningFrame,
                    distAngleVel = frameDistAngleVel,
                    laserAngleVel = frameLaserAngleVel,
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

                float duration = 8.0f;
                buffField.Initialize(_rootOwner, duration);
            }

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

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponentInChildren<PlayerStatusManager>();

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

            int ownerId = (myStatus != null) ? myStatus.playerId : 1;
            domainObj.tag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";

            // 🌟 スペル中（VJT展開中）かどうかの判定
            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

            float baseDuration = 4.0f;
            // スペル中であれば持続時間が1.5倍に拡張されます（SlothUltDomain側でも同期されます）
            float duration = isSpellActive ? baseDuration * 1.5f : baseDuration;

            domainLogic.Initialize(_rootOwner, targetTag, 1, linkedCircles, baseDuration);

            // 領域が持続している間待機
            yield return new WaitForSeconds(duration);
        }
        finally
        {
            // 1. 場に出ている魔方陣をすべて消滅させる
            List<SlothMagicCircle> circlesToDestroy = new List<SlothMagicCircle>(SlothMagicCircle.AllCircles);
            foreach (var circle in circlesToDestroy)
            {
                if (circle != null)
                {
                    circle.StartDestroyRoutine();
                }
            }
            SlothMagicCircle.AllCircles.Clear();

            _isEXSkillActive = false;
            _activeSkillCoroutines--;

            // 🌟【修正の核心】：スペル中にULTを使用した場合、技の終了時（finally）に聖少女領域（VJT）を強制終了（解除）する
            if (myStatus != null && myStatus.isSpellCardActive)
            {
                myStatus.DeactivateSpellCard(false);
                Debug.Log("<color=cyan>✨ [SLOTH ULT] スペル中のULT使用に伴い、聖少女領域（VJT）を強制終了しました。</color>");
            }
        }
    }
}