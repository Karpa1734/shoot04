using KanKikuchi.AudioManager;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Emitter_Lust : PlayerDanmakuEmitter
{
    // ⚔️ カリンのZスキル＝「しの字」アーク一閃！
    // =========================================================================
    // ⚔️ 色欲専用Z：愛の射出型流体ストリームレーザー（インフラ調停版）
    // =========================================================================
    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteLustLaserStreamRoutine(s));
    }



    // ⚔️ カリンのXスキル＝空間一閃・双極ブレード！
    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteEnemyEnclosureShrinkingRingRoutine(s));
    }

    // ⚔️ カリンのCスキル＝ブーメラン設置！
    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteBouncingTrailShotRoutine(s));
    }

    // ⚔️ カリンのVスキル＝防御フィールドチャージ！
    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteLustSpearAssaultRoutine(s));
    }

    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        yield return null;
    }

    // =========================================================================
    // 🔮【Lust専用領域変調】：3-way 設置型ストリームレーザー射出エンジン
    // 💡【弾源固定化】：領域中の5way極太ビームも、発動瞬間の位置に弾源をホールドさせます。
    // =========================================================================
    private IEnumerator ExecuteLustLaserStreamRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        PlaySkillSE(s.sePath);
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        try
        {
            int totalBulletSegments = Mathf.RoundToInt(Mathf.Max(10, s.count) * 1.5f);
            float laserWidthScale = ((s.wideAngle > 0f) ? s.wideAngle : 1.0f) * 1.3f;

            int laserIntervalFrames = 1;
            int wayCount = 3;
            float spreadAngle = 24f;

            Vector3 spawnOrigin = transform.position; // 👈 ここで弾源をロック！
            Material additiveMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            additiveMaterial.hideFlags = HideFlags.DontSave;
            // 🎯 固定された起点（spawnOrigin）から、現在の敵機へのリアルタイム角度を抽出！
            float currentTargetAngle = GetAngleToTarget(spawnOrigin);
            float currentBaseAngle = currentTargetAngle + s.angleOffset;
            // =========================================================================
            // 🎯【Lust側核心修正】：発動した瞬間の座標を変数に固定ホールド
            // =========================================================================

            for (int f = 0; f < totalBulletSegments; f++)
            {
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                    break;



                float startAngle = currentBaseAngle - (spreadAngle / 2f);
                float stepAngle = spreadAngle / (wayCount - 1);

                for (int w = 0; w < wayCount; w++)
                {
                    float finalLaserAngle = startAngle + (stepAngle * w);

                    // 🛠️ ここも transform.position ➔ 「spawnOrigin」へ差し替え！
                    CreateShot(s.bulletData, spawnOrigin, s.speed, finalLaserAngle, delay: 0f,
                               isConverge: false, accel: 0f, maxSpeed: 0f,
                               customMaterial: additiveMaterial, customScale: laserWidthScale);
                }

                for (int i = 0; i < laserIntervalFrames; i++)
                {
                    yield return new WaitForFixedUpdate();
                }
            }

            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
        }
    }

    private List<DanmakuBullet> _activeCageBullets = new List<DanmakuBullet>();

    // =========================================================================
    // 🔶 色欲専用X：位置固定・聖少女檻クナイ・完全自律公転スピンアサルト
    // 💡【領域変調】：領域中なら、囲う檻の1つの輪ごとの密度（弾数）を正確に「2倍」へブースト！
    // =========================================================================
    private IEnumerator ExecuteEnemyEnclosureShrinkingRingRoutine(PlayerSkillData.SkillSettings s)
    {
        if (_activeCageBullets != null && _activeCageBullets.Count > 0) 
        {
            for (int b = _activeCageBullets.Count - 1; b >= 0; b--) 
            {
                DanmakuBullet oldBullet = _activeCageBullets[b]; 
                if (oldBullet != null && oldBullet.gameObject.activeSelf) 
                {
                    oldBullet.Deactivate(true); 
                }
            }
            _activeCageBullets.Clear(); 
        }

        _activeSkillCoroutines++; 

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>(); 
        PlayerMove myMove = GetComponentInParent<PlayerMove>(); 

        if (myMove != null && !_isEXSkillActive) 
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier; 
        }

        List<List<Tuple<Transform, float>>> layersBulletMatrix = new List<List<Tuple<Transform, float>>>(); 
        bool isGenerationFinished = false; 

        try
        {
            Transform targetEnemyTransform = null; 
            foreach (var p in PlayerMove.AllPlayers) 
            {
                if (p != null && p.gameObject != _rootOwner) 
                {
                    targetEnemyTransform = p.transform; 
                    break; 
                }
            }
            Vector3 enemyCenterPos = (targetEnemyTransform != null) ? targetEnemyTransform.position : Vector3.zero; 

            // 💡 領域展開（スペルカード）中かどうかのステートを安全取得
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

            // 🎯【密度2倍変調】：領域中なら s.count を2倍にブースト、通常時は設定値（最低4）を維持
            int baseWayCount = Mathf.Max(4, s.count); 
            int wayCount = isSpellActive ? (baseWayCount * 2) : baseWayCount;

            float[] layersRadius = new float[] { 2.0f, 2.5f, 3.0f, 3.5f, 4.0f }; 
            int totalLayers = layersRadius.Length; 

            float bulletLifeTime = (s.speed > 0f) ? s.speed : 5.0f; 
            float baseRotateSpeed = (s.angleOffset != 0f) ? s.angleOffset : 60f; 

            float currentElapsed = 0f; 
            int nextLayerToSpawn = 0; 
            float spawnTimer = 0f; 
            float spawnInterval = 5f / 60f; 

            while (currentElapsed < bulletLifeTime || layersBulletMatrix.Count < totalLayers) 
            {
                yield return new WaitForFixedUpdate(); 
                float dt = Time.fixedDeltaTime; 
                currentElapsed += dt; 
                spawnTimer += dt; 

                if (nextLayerToSpawn < totalLayers && (nextLayerToSpawn == 0 || spawnTimer >= spawnInterval)) 
                {
                    if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) 
                        break; 

                    spawnTimer = 0f; 
                    int layer = nextLayerToSpawn; 
                    float radius = layersRadius[layer]; 
                    float stepAngle = 360f / wayCount; // 💡2倍密度のステップ角を動的算出
                    float layerStartAngleOffset = layer * (stepAngle * 0.3f); 

                    List<Tuple<Transform, float>> currentLayerList = new List<Tuple<Transform, float>>(); 
                    PlaySkillSE(s.sePath); 

                    for (int i = 0; i < wayCount; i++) 
                    {
                        float initPlacementAngle = (stepAngle * i) + layerStartAngleOffset; 
                        float rad = initPlacementAngle * Mathf.Deg2Rad; 

                        Vector3 spawnPos = enemyCenterPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius; 
                        float faceCenterAngle = initPlacementAngle + 180f; 

                        DanmakuBullet bullet = CreateShot(s.bulletData, spawnPos, speed: 0f, angle: faceCenterAngle, delay: s.delay); 

                        if (bullet != null) 
                        {
                            bullet.isMovementSuspended = true; 
                            bullet.StartSelfDestructTimer(bulletLifeTime); 

                            _activeCageBullets.Add(bullet); 
                            currentLayerList.Add(new Tuple<Transform, float>(bullet.transform, initPlacementAngle)); 
                        }
                    }

                    layersBulletMatrix.Add(currentLayerList); 
                    nextLayerToSpawn++; 

                    if (nextLayerToSpawn >= totalLayers) 
                    {
                        isGenerationFinished = true; 
                        _activeSkillCoroutines--; 
                        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f; 
                        Debug.Log("<color=lime>🔓【檻設置完了】密度変調レイerを固定開通しました！</color>");
                    }
                }

                for (int layerIndex = 0; layerIndex < layersBulletMatrix.Count; layerIndex++) 
                {
                    float rotDirection = (layerIndex % 2 == 0) ? 1.0f : -1.0f; 
                    float deltaRotation = baseRotateSpeed * currentElapsed * rotDirection; 

                    var currentLayerBullets = layersBulletMatrix[layerIndex]; 
                    float radius = layersRadius[layerIndex]; 

                    for (int i = 0; i < currentLayerBullets.Count; i++) 
                    {
                        var bulletTuple = currentLayerBullets[i]; 
                        Transform bulletTx = bulletTuple.Item1; 

                        if (bulletTx == null || !bulletTx.gameObject.activeSelf) continue; 

                        float updatedPlacementAngle = bulletTuple.Item2 + deltaRotation; 
                        float rad = updatedPlacementAngle * Mathf.Deg2Rad; 

                        Vector3 newPosition = enemyCenterPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius; 
                        bulletTx.position = newPosition; 

                        float updatedFaceCenterAngle = updatedPlacementAngle + 180f; 
                        bulletTx.rotation = Quaternion.Euler(0, 0, updatedFaceCenterAngle - 90f); 
                    }
                }
            }

            yield return new WaitForSeconds(s.cooldown); 
        }
        finally
        {
            if (!isGenerationFinished) 
            {
                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f; 
                _activeSkillCoroutines--; 
            }
        }
    }

    // =========================================================================
    // 🔶 色欲専用C：公転・反射の軌跡（跡引きトレイル弾幕）
    // 💡【領域変調】：領域中なら、ターゲットへの「自機狙い・60度間隔の美しい2way」へ動的変化！
    // =========================================================================
    private IEnumerator ExecuteBouncingTrailShotRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; 

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>(); 
        PlayerMove myMove = GetComponentInParent<PlayerMove>(); 

        if (myMove != null && !_isEXSkillActive) 
        {
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier; 
        }

        List<BouncingBulletTrack> trackingBullets = new List<BouncingBulletTrack>(); 
        bool isCostReleased = false; 

        try
        {
            float mainBulletSpeed = (s.speed > 0f) ? s.speed : 5.0f; 
            Vector3 spawnOrigin = transform.position; 
            PlaySkillSE(s.sePath); 

            // 💡 領域展開中（スペルカードアクティブ）のフラグを動的取得
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

            // 🎯【数理空間の動的分岐】：領域中なら固定の2本のリストを自律生成、通常時はインスペクター準拠
            List<float> finalAngles = new List<float>();

            if (isSpellActive)
            {
                // 🔮 領域展開中：敵機への絶対角度を中心に、左右に30度ずつ開いた「60度間隔2way」を強制上書き
                float absoluteCenterAngle = GetAngleToTarget(spawnOrigin);
                finalAngles.Add(absoluteCenterAngle - 30f);
                finalAngles.Add(absoluteCenterAngle + 30f);
                Debug.Log("<color=gold>🔶【領域展開・色欲の双閃】反射親弾を「自機狙い60度間隔2way」へ動的ポリモーフィック変調！</color>");
            }
            else
            {
                // ⚔️ 通常空間：設定された n-Way 軌道から初期配置角を展開
                int shotCount = Mathf.Max(1, s.count); 
                float targetAngle = GetAngleToTarget(); 
                float baseAngle = targetAngle + s.angleOffset; 

                float startAngle = baseAngle; 
                float stepAngle = 0f; 
                if (shotCount > 1) 
                {
                    float spread = (s.wideAngle > 0f) ? s.wideAngle : 45f; 
                    startAngle = baseAngle - (spread / 2f); 
                    stepAngle = spread / (shotCount - 1); 
                }

                for (int i = 0; i < shotCount; i++)
                {
                    finalAngles.Add(startAngle + (stepAngle * i)); 
                }
            }

            // 🎯 確定した角度配列に基づいて親弾を一斉生成
            foreach (float launchAngle in finalAngles)
            {
                DanmakuBullet bullet = CreateShot(s.bulletData, spawnOrigin, speed: 0f, angle: launchAngle, delay: s.delay); 

                if (bullet != null) 
                {
                    bullet.isMovementSuspended = true; 
                    trackingBullets.Add(new BouncingBulletTrack(bullet.transform, launchAngle, mainBulletSpeed, bullet)); 
                }
            }

            float currentElapsed = 0f; 
            int frameCounter = 0; 

            const float wallMinX = -8.8f; 
            const float wallMaxX = 8.8f; 
            const float wallMaxY = 4.8f; 
            const float wallMinY = -4.8f; 

            float myTrailSpeed = 0.15f; 
            float myTrailLifeTime = 0.8f; 
            float myTrailAccel = 0; 

            BulletData trailBaseAsset = (s.trailBulletData != null) ? s.trailBulletData : s.bulletData; 

            while (trackingBullets.Count > 0) 
            {
                yield return new WaitForFixedUpdate(); 
                float dt = Time.fixedDeltaTime; 
                currentElapsed += dt; 
                frameCounter++; 

                if (!isCostReleased && currentElapsed >= 2.0f) 
                {
                    isCostReleased = true; 
                    _activeSkillCoroutines--; 
                    if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f; 
                    Debug.Log("<color=lime>🔓【反射トレイル】発射後2秒経過。早期解放しました！</color>"); 
                }

                bool shouldLeaveTrailThisFrame = (frameCounter % 4 == 0); 

                for (int i = trackingBullets.Count - 1; i >= 0; i--) 
                {
                    BouncingBulletTrack b = trackingBullets[i]; 

                    if (b.tx == null || !b.tx.gameObject.activeSelf || b.bulletLogic == null) 
                    {
                        trackingBullets.RemoveAt(i); 
                        continue; 
                    }

                    if (b.bulletLogic.DelayFrames > 0) 
                    {
                        continue; 
                    }

                    float rad = b.currentAngle * Mathf.Deg2Rad; 
                    Vector3 moveStep = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * b.speed * dt; 
                    b.tx.position += moveStep; 

                    b.tx.rotation = Quaternion.Euler(0, 0, b.currentAngle - 90f); 

                    Vector3 currentPos = b.tx.position; 
                    bool isDangerZone = false; 

                    if (b.bounceCount < 6) 
                    {
                        if (currentPos.x <= wallMinX && Mathf.Cos(rad) < 0f) 
                        {
                            b.currentAngle = 180f - b.currentAngle; 
                            b.bounceCount++; 
                            isDangerZone = true; 
                        }
                        else if (currentPos.x >= wallMaxX && Mathf.Cos(rad) > 0f) 
                        {
                            b.currentAngle = 180f - b.currentAngle; 
                            b.bounceCount++; 
                            isDangerZone = true; 
                        }

                        rad = b.currentAngle * Mathf.Deg2Rad; 
                        if (currentPos.y <= wallMinY && Mathf.Sin(rad) < 0f) 
                        {
                            b.currentAngle = -b.currentAngle; 
                            b.bounceCount++; 
                            isDangerZone = true; 
                        }
                        else if (currentPos.y >= wallMaxY && Mathf.Sin(rad) > 0f) 
                        {
                            b.currentAngle = -b.currentAngle; 
                            b.bounceCount++; 
                            isDangerZone = true; 
                        }

                        if (isDangerZone) 
                        {
                            b.currentAngle = (b.currentAngle + 360f) % 360f;
                            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT2, 0.15f); 
                        }
                    }
                    else 
                    {
                        if (Mathf.Abs(currentPos.x) > 10.0f || Mathf.Abs(currentPos.y) > 6.0f) 
                        {
                            b.bulletLogic.Deactivate(false); 
                            trackingBullets.RemoveAt(i); 
                            continue; 
                        }
                    }

                    if (shouldLeaveTrailThisFrame && !isDangerZone) 
                    {
                        for (int v = 0; v < 3; v++) 
                        {
                            float randomAngle = UnityEngine.Random.Range(0f, 360f); 
                            Vector3 positionNoise = new Vector3(UnityEngine.Random.Range(-0.05f, 0.05f), UnityEngine.Random.Range(-0.05f, 0.05f), 0f); 
                            Vector3 scatterSpawnPos = currentPos + positionNoise; 
                            if (SEManager.Instance != null) 
                            {
                                SEManager.Instance.Play(SEPath.SHOT1, 0.1f); 
                            }
                            DanmakuBullet trailBullet = CreateShot(trailBaseAsset, scatterSpawnPos, speed: myTrailSpeed, angle: randomAngle, delay: 0f, isConverge: false, accel: myTrailAccel); 
                            if (trailBullet != null) 
                            {
                                trailBullet.StartSelfDestructTimer(myTrailLifeTime); 
                            }
                        }
                    }
                }
            }

            yield return new WaitForSeconds(s.cooldown); 
        }
        finally
        {
            if (!isCostReleased) 
            {
                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f; 
                _activeSkillCoroutines--; 
            }
        }
    }
    // =========================================================================
    // 🔱【色欲Dスキル実体コルーチンループ】
    // =========================================================================
    // =========================================================================
    // 🔱【色欲Dスキル実体コルーチンループ・マナロック完全修正版】
    // =========================================================================
    private IEnumerator ExecuteLustSpearAssaultRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++; // 🟢 マナの自動回復を一時停止

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        try
        {
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

            PlaySkillSE(s.sePath);

            int wayCount = isSpellActive ? 3 : 1;
            float spreadAngle = 45f;
            float sizeMultiplier = isSpellActive ? 1.5f : 1.0f;

            float targetAngle = GetAngleToTarget(transform.position);
            float baseAngle = targetAngle + s.angleOffset;

            if (wayCount == 1)
            {
                CreateShot(s.bulletData, transform.position, s.speed, baseAngle, delay: s.delay,
                           isConverge: false, accel: 0f, maxSpeed: 0f,
                           customMaterial: null, customScale: sizeMultiplier);
            }
            else
            {
                float startAngle = baseAngle - (spreadAngle / 2f);
                float stepAngle = spreadAngle / (wayCount - 1);

                for (int i = 0; i < wayCount; i++)
                {
                    float finalSpearAngle = startAngle + (stepAngle * i);
                    CreateShot(s.bulletData, transform.position, s.speed, finalSpearAngle, delay: s.delay,
                               isConverge: false, accel: 0f, maxSpeed: 0f,
                               customMaterial: null, customScale: sizeMultiplier);
                }
            }

            // 🛠️ 槍が起き上がって前方に射出されるまでのタメ（delay秒）を待機
            yield return new WaitForSeconds(s.delay);

            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT2, 0.3f);

            // =========================================================================
            // 🎯【修正の核心】：クールダウン消化エリア
            // =========================================================================
            // 💡 理由：ここに正常な yield ホールドを置いたことで、コルーチンが途中でハーフフリーズせず、
            //    技全体のタイムラインが完結した後に finally 句へと100%美しく遷移します！
            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            // スキル全体の終了に伴い、移動速度とマナ自動回復のロックを完全に全面解放！
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
        }
    }

    private class BouncingBulletTrack
    {
        public Transform tx;
        public float currentAngle;
        public float speed;
        public int bounceCount;
        public DanmakuBullet bulletLogic;

        public BouncingBulletTrack(Transform t, float angle, float spd, DanmakuBullet logic)
        {
            this.tx = t;
            this.currentAngle = angle;
            this.speed = spd;
            this.bounceCount = 0;
            this.bulletLogic = logic;
        }
    }
}