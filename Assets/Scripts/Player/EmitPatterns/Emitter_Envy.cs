using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Emitter_Envy : PlayerDanmakuEmitter
{
    private bool _isEnvyZHolding = false;
    public bool IsEnvyZHolding => _isEnvyZHolding;

    [Header("Envy V fog effect")]
    [SerializeField] private GameObject jealousyFogPrefab;
    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        float Speed2 = s.speed;
        int Count2 = s.count;
        if (_isEnvyZHolding) yield break;
        _isEnvyZHolding = true;
        _activeSkillCoroutines++;

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();

        float fireInterval = 0.05f;

        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        if (isSpellActive && s.bulletData != null) 
        {
            Speed2 *= 1.5f; // Increase speed by 50% during spell card
            Count2 *= 2; // Double the count during spell card
        }

        try
        {
            while (true)
            {
                if (!PlayerMove.CanShoot || myStatus == null) break;

                if (myMove != null && myMove.currentEnergy < s.cost)
                {
                    break;
                }

                PlaySkillSE(s.sePath);
                if (pAnim != null) pAnim.TriggerSkillAnimation(s.skillName);

                float targetAngle = GetAngleToTarget(transform.position) + s.angleOffset;

                if (Count2 > 1)
                {
                    float wayAngle = s.wideAngle / (Count2 - 1);
                    float startWayAngle = targetAngle - (s.wideAngle / 2f);

                    for (int i = 0; i < Count2; i++)
                    {
                        float finalAngle = targetAngle + Random.Range(-16, 16);
                        CreateShot(s.bulletData, transform.position, Speed2, finalAngle, delay: s.delay);
                    }
                }
                else
                {
                    CreateShot(s.bulletData, transform.position, Speed2, targetAngle, delay: s.delay);
                }

                if (myMove != null)
                {
                    myMove.currentEnergy -= s.cost;
                    myMove.AddUltimateEnergy(s.ultimateGain * 0.2f);
                }

                float elapsed = 0f;
                while (elapsed < fireInterval)
                {
                    elapsed += Time.deltaTime;

                    if (!IsZButtonPressed(myStatus))
                    {
                        yield break;
                    }
                    yield return null;
                }
            }
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _isEnvyZHolding = false;
            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;

            SkillManager skillMgr = GetComponentInChildren<SkillManager>();
            if (skillMgr == null && _rootOwner != null) skillMgr = _rootOwner.GetComponentInChildren<SkillManager>();
            if (skillMgr != null)
            {
                float cooldownMultiplier = (myStatus != null && myStatus.isOverheated) ? 1.5f : 1.0f;
                skillMgr.timerZ = s.cooldown * cooldownMultiplier;
            }
        }
    }

    private bool IsZButtonPressed(PlayerStatusManager status)
    {
        if (status == null || InputManager.Instance == null) return false;

        var inputSet = (status.playerId == 2) ? InputManager.Instance.player2 : InputManager.Instance.player1;
        if (inputSet.skillZ != null && inputSet.skillZ.action != null)
        {
            return inputSet.skillZ.action.IsPressed();
        }

        return Input.GetKey(KeyCode.L) || Input.GetKey(KeyCode.Z);
    }

    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();

        bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);
        int way = isSpellActive ? 12 : 8; // Increase way count during spell card

        try
        {
            FireCurvingOmnidirectionalShot(s, 0.6f, 40f, 120f,way);
            yield return new WaitForSeconds(s.cooldown);
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
        }
    }
    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();

        try
        {
            if (myMove != null && !_isEXSkillActive)
                myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

            PlaySkillSE(s.sePath);

            PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
            if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();
            if (pAnim != null) pAnim.TriggerSkillAnimation(s.skillName);

            GameObject familiarObj = new GameObject("EnvyFamiliar");
            familiarObj.transform.position = transform.position;

            Transform targetTransform = null;
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p != null && p.gameObject != _rootOwner)
                {
                    targetTransform = p.transform;
                    break;
                }
            }

            EnvyFamiliarObject familiar = familiarObj.AddComponent<EnvyFamiliarObject>();

            int ownerId = (myStatus != null) ? myStatus.playerId : 1;

            BulletData[] familiarBullets = (s.multiBulletDatas != null && s.multiBulletDatas.Length > 0)
                ? s.multiBulletDatas
                : null;

            float familiarDuration = (myStatus != null && myStatus.isSpellCardActive) ? 12.0f : 8.0f;
            float spellBoost = (myStatus != null && myStatus.isSpellCardActive) ? 1.5f : 1.0f;

            familiar.Initialize(
                owner: transform,
                target: targetTransform,
                defaultData: s.bulletData,
                familiarMultiBulletDatas: familiarBullets,
                emitter: this,
                targetTag: targetTag,
                ownerId: ownerId,
                duration: familiarDuration,
                spellBoostMultiplier: spellBoost
            );

            if (myMove != null)
            {
                myMove.AddUltimateEnergy(s.ultimateGain);
            }

            yield break;
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive)
                myMove.skillSpeedMultiplier = 1.0f;

            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
        }
    }
    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();

        if (myMove == null)
            yield break;

        // Spell中は長くする
        float duration = (myStatus != null && myStatus.isSpellCardActive) ? 5.0f : 3.0f;

        PlaySkillSE(s.sePath);

        myMove.SetInvincible(duration);

        if (!_isEXSkillActive)
        {
            myMove.skillSpeedMultiplier = (s.moveSpeedMultiplier > 0f) ? s.moveSpeedMultiplier : 1.0f;
        }

        SpriteRenderer[] bodyRenderers = GetComponentsInChildren<SpriteRenderer>();
        if (bodyRenderers.Length == 0 && _rootOwner != null)
        {
            bodyRenderers = _rootOwner.GetComponentsInChildren<SpriteRenderer>();
        }

        Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
        foreach (var sr in bodyRenderers)
        {
            if (sr != null)
            {
                originalColors[sr] = sr.color;
                Color c = sr.color;
                c.a = 0.45f;
                sr.color = c;
            }
        }

        float elapsed = 0f;
        float fogTimer = 0f;
        float fogInterval = 0.015f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fogTimer += Time.deltaTime;

            if (fogTimer >= fogInterval)
            {
                fogTimer = 0f;
                for (int i = 0; i < 9; i++)
                {
                    SpawnJealousyFogAura();
                }
            }

            yield return null;
        }

        if (myMove != null && !_isEXSkillActive)
        {
            myMove.skillSpeedMultiplier = 1.0f;
        }

        foreach (var kvp in originalColors)
        {
            if (kvp.Key != null)
            {
                kvp.Key.color = kvp.Value;
            }
        }
    }
    private void SpawnJealousyFogAura()
    {
        if (jealousyFogPrefab == null) return;

        Vector3 offset = new Vector3(
            Random.Range(-1.3f, 1.3f),
            Random.Range(-1.3f, 1.3f),
            0f
        );

        // 子オブジェクトにしない！ワールド座標で生成
        GameObject fog = Instantiate(
            jealousyFogPrefab,
            transform.position + offset,
            Quaternion.identity
        );

        // 霧の大きさを少しばらつかせる
        float scale = Random.Range(0.8f, 1.6f);
        fog.transform.localScale = Vector3.one * scale;

        // 霧の見た目を自機の付近に自然に寄せる
        SpriteRenderer sr = fog.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Color c = sr.color;
            c.a = 0.6f;
            sr.color = c;
        }
    }
    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        _isEXSkillActive = true;

        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();

        bool wasSpellActive = (myStatus != null && myStatus.isSpellCardActive);

        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();

        if (myMove != null)
        {
            myMove.skillSpeedMultiplier = (s.moveSpeedMultiplier > 0f) ? s.moveSpeedMultiplier : 0.7f;
        }

        BulletData mainData = s.bulletData;

        BulletData xData = s.bulletData;
        if (myStatus != null && myStatus.characterData != null)
        {
            var xSettings = myStatus.characterData.skillX;
            if (xSettings.bulletData != null)
            {
                xData = xSettings.bulletData;
            }
        }

        PlaySkillSE(SEPath.LASER7);

        float exDuration = wasSpellActive ? 4.5f : 2.5f;
        float elapsed = 0f;
        int frameCount = 0;

        try
        {
            if (pAnim != null) pAnim.TriggerSkillAnimation(s.skillName);

            while (elapsed < exDuration)
            {
                if (!PlayerMove.CanShoot)
                    break;

                float centerAngle = GetAngleToTarget(transform.position) + s.angleOffset;

                int waveCount = wasSpellActive ? 6 : 4;
                for (int wave = 0; wave < waveCount; wave++)
                {
                    float waveCenter = Random.Range(centerAngle - 60f, centerAngle + 60f);
                    float spread = wasSpellActive ? 48f : 40f;

                    float angle = waveCenter + Random.Range(-spread, spread);

                    if (Mathf.Abs(Mathf.DeltaAngle(angle, centerAngle)) < 12f)
                        continue;

                    CreateShot(
                        mainData,
                        transform.position,
                        s.speed > 0f ? s.speed : 6.5f,
                        angle + Random.Range(-12f, 12f),
                        delay: s.delay
                    );
                }

                if (frameCount % (wasSpellActive ? 10 : 20) == 0)
                {
                    PlaySkillSE(SEPath.SE_LGODS1);
                    FireCurvingOmnidirectionalShotWithData(
                        xData,
                        s,
                        0.6f,
                        40f,
                        120f,
                        wasSpellActive ? 18 : 12
                    );
                }

                frameCount++;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive)
                myMove.skillSpeedMultiplier = 1.0f;

            if (wasSpellActive && myStatus != null)
            {
                myStatus.DeactivateSpellCard(false);
            }

            _isEXSkillActive = false;
            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
        }
    }

    protected void FireCurvingOmnidirectionalShotWithData(
        BulletData data,
        PlayerSkillData.SkillSettings s,
        float minDecelRatio,
        float angularMin,
        float angularMax,
        int way = 8)
    {
        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();
        PlaySkillSE(s.sePath);

        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();
        if (pAnim != null) pAnim.TriggerSkillAnimation(s.skillName);

        float curveDirection = (Random.value < 0.5f) ? 1f : -1f;
        float curveIntensity = Random.Range(angularMin, angularMax);
        float setTargetAngularVelocity = curveDirection * curveIntensity;

        int wayCount = Mathf.Max(3, way);
        float angleStep = 360f / wayCount;
        float offsetBase = Random.Range(0f, 360f);

        for (int i = 0; i < wayCount; i++)
        {
            float finalAngle = offsetBase + (angleStep * i);

            DanmakuBullet bullet = CreateShot(data, transform.position, s.speed, finalAngle, delay: s.delay);

            if (bullet != null)
            {
                bullet.useXSkillCurve = true;
                bullet.xDecelStartFrame = 1;
                bullet.xMinSpeedRatio = minDecelRatio;
                bullet.xTargetAngularVelocity = setTargetAngularVelocity;
                bullet.xMaxCurveAngle = 30f;
            }
        }

        if (myMove != null)
        {
            myMove.AddUltimateEnergy(s.ultimateGain);
        }
    }
    protected IEnumerator SkillTempleteC(PlayerSkillData.SkillSettings s) { yield return null; }
    protected IEnumerator SkillTempleteEX(PlayerSkillData.SkillSettings s) { yield return null; }
}