using KanKikuchi.AudioManager;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Emitter_Lust : PlayerDanmakuEmitter
{
    private bool _isLustSpearCharging = false;

    // 🎯【新設インフラ】：現在戦場に実体化しているシールドへのポインタを記憶
    private LustTrackingShield _currentActiveShield = null;

    // 💡 外部（SkillManagerなど）から「現在シールドが生きているか？」を安全確認するための公開窓口
    public bool IsShieldActive => (_currentActiveShield != null && _currentActiveShield.gameObject != null && _currentActiveShield.gameObject.activeSelf);

    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteLustSpearAssaultRoutine(s));
    }

    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteEnemyEnclosureShrinkingRingRoutine(s));
    }

    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteBouncingTrailShotRoutine(s));
    }

    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteTrackSpear(s));
    }

    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(ExecuteSkillEXLust(s));
    }

    private List<DanmakuBullet> _activeCageBullets = new List<DanmakuBullet>();

    private IEnumerator ExecuteEnemyEnclosureShrinkingRingRoutine(PlayerSkillData.SkillSettings s)
    {
        if (_activeCageBullets != null && _activeCageBullets.Count > 0)
        {
            for (int b = _activeCageBullets.Count - 1; b >= 0; b--)
            {
                DanmakuBullet oldBullet = _activeCageBullets[b];
                if (oldBullet != null && oldBullet.gameObject.activeSelf) oldBullet.Deactivate(true);
            }
            _activeCageBullets.Clear();
        }

        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        List<List<Tuple<Transform, float>>> layersBulletMatrix = new List<List<Tuple<Transform, float>>>();
        bool isGenerationFinished = false;

        try
        {
            Transform targetEnemyTransform = null;
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p != null && p.gameObject != _rootOwner) { targetEnemyTransform = p.transform; break; }
            }
            Vector3 enemyCenterPos = (targetEnemyTransform != null) ? targetEnemyTransform.position : Vector3.zero;

            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

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

            // 💡 修正：while全体の強制離脱（break）条件をパージし、時間またはレイヤー維持のライフサイクルを絶対保証
            while (currentElapsed < bulletLifeTime || layersBulletMatrix.Count < totalLayers)
            {
                yield return new WaitForFixedUpdate();
                float dt = Time.fixedDeltaTime;
                currentElapsed += dt;
                spawnTimer += dt;

                if (nextLayerToSpawn < totalLayers && (nextLayerToSpawn == 0 || spawnTimer >= spawnInterval))
                {
                    // ⭕ 修正の核心：被弾中または射撃不可の時は、ループを破壊（break）するのではなく、
                    //                「新規生成のフェーズだけを丸ごとスキップ（continue）」させる盾へ切り替えます！
                    //                これにより、すでに戦場に出ているレイヤーの回転計算（下部）へ安全にタイムラインが流れます。
                    if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                    {
                        // 生成はスキップするが、タイマーをリセットして無限連打を防ぐ
                        spawnTimer = 0f;
                        continue;
                    }

                    spawnTimer = 0f;
                    int layer = nextLayerToSpawn;
                    float radius = layersRadius[layer];
                    float stepAngle = 360f / wayCount;
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
                    }
                }

                // 🎯【自律公転制御】：新規生成がスキップされていても、この回転アニメーション処理は毎フレーム100%確実に稼働し続けます！
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
    // 💡【弾消し耐性溶接】：Cスキルのブーメラン親弾（メイン弾）に弾消し耐性（isIndestructible: true）を付与！
    // =========================================================================
    private IEnumerator ExecuteBouncingTrailShotRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        List<BouncingBulletTrack> trackingBullets = new List<BouncingBulletTrack>();
        bool isCostReleased = false;

        try
        {
            float mainBulletSpeed = (s.speed > 0f) ? s.speed : 5.0f;
            Vector3 spawnOrigin = transform.position;
            PlaySkillSE(s.sePath);

            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

            List<float> finalAngles = new List<float>();

            if (isSpellActive)
            {
                float absoluteCenterAngle = GetAngleToTarget(spawnOrigin);
                finalAngles.Add(absoluteCenterAngle - 30f);
                finalAngles.Add(absoluteCenterAngle + 30f);
            }
            else
            {
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

                for (int i = 0; i < shotCount; i++) finalAngles.Add(startAngle + (stepAngle * i));
            }

            foreach (float launchAngle in finalAngles)
            {
                // 🎯 最末尾の引数に「true」を注入し、親弾を絶対に消えない弾消し耐性状態へ！[cite: 31]
                DanmakuBullet bullet = CreateShot(s.bulletData, spawnOrigin, speed: 0f, angle: launchAngle, delay: s.delay, false, 0, 0, null, 1, isIndestructible: true);
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

                    if (b.bulletLogic.DelayFrames > 0) continue;

                    float rad = b.currentAngle * Mathf.Deg2Rad;
                    Vector3 moveStep = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * b.speed * dt;
                    b.tx.position += moveStep;

                    b.tx.rotation = Quaternion.Euler(0, 0, b.currentAngle - 90f);

                    Vector3 currentPos = b.tx.position;
                    bool isDangerZone = false;

                    if (b.bounceCount < 6)
                    {
                        if (currentPos.x <= wallMinX && Mathf.Cos(rad) < 0f) { b.currentAngle = 180f - b.currentAngle; b.bounceCount++; isDangerZone = true; }
                        else if (currentPos.x >= wallMaxX && Mathf.Cos(rad) > 0f) { b.currentAngle = 180f - b.currentAngle; b.bounceCount++; isDangerZone = true; }

                        rad = b.currentAngle * Mathf.Deg2Rad;
                        if (currentPos.y <= wallMinY && Mathf.Sin(rad) < 0f) { b.currentAngle = -b.currentAngle; b.bounceCount++; isDangerZone = true; }
                        else if (currentPos.y >= wallMaxY && Mathf.Sin(rad) > 0f) { b.currentAngle = -b.currentAngle; b.bounceCount++; isDangerZone = true; }

                        if (isDangerZone)
                        {
                            b.currentAngle = (b.currentAngle + 360f) % 360f;
                            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT2, 0.15f);
                        }
                    }
                    else
                    {
                        if (Mathf.Abs(currentPos.x) > 10.0f || Mathf.Abs(currentPos.y) > 6.0f) { b.bulletLogic.Deactivate(false); trackingBullets.RemoveAt(i); continue; }
                    }

                    if (shouldLeaveTrailThisFrame && !isDangerZone)
                    {
                        for (int v = 0; v < 3; v++)
                        {
                            float randomAngle = UnityEngine.Random.Range(0f, 360f);
                            Vector3 positionNoise = new Vector3(UnityEngine.Random.Range(-0.05f, 0.05f), UnityEngine.Random.Range(-0.05f, 0.05f), 0f);
                            Vector3 scatterSpawnPos = currentPos + positionNoise;
                            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT1, 0.1f);
                            // 💡 跡引きトレイル子弾は消し能力に触れると通常消滅（等倍維持）
                            DanmakuBullet trailBullet = CreateShot(trailBaseAsset, scatterSpawnPos, speed: myTrailSpeed, angle: randomAngle, delay: 0f, isConverge: false, accel: myTrailAccel);
                            if (trailBullet != null) trailBullet.StartSelfDestructTimer(myTrailLifeTime);
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

    private IEnumerator ExecuteLustSpearAssaultRoutine(PlayerSkillData.SkillSettings s)
    {
        if (_isLustSpearCharging) yield break;
        _isLustSpearCharging = true;

        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();

        float chargeMoveSpeed = (s.moveSpeedMultiplier > 0f) ? s.moveSpeedMultiplier : 0.4f;
        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = chargeMoveSpeed;

        UnityEngine.InputSystem.InputAction zAction = null;
        if (InputManager.Instance != null && myStatus != null)
        {
            var inputSet = (myStatus.playerId == 2) ? InputManager.Instance.player2 : InputManager.Instance.player1;
            if (inputSet.skillZ != null) zAction = inputSet.skillZ.action;
        }

        GameObject indicatorObj = new GameObject("SpearAimIndicator");
        MeshFilter meshFilter = indicatorObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = indicatorObj.AddComponent<MeshRenderer>();

        meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);
        Color indicatorColor = new Color(1f, 0.2f, 0.2f, 0.5f);
        meshRenderer.material.color = indicatorColor;

        SpriteRenderer mySR = GetComponent<SpriteRenderer>();
        if (mySR == null) mySR = GetComponentInParent<SpriteRenderer>();
        if (mySR == null) mySR = GetComponentInChildren<SpriteRenderer>();

        if (mySR != null) { meshRenderer.sortingLayerID = mySR.sortingLayerID; meshRenderer.sortingOrder = 14900; }
        else { meshRenderer.sortingLayerName = "Default"; meshRenderer.sortingOrder = 14900; }

        Mesh reusableMesh = new Mesh();

        try
        {
            PlaySkillSE(s.sePath);

            int wayCount = isSpellActive ? 5 : 3;
            float sizeMultiplier = isSpellActive ? 1.5f : 1.0f;

            float currentSpread = 120f;
            float minSpread = 15f;

            int chargeTargetFrames = 60;
            float shrinkSpeed = (120f - minSpread) / chargeTargetFrames;

            float initialTargetAngle = GetAngleToTarget(transform.position);
            float fixedBaseAngle = initialTargetAngle + s.angleOffset;

            int elapsedFrames = 0;
            bool isKeyReleased = false;

            while (!isKeyReleased)
            {
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal)) break;

                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = chargeMoveSpeed;
                if (indicatorObj != null) indicatorObj.transform.position = transform.position;

                if (elapsedFrames < chargeTargetFrames) currentSpread = Mathf.Max(minSpread, currentSpread - shrinkSpeed);
                else currentSpread = minSpread;

                DrawFanMesh(meshFilter, reusableMesh, currentSpread, fixedBaseAngle, 2.5f);

                if (elapsedFrames < chargeTargetFrames && elapsedFrames % 12 == 0 && SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT1, 0.12f);

                yield return new WaitForFixedUpdate();
                elapsedFrames++;

                // ⭕ 修正後：AI操作中か人間操作中かを自動判別し、AIならインフラフレームの shotZ を最優先信用する！
                if (elapsedFrames >= 3)
                {
                    DanmakuAgent agent = GetComponentInChildren<DanmakuAgent>();
                    if (agent == null) agent = GetComponentInParent<DanmakuAgent>();

                    // 🤖 AI自動操作モードの時のホールド・リリース判定
                    if (agent != null && agent._useAutoEvadeAI)
                    {
                        if (myMove != null && !myMove.currentFrameInput.shotZ)
                        {
                            isKeyReleased = true; // AIが62Fに達して shotZ = false にした瞬間にリリース
                        }
                    }
                    // 🧑 人間がキーボード・パッドで手動操作している時の判定
                    else
                    {
                        if (zAction != null && !zAction.IsPressed()) isKeyReleased = true;
                        else if (zAction == null && !Input.anyKey) isKeyReleased = true;
                    }
                }
            }

            float startAngle = fixedBaseAngle - (currentSpread / 2f);
            float stepAngle = (wayCount > 1) ? (currentSpread / (wayCount - 1)) : 0f;

            if (indicatorObj != null) Destroy(indicatorObj);

            for (int i = 0; i < wayCount; i++)
            {
                float finalSpearAngle = startAngle + (stepAngle * i);
                CreateShot(s.bulletData, transform.position, s.speed, finalSpearAngle, delay: s.delay, isConverge: false, accel: 0f, maxSpeed: 0f, customMaterial: null, customScale: sizeMultiplier, true);
            }

            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT2, 0.4f);
            yield return new WaitForSeconds(s.delay / 60f);
        }
        finally
        {
            if (indicatorObj != null) Destroy(indicatorObj);
            if (reusableMesh != null) Destroy(reusableMesh);
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;

            _activeSkillCoroutines--;
            _isLustSpearCharging = false;
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

    private class BouncingBulletTrack
    {
        public Transform tx;
        public float currentAngle;
        public float speed;
        public int bounceCount;
        public DanmakuBullet bulletLogic;

        public BouncingBulletTrack(Transform t, float angle, float spd, DanmakuBullet logic)
        {
            this.tx = t; this.currentAngle = angle; this.speed = spd; this.bounceCount = 0; this.bulletLogic = logic;
        }
    }

    protected IEnumerator ExecuteTrackSpear(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        try
        {
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            PlaySkillSE(s.sePath);

            Transform enemyTarget = null;
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p != null && p.gameObject != _rootOwner) { enemyTarget = p.transform; break; }
            }

            int ownerId = (myStatus != null) ? myStatus.playerId : 1;
            string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
            int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");

            if (s.bulletData != null && s.bulletData.bulletPrefab != null)
            {
                GameObject shieldObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);
                shieldObj.tag = assignedTag;
                shieldObj.layer = assignedLayer;
                SetLayerRecursive(shieldObj, assignedLayer);

                SpriteRenderer shieldSR = shieldObj.GetComponent<SpriteRenderer>();
                if (shieldSR != null)
                {
                    shieldSR.sprite = s.bulletData.bulletSprite;
                    if (s.bulletData.material != null) shieldSR.material = s.bulletData.material;
                }

                LustTrackingShield shieldLogic = shieldObj.GetComponent<LustTrackingShield>();
                if (shieldLogic == null) shieldLogic = shieldObj.AddComponent<LustTrackingShield>();

                bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);
                float trackingSpeed = (s.speed > 0f) ? s.speed : 1.5f;
                float durationMultiplier = isSpellActive ? 1.5f : 1.0f;

                _currentActiveShield = shieldLogic;

                shieldLogic.Initialize(_rootOwner.transform, enemyTarget, s.bulletData, trackingSpeed, duration: 5.0f * durationMultiplier);
            }
            yield return null;
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
        }
    }
    // =========================================================================
    // 🛡️✨【新設】：通常Zスキル（チャージ槍）の入力時に、展開中のシールドを即座に完全パージする窓口
    // =========================================================================
    public void PurgeActiveShield()
    {
        if (IsShieldActive)
        {
            Debug.Log("<color=red>🛡️➔⚡【シールド強制終了連動】Zスキル発動を検知したため、生存中のV魔槍シールドを即座にパージします。</color>");
            _currentActiveShield.ForceRequestDespawn();
        }
    }
    // =========================================================================
    // 👑【色欲EX必殺術式】：必殺の突撃大槍に、不滅の弾消し耐性を完全溶接！
    // =========================================================================
    protected IEnumerator ExecuteSkillEXLust(PlayerSkillData.SkillSettings s)
    {
        if (IsShieldActive)
        {
            Debug.Log("<color=red>🛡️➔👑【シールド強制終了連動】EX発動を検知したため、生存中のV魔槍をパージします。</color>");
            _currentActiveShield.ForceRequestDespawn();
        }

        _activeSkillCoroutines++;
        _isEXSkillActive = true;

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();

        if (myMove != null) myMove.skillSpeedMultiplier = 0.15f;

        Transform enemyTarget = null;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != _rootOwner) { enemyTarget = p.transform; break; }
        }

        int ownerId = (myStatus != null) ? myStatus.playerId : 1;
        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");

        GameObject spearObj = Instantiate(s.bulletData.bulletPrefab, transform.position, Quaternion.identity);
        spearObj.tag = assignedTag;
        spearObj.layer = assignedLayer;
        SetLayerRecursive(spearObj, assignedLayer);

        SpriteRenderer spearSR = spearObj.GetComponent<SpriteRenderer>();
        if (spearSR != null)
        {
            spearSR.sprite = s.bulletData.bulletSprite;
            if (s.bulletData.material != null) spearSR.material = s.bulletData.material;
            spearSR.sortingOrder = 16000;
        }

        float currentAimAngle = (ownerId == 2) ? 180f : 0f;
        spearObj.transform.rotation = Quaternion.Euler(90f, 0f, currentAimAngle - 90f);

        GameObject lineObj = new GameObject("EXLaserPreviewLine");
        LineRenderer previewLine = lineObj.AddComponent<LineRenderer>();
        previewLine.material = new Material(Shader.Find("Sprites/Default"));
        previewLine.startColor = new Color(1f,1, 0.2f, 0.8f);
        previewLine.endColor = new Color(1f, 1, 0.2f, 0.8f);
        previewLine.startWidth = 0.06f;
        previewLine.endWidth = 0.03f;
        previewLine.positionCount = 2;

        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        try
        {
            PlaySkillSE(s.sePath);

            float lockOnElapsed = 0f;
            float riseDuration = 0.5f;
            float maxTurnSpeedPerSecond = 360f;

            while (lockOnElapsed < riseDuration)
            {
                lockOnElapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(lockOnElapsed / riseDuration);

                float instantTargetAngle = GetAngleToTarget(transform.position);
                currentAimAngle = Mathf.MoveTowardsAngle(currentAimAngle, instantTargetAngle, maxTurnSpeedPerSecond * Time.deltaTime);

                float currentXRot = Mathf.Lerp(90f, 0f, progress);
                spearObj.transform.rotation = Quaternion.Euler(currentXRot, 0f, currentAimAngle - 90f);

                if (lineObj != null)
                {
                    previewLine.SetPosition(0, transform.position);
                    float rad = currentAimAngle * Mathf.Deg2Rad;
                    Vector3 endpoint = transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * 15f;
                    previewLine.SetPosition(1, endpoint);
                }
                yield return null;
            }

            float fixedFinalAngle = currentAimAngle;

            float totalLockOnDuration = 1.4f;
            float remainingChargeTime = totalLockOnDuration - riseDuration;

            if (BossEffectManager.Instance != null && _rootOwner != null)
            {
                BossEffectManager.Instance.PlayChargeEffect(remainingChargeTime-0.5f, s.bulletData.breakColor, _rootOwner.transform.position);
            }

            float chargeElapsed = 0f;
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.LASER2, 0.5f);

            while (chargeElapsed < remainingChargeTime)
            {
                chargeElapsed += Time.deltaTime;
                spearObj.transform.rotation = Quaternion.Euler(0f, 0f, fixedFinalAngle - 90f);

                if (lineObj != null)
                {
                    previewLine.SetPosition(0, transform.position);
                    float rad = fixedFinalAngle * Mathf.Deg2Rad;
                    Vector3 endpoint = transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * 15f;
                    previewLine.SetPosition(1, endpoint);
                }
                yield return null;
            }

            if (lineObj != null) Destroy(lineObj);
            if (SEManager.Instance != null) SEManager.Instance.Play(SEPath.SHOT2, 0.5f);

            // 🎯 プロジェクタイルスクリプトを追加
            LustEXSpearProjectile projectileLogic = spearObj.AddComponent<LustEXSpearProjectile>();
            float ultraSpeed = (s.speed > 0f) ? s.speed * 1.5f : 22.0f;

            // 💡 射出する突撃魔槍自身のオブジェクトをコンポ追加後にフリップ起動
            // 💡 親クラスから提供されているファクトリ窓口に「isIndestructible: true」を付与して生成されるのが理想ですが、
            // 💡 Prefab Instantiate型ロジックのため、ここで直撃「isIndestructible = true」を仕込んで防御判定を無敵化させます。
            DanmakuBullet spearBulletComponent = spearObj.GetComponent<DanmakuBullet>();
            if (spearBulletComponent != null)
            {
                spearBulletComponent.isIndestructible = true;
            }

            projectileLogic.Launch(_rootOwner, fixedFinalAngle, ultraSpeed, s.bulletData, s.trailBulletData, this, enableHoming: isSpellActive);

            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            if (lineObj != null) Destroy(lineObj);
            if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
            if (spearObj == null) _isEXSkillActive = false;

            _activeSkillCoroutines--;
        }
    }
}