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
    // 🛡️【安全弁】：万が一フラグやカウントがスタックした際、一定時間で強制解除するフェイルセーフ
    private float _lustSafetyTimer = 0f;

    // 🌟【新規追加】：EX魔槍が戦場に発射されてから消滅するまでの生存フラグ
    private bool _isEXSpearActive = false;
    public bool IsEXSpearActive => _isEXSpearActive;

    void Update()
    {
        // もし何かしらの理由で3秒以上チャージやスキル硬直から抜け出せない場合、強制リセット
        if (_isLustSpearCharging || _activeSkillCoroutines > 0)
        {
            _lustSafetyTimer += Time.deltaTime;
            if (_lustSafetyTimer > 4.0f)
            {
                Debug.LogWarning("⚠️ [Lust Safety] スキルスタック検知。強制的にフラグとコルーチンカウントをリセットします。");
                _isLustSpearCharging = false;
                _activeSkillCoroutines = 0;
                _lustSafetyTimer = 0f;
            }
        }
        else
        {
            _lustSafetyTimer = 0f;
        }
    }

    public bool IsShieldActive => (_currentActiveShield != null && _currentActiveShield.gameObject != null && _currentActiveShield.gameObject.activeSelf);

    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        if (_isEXSkillActive || _isEXSpearActive) yield break; // 🌟 EX硬直中またはEX魔槍生存中は使用不可
        yield return StartCoroutine(ExecuteLustSpearAssaultRoutine(s));
    }

    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        if (_isEXSkillActive || _isEXSpearActive) yield break; // 🌟 EX硬直中またはEX魔槍生存中は使用不可
        yield return StartCoroutine(ExecuteEnemyEnclosureShrinkingRingRoutine(s));
    }

    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        if (_isEXSkillActive || _isEXSpearActive) yield break; // 🌟 EX硬直中またはEX魔槍生存中は使用不可
        yield return StartCoroutine(ExecuteBouncingTrailShotRoutine(s));
    }

    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        if (_isEXSkillActive || _isEXSpearActive) yield break; // 🌟 EX硬直中またはEX魔槍生存中は使用不可
        yield return StartCoroutine(ExecuteTrackSpear(s));
    }

    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        if (_isEXSkillActive || _isEXSpearActive) yield break; // 🌟 すでにEX魔槍が生存しているなら重複発動不可
        yield return StartCoroutine(ExecuteSkillEXLust(s));
    }

    private List<Tuple<DanmakuBullet, int>> _activeCageBulletsWithGenId = new List<Tuple<DanmakuBullet, int>>();

    private IEnumerator ExecuteEnemyEnclosureShrinkingRingRoutine(PlayerSkillData.SkillSettings s)
    {
        if (_activeCageBulletsWithGenId != null && _activeCageBulletsWithGenId.Count > 0)
        {
            for (int b = _activeCageBulletsWithGenId.Count - 1; b >= 0; b--)
            {
                var bulletTuple = _activeCageBulletsWithGenId[b];
                DanmakuBullet oldBullet = bulletTuple.Item1;
                int spawnedGenId = bulletTuple.Item2;

                if (oldBullet != null && oldBullet.gameObject.activeSelf)
                {
                    if (oldBullet.originPrefab == s.bulletData.bulletPrefab && oldBullet.instanceGenerationId == spawnedGenId)
                    {
                        oldBullet.Deactivate(true);
                    }
                }
            }
            _activeCageBulletsWithGenId.Clear();
        }

        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        List<List<Tuple<Transform, float, DanmakuBullet, int>>> layersBulletMatrix = new List<List<Tuple<Transform, float, DanmakuBullet, int>>>();
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

            while (currentElapsed < bulletLifeTime || layersBulletMatrix.Count < totalLayers)
            {
                yield return new WaitForFixedUpdate();
                float dt = Time.fixedDeltaTime;
                currentElapsed += dt;
                spawnTimer += dt;

                if (nextLayerToSpawn < totalLayers && (nextLayerToSpawn == 0 || spawnTimer >= spawnInterval))
                {
                    if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                    {
                        spawnTimer = 0f;
                        continue;
                    }

                    spawnTimer = 0f;
                    int layer = nextLayerToSpawn;
                    float radius = layersRadius[layer];
                    float stepAngle = 360f / wayCount;
                    float layerStartAngleOffset = layer * (stepAngle * 0.3f);

                    List<Tuple<Transform, float, DanmakuBullet, int>> currentLayerList = new List<Tuple<Transform, float, DanmakuBullet, int>>();
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

                            var bulletRecord = new Tuple<DanmakuBullet, int>(bullet, bullet.instanceGenerationId);
                            _activeCageBulletsWithGenId.Add(bulletRecord);

                            currentLayerList.Add(new Tuple<Transform, float, DanmakuBullet, int>(bullet.transform, initPlacementAngle, bullet, bullet.instanceGenerationId));
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
                        DanmakuBullet bLogic = bulletTuple.Item3;
                        int originGenId = bulletTuple.Item4;

                        if (bulletTx == null || !bulletTx.gameObject.activeSelf || bLogic == null || bLogic.instanceGenerationId != originGenId)
                            continue;

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

            int chargeTargetFrames = 45;
            float shrinkSpeed = (120f - minSpread) / chargeTargetFrames;

            // 🎯【要件①】：引き絞り中は敵への角度追従を行わず、最初に取得したアングルで完全に固定！
            float fixedBaseAngle = GetAngleToTarget(transform.position) + s.angleOffset;

            int elapsedFrames = 0;
            bool isKeyReleased = false;

            while (!isKeyReleased)
            {
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                {
                    isKeyReleased = true;
                    break;
                }

                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = chargeMoveSpeed;
                if (indicatorObj != null) indicatorObj.transform.position = transform.position;

                if (elapsedFrames < chargeTargetFrames)
                {
                    currentSpread = Mathf.Max(minSpread, 120f - (120f - minSpread) * ((float)elapsedFrames / chargeTargetFrames));
                }
                else
                {
                    currentSpread = minSpread;
                }

                // 固定されたアングルと、引き絞りに応じた収束扇形を描画
                DrawFanMesh(meshFilter, reusableMesh, currentSpread, fixedBaseAngle, 2.5f);

                yield return new WaitForFixedUpdate();
                elapsedFrames++;

                DanmakuAgent agent = GetComponentInChildren<DanmakuAgent>();
                if (agent == null) agent = GetComponentInParent<DanmakuAgent>();

                if (agent != null && agent._useAutoEvadeAI)
                {
                    if (elapsedFrames >= chargeTargetFrames)
                    {
                        isKeyReleased = true;
                    }
                }
                else
                {
                    if (zAction != null && !zAction.IsPressed()) isKeyReleased = true;
                    else if (zAction == null && !Input.anyKey) isKeyReleased = true;
                    else if (elapsedFrames >= 90) isKeyReleased = true;
                }
            }

            // 🎯【要件②】：槍（弾）の消滅・発動は引き絞ったときではなく、ボタンを離して発射したこの瞬間に実行！
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

            _isLustSpearCharging = false;
            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
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

    public void PurgeActiveShield()
    {
        if (IsShieldActive)
        {
            Debug.Log("<color=red>🛡️➔⚡【シールド強制終了連動】Zスキル発動を検知したため、生存中のV魔槍シールドを即座にパージします。</color>");
            _currentActiveShield.ForceRequestDespawn();
        }
    }

    protected IEnumerator ExecuteSkillEXLust(PlayerSkillData.SkillSettings s)
    {
        // 🌟 すでにEX魔槍が生存している場合は発動を拒絶
        if (_isEXSpearActive) yield break;

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
        previewLine.startColor = new Color(1f, 1, 0.2f, 0.8f);
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
                BossEffectManager.Instance.PlayChargeEffect(remainingChargeTime - 0.5f, s.bulletData.breakColor, _rootOwner.transform.position);
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

            // 🌟【最重要】：槍が発射されて戦場に飛び出したこの瞬間に生存フラグをONにする
            _isEXSpearActive = true;

            LustEXSpearProjectile projectileLogic = spearObj.AddComponent<LustEXSpearProjectile>();
            float ultraSpeed = (s.speed > 0f) ? s.speed * 1.5f : 22.0f;

            DanmakuBullet spearBulletComponent = spearObj.GetComponent<DanmakuBullet>();
            if (spearBulletComponent != null)
            {
                spearBulletComponent.isIndestructible = true;
            }

            projectileLogic.Launch(_rootOwner, fixedFinalAngle, ultraSpeed, s.bulletData, s.trailBulletData, this, enableHoming: isSpellActive);

            // 🌟 槍オブジェクトの生存を監視し、消滅したらフラグを落とす
            StartCoroutine(MonitorEXSpearLife(spearObj));

            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            _isEXSkillActive = false;

            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;

            if (lineObj != null) Destroy(lineObj);
            if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
        }
    }

    private IEnumerator MonitorEXSpearLife(GameObject spear)
    {
        while (spear != null)
        {
            yield return null;
        }
        // 槍が画面外消滅やヒット等で消えたら制限解除
        _isEXSpearActive = false;
        Debug.Log("<color=cyan>👑 [EX Spear] 槍が戦場から消滅しました。スキル使用制限を解除します。</color>");
    }
}