using KanKikuchi.AudioManager;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AppUI.UI;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// プレイヤーのスキル設定に基づき、実際に弾幕を生成・射出するクラス
/// 1vs1対戦対応：奇数弾は自機狙い、偶数弾は自機外しを自動計算
/// </summary>
public class PlayerDanmakuEmitter : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("攻撃対象（相手）のタグ")]
    public string targetTag = "Player";

    [Header("--- Fire Limit Settings ---")]
    [Tooltip("同時に実行を許可するスキルの最大コルーチン数（連射スキル等の緩和用）")]
    public int maxConcurrentSkills = 1;
    protected GameObject _rootOwner;
    protected bool _isArcReversed = false;
    protected int _activeSkillCoroutines = 0;
    public bool IsAnySkillActive => _activeSkillCoroutines > 0;
    protected bool _isEXSkillActive = false;

    public bool IsUltimateSkillActive => _isEXSkillActive;

    private static int _smallOrderCounter = 5000;
    private static int _mediumOrderCounter = 10000;
    private static int _largeOrderCounter = 15000;

    private int AllocateNextSortingOrder(BulletSize size)
    {
        switch (size)
        {
            case BulletSize.Small:
                _smallOrderCounter++;
                if (_smallOrderCounter > 20000) _smallOrderCounter = 15000;
                return _smallOrderCounter;

            case BulletSize.Medium:
                _mediumOrderCounter++;
                if (_mediumOrderCounter > 15000) _mediumOrderCounter = 10000;
                return _mediumOrderCounter;

            case BulletSize.Large:
                _largeOrderCounter++;
                if (_largeOrderCounter > 10000) _largeOrderCounter = 5000;
                return _largeOrderCounter;

            default:
                return 1000;
        }
    }

    private void Awake()
    {
        _rootOwner = transform.root.gameObject;
    }

    protected float GetAngleToTarget()
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

    protected float GetAngleToTarget(Vector3 fromPos)
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

    public bool CanFire(PlayerSkillData.SkillSettings s)
    {
        if (!enabled) return false;
        if (!PlayerMove.CanShoot) return false;

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return false;

        if (_isEXSkillActive) return false;

        if (s.isConcurrentAllowed || s.isChargeSkill)
        {
            return true;
        }

        if (_activeSkillCoroutines >= maxConcurrentSkills)
        {
            return false;
        }

        return true;
    }

    public void Fire(PlayerSkillData.SkillSettings s)
    {
        if (!enabled)
        {
            PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
            foreach (var emitter in allEmitters)
            {
                if (emitter != null && emitter.enabled)
                {
                    emitter.Fire(s);
                    return;
                }
            }
            return;
        }

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (!PlayerMove.CanShoot) return;
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;
        if (s.bulletData == null || s.bulletData.bulletPrefab == null) return;
        if (_isEXSkillActive && s.patternType != SkillPatternType.Line) return;

        // =========================================================================
        // 🎯 モード別の発射切り替え（ストーリーモード / 通常モード）
        // =========================================================================
        if (GameModeManager.IsStoryMode)
        {
            // ストーリーモード：自機の右方に弾幕を直進させる
            Vector3 spawnPos = transform.position + new Vector3(1.0f, 0f, 0f);
            CreateShot(
                data: s.bulletData,
                pos: spawnPos,
                speed: s.speed > 0f ? s.speed : 12f,
                angle: 0f,
                delay: 1.0f,
                isConverge: false,
                accel: 0f,
                maxSpeed: 0f,
                customMaterial: null,
                customScale: 1.0f,
                isIndestructible: false
            );
            PlaySkillSE(s.sePath);
            return;
        }
        else
        {
            // ストーリーモード以外：一発の自機狙い弾を出す
            float aimedAngle = GetAngleToTarget();
            ExecuteSubShot(
                data: s.bulletData,
                pos: transform.position,
                speed: s.speed > 0f ? s.speed : 8f,
                angle: aimedAngle,
                accel: 0f,
                maxSpeed: 0f,
                tag: targetTag,
                layer: gameObject.layer,
                delay_: 1.0f
            );
            PlaySkillSE(s.sePath);
            return;
        }
        // =========================================================================
    }

    public void FireEX(PlayerSkillData.SkillSettings s)
    {
        if (!enabled)
        {
            PlayerDanmakuEmitter[] allEmitters = GetComponents<PlayerDanmakuEmitter>();
            foreach (var emitter in allEmitters)
            {
                if (emitter != null && emitter.enabled)
                {
                    emitter.FireEX(s);
                    return;
                }
            }
            return;
        }

        if (!PlayerMove.CanShoot) return;

        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        if (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal) return;

        if (_isEXSkillActive) return;

        StartCoroutine(ExecuteEXInfrastructureRoutine(s));
    }

    protected IEnumerator ExecuteEXInfrastructureRoutine(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();

        if (myMove != null) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;
        PlaySkillSE(s.sePath);

        try
        {
            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isZoneActive = (myStatus != null && myStatus.isSpellCardActive);

            PlayerSkillData.SkillSettings enhancedEXSettings = s;

            if (isZoneActive)
            {
                enhancedEXSettings.speed = s.speed * 1.3f;
                s = enhancedEXSettings;
            }

            yield return StartCoroutine(ExecuteSkillEX(s));
        }
        finally
        {
            if (myMove != null) myMove.skillSpeedMultiplier = 1.0f;
            _activeSkillCoroutines--;
        }
        yield return null;
    }

    protected virtual IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s) { yield return null; }
    protected virtual IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s) { yield return null; }

    public void ExecuteSubShot(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer, float delay_ = 0f)
    {
        if (data == null || data.bulletPrefab == null) return;

        if (delay_ < 1) { delay_ = 1; }

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.1f);
        }

        CreateShot(
            data: data,
            pos: pos,
            speed: speed,
            angle: angle,
            delay: delay_,
            isConverge: false,
            accel: accel,
            maxSpeed: maxSpeed,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: false
        );
    }

    public void ExecuteSubShot02(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer, float angularVelocity = 0f, float maxRotationLimit = 0f)
    {
        if (data == null || data.bulletPrefab == null) return;

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.1f);
        }

        CreateShot(
            data: data,
            pos: pos,
            speed: speed,
            angle: angle,
            delay: 0,
            isConverge: false,
            accel: accel,
            maxSpeed: maxSpeed,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: true,
            angularVelocity: angularVelocity,
            maxRotationLimit: maxRotationLimit
        );
    }

    public DanmakuBullet ExecuteSubShot02_Returnable(BulletData data, Vector3 pos, float speed, float angle, float accel, float maxSpeed, string tag, int layer, float angularVelocity = 0f, float maxRotationLimit = 0f)
    {
        if (data == null || data.bulletPrefab == null) return null;

        if (SEManager.Instance != null)
        {
            SEManager.Instance.Play(SEPath.SHOT2, 0.1f);
        }

        return CreateShot(
            data: data,
            pos: pos,
            speed: speed,
            angle: angle,
            delay: 0f,
            isConverge: false,
            accel: accel,
            maxSpeed: maxSpeed,
            customMaterial: null,
            customScale: 1.0f,
            isIndestructible: true,
            angularVelocity: angularVelocity,
            maxRotationLimit: maxRotationLimit
        );
    }

    protected void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    protected IEnumerator ExecuteStreamLaserInfrastructure(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;
        PlayerHitHandler myHH = GetComponentInChildren<PlayerHitHandler>();
        PlayerMove myMove = _rootOwner.GetComponent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        try
        {
            int laserIntervalFrames = 1;
            int totalBulletSegments = Mathf.Max(10, s.count);
            float laserWidthScale = (s.wideAngle > 0f) ? s.wideAngle : 1.0f;

            PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
            bool isSpellActive = (myStatus != null && myStatus.isSpellCardActive);

            int wayCount = isSpellActive ? 5 : 3;
            float spreadAngle = 24f;

            Material additiveMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            additiveMaterial.hideFlags = HideFlags.DontSave;

            Vector3 spawnOrigin = transform.position;

            for (int f = 0; f < totalBulletSegments; f++)
            {
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                    break;

                if (f % 4 == 0) PlaySkillSE(s.sePath);

                float currentTargetAngle = GetAngleToTarget(spawnOrigin);
                float currentBaseAngle = currentTargetAngle + s.angleOffset;

                float startAngle = currentBaseAngle - (spreadAngle / 2f);
                float stepAngle = spreadAngle / (wayCount - 1);

                for (int w = 0; w < wayCount; w++)
                {
                    float finalLaserAngle = startAngle + (stepAngle * w);

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

    protected DanmakuBullet CreateShot(BulletData data, Vector3 pos, float speed, float angle, float delay,
                                       bool isConverge = false, float accel = 0f, float maxSpeed = 0f,
                                       Material customMaterial = null, float customScale = 1.0f,
                                       bool isIndestructible = false,
                                       float angularVelocity = 0f, float maxRotationLimit = 0f)
    {
        if (data == null || data.bulletPrefab == null) return null;

        if (delay < 1.0f) { delay = 1.0f; }

        BulletData runtimeData = Instantiate(data);
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        if (myStatus != null && myStatus.characterData != null)
        {
            float atkMultiplier = 1.0f;
            switch (myStatus.characterData.rankAttack)
            {
                case StatusRank.E: atkMultiplier = 0.8f; break;
                case StatusRank.D: atkMultiplier = 0.9f; break;
                case StatusRank.C: atkMultiplier = 1.0f; break;
                case StatusRank.B: atkMultiplier = 1.1f; break;
                case StatusRank.A: atkMultiplier = 1.2f; break;
                case StatusRank.EX: atkMultiplier = 1.3f; break;
            }
            if (myStatus.IsAttackBoostActive) atkMultiplier *= 1.3f;
            atkMultiplier *= myStatus.GetJealousyMultiplier();
            runtimeData.damage = Mathf.RoundToInt(runtimeData.damage * atkMultiplier);
        }
        bool isSpear = (runtimeData != null && (runtimeData.name.Contains("Spear") || runtimeData.bulletPrefab.name.Contains("Spear")));

        Quaternion initialRotation = isSpear && delay > 0f
            ? Quaternion.Euler(90f, 0f, angle - 90f)
            : Quaternion.Euler(0f, 0f, angle - 90f);

        GameObject obj = null;
        if (BulletPool.Instance != null)
        {
            obj = BulletPool.Instance.Get(data.bulletPrefab, pos, initialRotation);
        }
        else
        {
            obj = Instantiate(data.bulletPrefab, pos, initialRotation);
        }

        if (obj == null) return null;

        DanmakuBullet bullet = obj.GetComponent<DanmakuBullet>();
        if (bullet != null)
        {
            float finalMaxSpeed = (maxSpeed == 0f) ? speed : maxSpeed;

            bullet.Initialize(
                shooter: _rootOwner,
                target: targetTag,
                speed: speed,
                angle: angle,
                accel: accel,
                maxSpeed: finalMaxSpeed,
                angVel: angularVelocity,
                delay: delay,
                data: runtimeData,
                converge: isConverge
            );

            bullet.isIndestructible = isIndestructible;
        }

        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        obj.tag = assignedTag;

        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        obj.layer = assignedLayer;
        SetLayerRecursive(obj, assignedLayer);

        float finalBulletScale = runtimeData.bulletScale * customScale;
        obj.transform.localScale = new Vector3(finalBulletScale, finalBulletScale, 1.0f);

        if (customMaterial != null)
        {
            SpriteRenderer mainSR = obj.GetComponentInChildren<SpriteRenderer>();
            if (mainSR != null) mainSR.material = customMaterial;
        }

        obj.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        return bullet;
    }

    protected EnemyLaserBeam CreateLaserShot(BulletData data, Vector3 pos, float speed, int count, float wideAngle, int warningFrame, bool isSetupB = false)
    {
        if (BulletManager.Instance == null || data == null || data.bulletPrefab == null) return null;

        BulletData runtimeData = Instantiate(data);
        BulletManager.LaserColor color = runtimeData.laserColor;
        var laserSet = BulletManager.Instance.GetLaserSet(color);

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        if (myStatus == null && _rootOwner != null) myStatus = _rootOwner.GetComponent<PlayerStatusManager>();
        int ownerId = (myStatus != null) ? myStatus.playerId : 1;

        int finalLaserDamage = runtimeData.damage;
        if (myStatus != null && myStatus.characterData != null)
        {
            float atkMultiplier = 1.0f;
            switch (myStatus.characterData.rankAttack)
            {
                case StatusRank.E: atkMultiplier = 0.8f; break;
                case StatusRank.D: atkMultiplier = 0.9f; break;
                case StatusRank.C: atkMultiplier = 1.0f; break;
                case StatusRank.B: atkMultiplier = 1.1f; break;
                case StatusRank.A: atkMultiplier = 1.2f; break;
                case StatusRank.EX: atkMultiplier = 1.3f; break;
            }

            if (myStatus.IsAttackBoostActive) atkMultiplier *= 1.3f;
            atkMultiplier *= myStatus.GetJealousyMultiplier();
            finalLaserDamage = Mathf.RoundToInt(finalLaserDamage * atkMultiplier);
        }

        GameObject laserObj = Instantiate(BulletManager.Instance.laserBeamPrefab, pos, Quaternion.identity);

        string assignedTag = (ownerId == 1) ? "PlayerBullet" : "EnemyBullet";
        laserObj.tag = assignedTag;

        int assignedLayer = LayerMask.NameToLayer((ownerId == 1) ? "Player1Bullet" : "Player2Bullet");
        laserObj.layer = assignedLayer;
        SetLayerRecursive(laserObj, assignedLayer);

        EnemyLaserBeam laser = laserObj.GetComponent<EnemyLaserBeam>();
        if (laser != null)
        {
            if (isSetupB)
            {
                laser.SetupB(_rootOwner, targetTag, finalLaserDamage, pos.x, pos.y, count, wideAngle, color, warningFrame,
                             BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite, runtimeData);
            }
            else
            {
                laser.SetupA(_rootOwner, targetTag, finalLaserDamage, pos.x, pos.y, count, wideAngle, color, warningFrame,
                             BulletManager.Instance.laserSourceEffectPrefab, laserSet.sourceEffectSprite, runtimeData);
            }
        }

        return laser;
    }

    private void RestoreSpeedSafety(PlayerMove myMove)
    {
        if (myMove == null) return;
        if (_isEXSkillActive) return;
        myMove.skillSpeedMultiplier = 1.0f;
    }

    private IEnumerator TemporarySlow(float multiplier, float duration)
    {
        PlayerMove myMove = (_rootOwner != null) ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();

        if (myMove != null) myMove.skillSpeedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);

        RestoreSpeedSafety(myMove);
    }

    protected void PlaySkillSE(string path)
    {
        string clip = string.IsNullOrEmpty(path) ? SEPath.SHOT1 : path;
        if (SEManager.Instance != null)
            SEManager.Instance.Play(clip, 0.4f);
    }
}