using KanKikuchi.AudioManager;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Emitter_Gluttony : PlayerDanmakuEmitter
{
    private const int OPEN_SETTLE_FRAMES = 20;

    private float _gluttonyZChargeTime = 0f;
    private Vector3 _fangCenter;
    private float _fangAxisAngle;
    private float _fangHalfWidth = 1.5f;

    private float _leftWallAngle = 0f;
    private float _rightWallAngle = 0f;

    private readonly List<DanmakuBullet> _preparedJawBullets = new List<DanmakuBullet>();
    private bool _jawSetupBuilt = false;
    private bool _jawReleased = false;
    private int _openSettleFrameCounter = 0;
    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        _activeSkillCoroutines++;

        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();
        PlayerMove myMove = _rootOwner != null ? _rootOwner.GetComponent<PlayerMove>() : GetComponentInParent<PlayerMove>();

        if (myMove != null && !_isEXSkillActive)
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        PlayerAnimation pAnim = GetComponentInChildren<PlayerAnimation>();
        if (pAnim == null && _rootOwner != null) pAnim = _rootOwner.GetComponentInChildren<PlayerAnimation>();

        if (myStatus == null)
        {
            _activeSkillCoroutines--;
            yield break;
        }

        _preparedJawBullets.Clear();
        _jawSetupBuilt = false;
        _jawReleased = false;
        _openSettleFrameCounter = 0;

        _fangCenter = ResolveMouthCenter();
        _fangAxisAngle = Random.Range(0f, 360f);
        _fangHalfWidth = Random.Range(1.5f, 2.3f);

        ComputeFixedJawAngles();

        float maxCharge = 0.9f;

        try
        {
            while (true)
            {
                if (!PlayerMove.CanShoot)
                    break;

                if (myMove != null && myMove.currentEnergy < s.cost)
                    break;

                if (IsZButtonPressed(myStatus))
                {
                    _gluttonyZChargeTime += Time.deltaTime;
                    _gluttonyZChargeTime = Mathf.Min(_gluttonyZChargeTime, maxCharge);

                    if (!_jawSetupBuilt)
                    {
                        BuildJawSetup(s);
                        _jawSetupBuilt = true;
                    }

                    UpdateJawSetupDuringCharge();

                    yield return null;
                    continue;
                }

                break;
            }

            if (!_jawSetupBuilt)
            {
                yield break;
            }

            // ここから 20 フレーム待ち
            // これが終わるまでは、離しても閉じない
            while (_openSettleFrameCounter < OPEN_SETTLE_FRAMES)
            {
                _openSettleFrameCounter++;
                UpdateJawSetupDuringCharge();
                yield return null;
            }

            float chargeRatio = Mathf.Clamp01(_gluttonyZChargeTime / maxCharge);
            ReleaseJawBullets(s, chargeRatio);
            _jawReleased = true;

            if (myMove != null)
            {
                myMove.currentEnergy -= s.cost;
                myMove.AddUltimateEnergy(s.ultimateGain * 0.2f);
            }
        }
        finally
        {
            if (!_jawReleased)
            {
                CleanupPreparedBullets();
            }

            _preparedJawBullets.Clear();
            _gluttonyZChargeTime = 0f;
            _openSettleFrameCounter = 0;

            if (myMove != null && !_isEXSkillActive)
                myMove.skillSpeedMultiplier = 1.0f;

            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
        }
    }

    private void ComputeFixedJawAngles()
    {
        Vector3 axisDir = Quaternion.Euler(0f, 0f, _fangAxisAngle) * Vector3.right;
        Vector3 normalDir = new Vector3(-axisDir.y, axisDir.x, 0f);

        float jawHalfWidth = 2.4f;

        Vector3 leftWallCenter = _fangCenter + normalDir * jawHalfWidth * 1.1f;
        Vector3 rightWallCenter = _fangCenter - normalDir * jawHalfWidth * 1.1f;

        Vector3 leftMoveDir = (_fangCenter - leftWallCenter).normalized;
        Vector3 rightMoveDir = (_fangCenter - rightWallCenter).normalized;

        _leftWallAngle = Mathf.Atan2(leftMoveDir.y, leftMoveDir.x) * Mathf.Rad2Deg;
        _rightWallAngle = Mathf.Atan2(rightMoveDir.y, rightMoveDir.x) * Mathf.Rad2Deg;
    }

    private void BuildJawSetup(PlayerSkillData.SkillSettings s)
    {
        Vector3 axisDir = Quaternion.Euler(0f, 0f, _fangAxisAngle) * Vector3.right;
        Vector3 normalDir = new Vector3(-axisDir.y, axisDir.x, 0f);

        int count = 16;
        float alongSpan = 1.2f;
        float jawHalfWidth = 2.4f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / Mathf.Max(1, count - 1);
            float along = Mathf.Lerp(-alongSpan, alongSpan, t);

            Vector3 leftSource = _fangCenter + axisDir * along + normalDir * jawHalfWidth;
            Vector3 rightSource = _fangCenter + axisDir * along - normalDir * jawHalfWidth;

            DanmakuBullet leftBullet = CreateShot(
                s.bulletData,
                leftSource,
                0f,
                _leftWallAngle,
                delay: 0f
            );

            DanmakuBullet rightBullet = CreateShot(
                s.bulletData,
                rightSource,
                0f,
                _rightWallAngle,
                delay: 0f
            );

            if (leftBullet != null)
            {
                leftBullet.SetAngle(_leftWallAngle);
                leftBullet.BeginChargeDrift(1.8f, _leftWallAngle + 180f);
                leftBullet.transform.rotation = Quaternion.Euler(0f, 0f, _leftWallAngle - 90f);
                _preparedJawBullets.Add(leftBullet);
            }

            if (rightBullet != null)
            {
                rightBullet.SetAngle(_rightWallAngle);
                rightBullet.BeginChargeDrift(1.8f, _rightWallAngle + 180f);
                rightBullet.transform.rotation = Quaternion.Euler(0f, 0f, _rightWallAngle - 90f);
                _preparedJawBullets.Add(rightBullet);
            }
        }
    }

    private void UpdateJawSetupDuringCharge()
    {
        float chargeT = Mathf.Clamp01(_gluttonyZChargeTime / 0.5f);
        float driftSpeed = Mathf.Lerp(1.8f, 0f, chargeT);

        for (int i = 0; i < _preparedJawBullets.Count; i++)
        {
            var bullet = _preparedJawBullets[i];
            if (bullet == null) continue;

            bool isLeft = (i % 2 == 0);
            float angle = isLeft ? (_leftWallAngle + 180f) : (_rightWallAngle + 180f);

            bullet.SetRuntimeSpeedAndAngle(driftSpeed, angle);

            if (driftSpeed <= 0.02f)
            {
                bullet.StopAtZero();
            }
        }
    }

    private void ReleaseJawBullets(PlayerSkillData.SkillSettings s, float chargeRatio)
    {
        int releaseCount = _preparedJawBullets.Count;
        float speedMult = 1.2f;

        for (int i = 0; i < releaseCount; i++)
        {
            var bullet = _preparedJawBullets[i];
            if (bullet == null) continue;

            bool isLeft = (i % 2 == 0);
            float targetAngle = isLeft ? _leftWallAngle : _rightWallAngle;

            bullet.ReleaseMovement(s.speed * speedMult, targetAngle);
            bullet.transform.rotation = Quaternion.Euler(0f, 0f, targetAngle - 90f);

            float life = 0.37f;
            bullet.StartSelfDestructTimer(life);
        }

        _preparedJawBullets.Clear();
    }

    private void CleanupPreparedBullets()
    {
        for (int i = _preparedJawBullets.Count - 1; i >= 0; i--)
        {
            var b = _preparedJawBullets[i];
            if (b != null)
            {
                b.Deactivate(true, force: true);
            }
        }
    }

    private Vector3 ResolveMouthCenter()
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
            return target.position;
        }

        float targetAngle = GetAngleToTarget(transform.position);
        Vector3 dir = Quaternion.Euler(0f, 0f, targetAngle) * Vector3.right;
        return transform.position + dir * 2.5f;
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
        PlayerStatusManager myStatus = GetComponentInParent<PlayerStatusManager>();

        if (myMove != null && !_isEXSkillActive)
            myMove.skillSpeedMultiplier = s.moveSpeedMultiplier;

        if (myStatus == null)
        {
            _activeSkillCoroutines--;
            yield break;
        }

        float fireInterval = 0.08f;
        float maxCharge = 0.8f;
        float chargeTime = 0f;

        try
        {
            while (true)
            {
                if (!PlayerMove.CanShoot)
                    break;

                if (myMove != null && myMove.currentEnergy < s.cost)
                    break;

                if (!IsXButtonPressed(myStatus))
                {
                    break;
                }

                chargeTime += Time.deltaTime;
                chargeTime = Mathf.Min(chargeTime, maxCharge);


                // 長押し中は連続発射
                int shotCount = 6;
                float baseSpeed = 6.0f;

                Vector3 targetCenter = GetOpponentCenterOrFallback(myMove);
                for (int i = 0; i < shotCount; i++)
                {
                    Vector3 spawn = GetRandomTopSpawnPoint(targetCenter, 1.8f);
                    float dirAngle = -90f + Random.Range(-3f, 3f);
                    float speed = baseSpeed + Random.Range(-1.8f, 1.8f);

                    CreateShot(s.bulletData, spawn, speed, dirAngle, delay: 0f);
                }

                if (myMove != null)
                {
                    myMove.currentEnergy -= s.cost;
                    myMove.AddUltimateEnergy(s.ultimateGain * 0.15f);
                }

                // ここで長押し中に連射
                float elapsed = 0f;
                while (elapsed < fireInterval)
                {
                    elapsed += Time.deltaTime;

                    if (!IsXButtonPressed(myStatus))
                    {
                        yield break;
                    }

                    yield return null;
                }
            }
        }
        finally
        {
            if (myMove != null && !_isEXSkillActive)
                myMove.skillSpeedMultiplier = 1.0f;

            _activeSkillCoroutines--;
            if (_activeSkillCoroutines < 0) _activeSkillCoroutines = 0;
        }
    }
    private Vector3 GetOpponentCenterOrFallback(PlayerMove me)
    {
        if (me != null && me.Opponent != null)
        {
            return me.Opponent.transform.position;
        }

        foreach (var player in PlayerMove.AllPlayers)
        {
            if (player != null && player != me)
            {
                return player.transform.position;
            }
        }

        return Vector3.zero;
    }

    private Vector3 GetRandomTopSpawnPoint(Vector3 opponentCenter, float noFireWidth)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return new Vector3(Random.Range(-7f, 7f), 8f, 0f);
        }

        float z = Mathf.Abs(cam.transform.position.z);
        Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0f, 1f, z));
        Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1f, 1f, z));

        float minX = topLeft.x;
        float maxX = topRight.x;

        float safeMin = opponentCenter.x - noFireWidth;
        float safeMax = opponentCenter.x + noFireWidth;

        float x = 0f;
        int tries = 0;

        while (tries < 20)
        {
            x = Random.Range(minX, maxX);
            if (x < safeMin || x > safeMax)
            {
                break;
            }
            tries++;
        }

        if (tries >= 20)
        {
            x = Random.Range(minX, maxX);
        }

        float y = topLeft.y + 0.8f;
        return new Vector3(x, y, 0f);
    }

    private bool IsXButtonPressed(PlayerStatusManager status)
    {
        if (status == null || InputManager.Instance == null) return false;

        var inputSet = (status.playerId == 2) ? InputManager.Instance.player2 : InputManager.Instance.player1;
        if (inputSet.skillX != null && inputSet.skillX.action != null)
        {
            return inputSet.skillX.action.IsPressed();
        }

        return Input.GetKey(KeyCode.X);
    }
    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s) { yield return null; }
    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s) { yield return null; }
    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s) { yield return null; }
}