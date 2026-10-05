using KanKikuchi.AudioManager;
using System.Collections;
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
    /// 💤 3段階チャージ式・同時射出速度差ウェーブ弾幕ルーチン（2段階目を少し強化）
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

        GameObject indicatorObj = new GameObject("SlothAimIndicator");
        MeshFilter meshFilter = indicatorObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = indicatorObj.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material.color = new Color(0.3f, 0.6f, 1f, 0.4f);

        SpriteRenderer mySR = GetComponent<SpriteRenderer>();
        if (mySR == null) mySR = GetComponentInParent<SpriteRenderer>();
        if (mySR == null) mySR = GetComponentInChildren<SpriteRenderer>();
        if (mySR != null) { meshRenderer.sortingLayerID = mySR.sortingLayerID; meshRenderer.sortingOrder = 14900; }

        Mesh reusableMesh = new Mesh();

        try
        {
            PlaySkillSE(s.sePath);

            float fixedBaseAngle = GetAngleToTarget(transform.position) + s.angleOffset;
            float spreadAngle = 60f;

            int elapsedFrames = 0;
            bool isKeyReleased = false;

            int tier1Frames = 25;
            int tier2Frames = 55;
            int maxChargeFrames = 90;
            int currentTier = 1;

            while (!isKeyReleased)
            {
                if (!PlayerMove.CanShoot || (myHH != null && myHH.currentState != PlayerHitHandler.PlayerState.Normal))
                {
                    isKeyReleased = true;
                    break;
                }

                if (myMove != null && !_isEXSkillActive) myMove.skillSpeedMultiplier = chargeMoveSpeed;
                if (indicatorObj != null) indicatorObj.transform.position = transform.position;

                if (elapsedFrames < tier1Frames) currentTier = 1;
                else if (elapsedFrames < tier2Frames) currentTier = 2;
                else currentTier = 3;

                float currentRadius = 1.5f + (currentTier * 0.4f);
                DrawFanMesh(meshFilter, reusableMesh, spreadAngle, fixedBaseAngle, currentRadius);

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

            if (indicatorObj != null) Destroy(indicatorObj);
            PlaySkillSE(SEPath.SHOT2);

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
                // 💡 段階2：広がり係数を `1.2f` に指定して発射
                int[] waysTier2 = new int[] { 2, 3, 4 };
                float[] speedsTier2 = new float[] { baseSpeed * 1.0f, baseSpeed * 0.8f, baseSpeed *0.6f };
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
            if (indicatorObj != null) Destroy(indicatorObj);
            if (reusableMesh != null) Destroy(reusableMesh);
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

    protected IEnumerator SkillTempleteX(PlayerSkillData.SkillSettings s)
    {
        yield return null;
    }

    protected IEnumerator SkillTempleteC(PlayerSkillData.SkillSettings s)
    {
        yield return null;
    }

    private IEnumerator SkillTempleteV(PlayerSkillData.SkillSettings s)
    {
        yield return null;
    }

    protected IEnumerator SkillTempleteEX(PlayerSkillData.SkillSettings s)
    {
        yield return null;
    }
}