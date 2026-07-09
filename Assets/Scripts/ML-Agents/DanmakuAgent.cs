// --- DanmakuAgent.cs 難易度動変調インフラ・Easy領域暴発・領域返しNormal選別・再装填難易度可変・コンパイルエラー完全根治版 ---
using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================================
// 🌐【重要】：ゲーム内難易度を管理する列挙型と静的クラスインフラ
// =========================================================================
public enum GameDifficulty
{
    Easy,
    Normal,
    Hard,
    Lunatic
}

public static class GameDifficultyManager
{
    // 🎯 デフォルトは制限を完全に撤廃した「Lunatic」モードで駆動
    public static GameDifficulty CurrentDifficulty = GameDifficulty.Lunatic;

    // 🎯【1P自動操縦フラグ】：Cキーで制御される自機AI化フラグ
    public static bool IsP1AutoAiDebugMode = false;

    // 🎯【新規追加：エンドレスモードフラグ】：Eキーで制御される勝ち星カウントストップフラグ
    public static bool IsEndlessMode = false;
}

public class DanmakuAgent : Agent
{
    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler;
    private SkillManager skillManager;
    private PlayerStatusManager statusManager;
    [SerializeField] private Transform opponent;
    public int playerID = 1;

    [Header("AI Evade Settings (Rule-Based)")]
    [SerializeField] private float _detectionRadius = 3.5f;
    public bool _useAutoEvadeAI = false;

    [Header("🎯 ML-Agents Training Settings (しの字・つの字対策空間)")]
    [Tooltip("3方向以上を弾に囲まれた（袋小路に入った）際に、毎フレーム与えるペナルティの最大値（マイナス値で指定）")]
    [SerializeField] private float _deadEndFramePenalty = -0.015f;

    private Vector3 _initialPosition;
    private float _timeSinceMatchEnd = 0f;

    // ヒステリシス射撃管理用
    private enum ShootingState { Charging, Bursting }
    private ShootingState _currentShootingState = ShootingState.Bursting;
    private float _mpReadyThreshold = 80f;
    private float _mpSaveThreshold = 25f;

    [Header("Input System Actions")]
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction skillZAction;
    [SerializeField] private InputAction skillXAction;
    [SerializeField] private InputAction skillCAction;
    [SerializeField] private InputAction skillVAction;
    [SerializeField] private InputAction slowAction;

    // 🦥【新規追加】：チョン避け（一方向引き付け）慣性ホールド用の内部ワーク変数
    private float _chonTimer = 0f;
    private Vector2 _chonLockedDirection = Vector2.zero;

    // 🌟【デモ用新設】：EXスキル（ULT）と領域展開（VJT）のAI使用を完全に禁止するスイッチ
    [SerializeField] private bool _disableEXAndVJTForDemo = false;

    // =========================================================================
    // ⏳【新規追加】：AI専用の「リキャストとは別の使用間（手加減タイマー）」
    // =========================================================================
    private float _aiSkillIntervalTimer = 0f;
    private int skipFrameTimer = 0; // フレーム間引き用のカウントダウンタイマー

    // 📐 【新設：AI専用マルチ通常スキルチャージインフラマネージャー】
    // 💡 0: チャージなし, 1: Zスキルチャージ中
    private int _aiCurrentChargingSkillSlot = 0;
    private int _aiChargeFrameTimer = 0;
    private const int AI_GENERIC_CHARGE_TARGET_FRAMES = 62; // 💡 基準の最大溜めターゲット（約1.0秒）

    public override void Initialize()
    {
        playerMove = GetComponent<PlayerMove>();
        hitHandler = GetComponentInChildren<PlayerHitHandler>();
        skillManager = GetComponent<SkillManager>();
        statusManager = GetComponent<PlayerStatusManager>();
        _initialPosition = transform.position;

        if (playerID == 2)
        {
            _useAutoEvadeAI = GameSelectionData.UseAutoEvadeAI;
            Debug.Log($"<color=magenta>🤖【AI同期】2Pエージェント生成。自動回避AI状態: {_useAutoEvadeAI}</color>");
        }
        else
        {
            // =========================================================================
            // 🎯【ピンポイントバグ修正】：1P自機のEキーデバッグ自動操縦のバトルシーン完全同期
            // 💡 理由：キャラ選択画面で蓄積した「IsP1AutoAiDebugMode」が真なら、
            //          手動入力を強制遮断して、自機自身の知性をAI自動回避モードへ最優先で上書きします！
            // =========================================================================
            if (GameDifficultyManager.IsP1AutoAiDebugMode)
            {
                _useAutoEvadeAI = true;
                Debug.Log("<color=lime>🛠️【Debug自動操縦】1P自機のAI自動操縦（Eキー）がバトルシーンに100%完全同期されました！</color>");
            }
            else if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer ||
                     GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom)
            {
                _useAutoEvadeAI = false;
            }
        }

        _aiCurrentChargingSkillSlot = 0;
        _aiChargeFrameTimer = 0;
        _aiSkillIntervalTimer = 0f;
        skipFrameTimer = 0;
    }

    void FixedUpdate()
    {
        // =========================================================================
        // 📊【難易度マトリクス・危険デテクション領域（探索半径）の動的変調】
        // =========================================================================
        switch (GameDifficultyManager.CurrentDifficulty)
        {
            case GameDifficulty.Easy: _detectionRadius = 1.5f; break;
            case GameDifficulty.Normal: _detectionRadius = 2.5f; break;
            case GameDifficulty.Hard: _detectionRadius = 3.5f; break;
            case GameDifficulty.Lunatic: _detectionRadius = 5.0f; break;
        }

        // 手加減用タイマーの進行
        if (_aiSkillIntervalTimer > 0f)
        {
            _aiSkillIntervalTimer -= Time.fixedDeltaTime;
        }

        // 間引きタイマーの減算
        if (skipFrameTimer > 0)
        {
            skipFrameTimer--;
        }

        // 🚨 試合終了時やカウントダウン中、被弾中など、動けない時は入力をクリア
        if (!PlayerMove.CanShoot)
        {
            _timeSinceMatchEnd += Time.fixedDeltaTime;
            if (playerMove != null) playerMove.currentFrameInput = new PlayerMove.ReplayFrame();

            if (_useAutoEvadeAI)
            {
                if (_timeSinceMatchEnd < 1.5f) transform.position += GetPerlinWanderVector(Time.time, 0.015f);
                else
                {
                    if (Vector3.Distance(transform.position, _initialPosition) > 0.1f)
                        transform.position = Vector3.MoveTowards(transform.position, _initialPosition, 2.5f * Time.fixedDeltaTime);
                    transform.position += GetPerlinWanderVector(Time.time, 0.025f);
                }
            }
            return;
        }
        _timeSinceMatchEnd = 0f;

        RequestAction();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        if (opponent != null)
        {
            sensor.AddObservation(opponent.localPosition);
            Rigidbody2D oppRb = opponent.GetComponent<Rigidbody2D>();
            sensor.AddObservation(oppRb != null ? oppRb.linearVelocity : Vector2.zero);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector2.zero);
        }

        if (playerMove != null)
        {
            sensor.AddObservation(playerMove.currentEnergy / playerMove.maxEnergy);
            sensor.AddObservation(playerMove.ultimateEnergy);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        float wallX = 4.0f;
        float wallY = 4.5f;
        float cornerX = Mathf.Clamp01((Mathf.Abs(transform.localPosition.x) - 2.5f) / (wallX - 2.5f));
        float cornerY = Mathf.Clamp01((Mathf.Abs(transform.localPosition.y) - 3.0f) / (wallY - 3.0f));
        sensor.AddObservation(cornerX);
        sensor.AddObservation(cornerY);

        string targetBulletTag = (playerID == 1) ? "EnemyBullet" : "PlayerBullet";
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, _detectionRadius);

        int bulletCount = 0;
        Vector2 bulletCenterGroup = Vector2.zero;
        Vector2 bulletAverageVelocity = Vector2.zero;

        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            if (col.CompareTag(targetBulletTag) || col.CompareTag("Laser"))
            {
                Vector2 relativePos = col.transform.position - transform.position;
                bulletCenterGroup += relativePos;
                bulletCount++;

                if (col.attachedRigidbody != null)
                {
                    bulletAverageVelocity += col.attachedRigidbody.linearVelocity;
                }
            }
        }

        if (bulletCount > 0)
        {
            bulletCenterGroup /= bulletCount;
            bulletAverageVelocity /= bulletCount;
            sensor.AddObservation(bulletCenterGroup);
            sensor.AddObservation(bulletAverageVelocity);
        }
        else
        {
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(Vector2.zero);
        }

        if (PlayerMove.CanShoot)
        {
            AddReward(0.0005f);
            EvaluateDeadEndSurroundingPenalty();
        }
    }

    private void EvaluateDeadEndSurroundingPenalty()
    {
        string targetBulletTag = (playerID == 1) ? "EnemyBullet" : "PlayerBullet";
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, _detectionRadius);

        int leftCount = 0; int rightCount = 0; int upCount = 0; int downCount = 0;

        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            if (col.CompareTag(targetBulletTag) || col.CompareTag("Laser"))
            {
                Vector2 relativePos = col.transform.position - transform.position;
                if (relativePos.x < -0.3f) leftCount++;
                if (relativePos.x > 0.3f) rightCount++;
                if (relativePos.y > 0.3f) upCount++;
                if (relativePos.y < -0.3f) downCount++;
            }
        }

        float wallX = 4.0f; float wallY = 4.5f;
        bool isCornered = (Math.Abs(transform.localPosition.x) > wallX - 1.5f) &&
                          (Math.Abs(transform.localPosition.y) > wallY - 1.5f);

        if (isCornered && (leftCount + rightCount + upCount + downCount) >= 4)
        {
            AddReward(_deadEndFramePenalty);
        }

        if (isCornered && playerMove != null)
        {
            if (transform.localPosition.x < 0 && transform.localPosition.y < 0)
            {
                if (playerMove.currentFrameInput.v > 0 || playerMove.currentFrameInput.h > 0) AddReward(0.005f);
            }
            else if (transform.localPosition.x > 0 && transform.localPosition.y < 0)
            {
                if (playerMove.currentFrameInput.v > 0 || playerMove.currentFrameInput.h < 0) AddReward(0.005f);
            }
        }
    }

    public void GiveDamageReward() => AddReward(0.3f);
    public void GiveGrazeReward() => AddReward(0.05f);
    public void GiveHitPenalty() => AddReward(-0.5f);

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!PlayerMove.CanInput || (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal))
        {
            if (playerMove != null) playerMove.currentFrameInput = new PlayerMove.ReplayFrame();
            return;
        }

        var discrete = actions.DiscreteActions;

        float h = 0, v = 0;
        if (discrete[0] == 1) h = -1; else if (discrete[0] == 2) h = 1;
        if (discrete[1] == 1) v = 1; else if (discrete[1] == 2) v = -1;

        int attackAction = discrete[2];

        // 🧠【強化学習専用ハッキング＆温存レール】
        if (Unity.MLAgents.Academy.Instance.IsCommunicatorOn)
        {
            float currentUltGauge = (playerMove != null) ? playerMove.ultimateEnergy : 0f;
            bool isMyVjtActive = (statusManager != null && statusManager.isSpellCardActive);

            if (!isMyVjtActive && currentUltGauge >= 200f && !statusManager.isOverheated)
            {
                if (attackAction >= 1 && attackAction <= 5) attackAction = 6;
            }
            else if (attackAction == 5)
            {
                if (isMyVjtActive)
                {
                    if (!IsVjtCancelAllowed()) attackAction = 0;
                }
                else
                {
                    if (currentUltGauge >= 100f && currentUltGauge < 200f)
                    {
                        AddReward(-0.02f);
                        attackAction = 0;
                    }
                }
            }
        }

        bool autoSlowToggle = (discrete[3] == 1);

        if (skipFrameTimer > 0 && _useAutoEvadeAI)
        {
            if (playerMove != null)
            {
                PlayerMove.ReplayFrame keptInput = playerMove.currentFrameInput;
                UpdateAttackFrameAction(attackAction, autoSlowToggle, ref keptInput);
                playerMove.currentFrameInput = keptInput;
            }
            return;
        }
        else
        {
            if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Easy) skipFrameTimer = 15;
            else if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Normal) skipFrameTimer = 6;
            else skipFrameTimer = 0;
        }

        PlayerMove.ReplayFrame frameInput = new PlayerMove.ReplayFrame
        {
            h = h,
            v = v,
            slow = autoSlowToggle
        };

        if (_aiCurrentChargingSkillSlot > 0)
        {
            _aiChargeFrameTimer++;
            if (attackAction == 1 && _aiChargeFrameTimer < AI_GENERIC_CHARGE_TARGET_FRAMES)
            {
                if (_aiCurrentChargingSkillSlot == 1) frameInput.shotZ = true;
            }
            else
            {
                if (_aiCurrentChargingSkillSlot == 1) frameInput.shotZ = false;
                _aiCurrentChargingSkillSlot = 0;
                _aiChargeFrameTimer = 0;
            }
        }
        else
        {
            UpdateAttackFrameAction(attackAction, autoSlowToggle, ref frameInput);

            if (attackAction == 1 && statusManager != null && statusManager.characterData != null)
            {
                if (statusManager.characterData.skillZ.isChargeSkill)
                {
                    _aiCurrentChargingSkillSlot = 1;
                    _aiChargeFrameTimer = 1;
                    frameInput.shotZ = true;
                }
            }
        }

        playerMove.currentFrameInput = frameInput;

        if (attackAction == 6 && PlayerMove.CanShoot && statusManager != null && !statusManager.isSpellCardActive)
        {
            if (playerMove != null && playerMove.ultimateEnergy >= 200f && !statusManager.isOverheated)
            {
                statusManager.ActivateSpellCard();
                if (Unity.MLAgents.Academy.Instance.IsCommunicatorOn) AddReward(0.5f);
            }
        }
    }

    private void UpdateAttackFrameAction(int attackAction, bool autoSlow, ref PlayerMove.ReplayFrame frame)
    {
        frame.shotZ = (attackAction == 1);
        frame.shotX = (attackAction == 2);
        frame.shotC = (attackAction == 3);
        frame.shotV = (attackAction == 4);
        frame.ultimate = (attackAction == 5);
        frame.slow = autoSlow;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!PlayerMove.CanInput || (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal)) return;

        var discrete = actionsOut.DiscreteActions;
        discrete.Clear();

        if (_useAutoEvadeAI)
        {
            Vector2 evadeVector = CalculatePotentialEvadeDirection();
            if (evadeVector.x < -0.15f) discrete[0] = 1; else if (evadeVector.x > 0.15f) discrete[0] = 2;
            if (evadeVector.y > 0.15f) discrete[1] = 1; else if (evadeVector.y < -0.15f) discrete[1] = 2;

            discrete[2] = EvaluateAndSelectTacticalSkill();
            return;
        }

        if (InputManager.Instance == null) return;
        var inputSet = (playerID == 1) ? InputManager.Instance.player1 : InputManager.Instance.player2;

        Vector2 m = inputSet.move.action.ReadValue<Vector2>();
        const float STICK_DEADZONE = 0.35f;

        if (m.magnitude > STICK_DEADZONE)
        {
            float angleDeg = Mathf.Atan2(m.y, m.x) * Mathf.Rad2Deg;

            if (angleDeg >= -22.5f && angleDeg < 22.5f) discrete[0] = 2;
            else if (angleDeg >= 22.5f && angleDeg < 67.5f) { discrete[0] = 2; discrete[1] = 1; }
            else if (angleDeg >= 67.5f && angleDeg < 112.5f) discrete[1] = 1;
            else if (angleDeg >= 112.5f && angleDeg < 157.5f) { discrete[0] = 1; discrete[1] = 1; }
            else if (angleDeg >= 157.5f || angleDeg < -157.5f) discrete[0] = 1;
            else if (angleDeg >= -157.5f && angleDeg < -112.5f) { discrete[0] = 1; discrete[1] = 2; }
            else if (angleDeg >= -112.5f && angleDeg < -67.5f) discrete[1] = 2;
            else if (angleDeg >= -67.5f && angleDeg < -22.5f) { discrete[0] = 2; discrete[1] = 2; }
        }
        else
        {
            discrete[0] = 0; discrete[1] = 0;
        }

        bool isCPressed = (inputSet.skillC != null && inputSet.skillC.action != null) && inputSet.skillC.action.IsPressed();
        bool isVPressed = (inputSet.skillV != null && inputSet.skillV.action != null) && inputSet.skillV.action.IsPressed();

        bool pEX = false;
        if (inputSet.skillEX != null && inputSet.skillEX.action != null) pEX = inputSet.skillEX.action.IsPressed();
        else pEX = (isCPressed && isVPressed);

        if (pEX) discrete[2] = 5;
        else
        {
            if (inputSet.skillZ != null && inputSet.skillZ.action != null && inputSet.skillZ.action.IsPressed()) discrete[2] = 1;
            else if (inputSet.skillX != null && inputSet.skillX.action != null && inputSet.skillX.action.IsPressed()) discrete[2] = 2;
            else if (isCPressed) discrete[2] = 3;
            else if (isVPressed) discrete[2] = 4;
        }

        if (inputSet.slow != null && inputSet.slow.action != null && inputSet.slow.action.IsPressed()) discrete[3] = 1;
    }

    private bool IsVjtCancelAllowed()
    {
        if (statusManager == null || !statusManager.isSpellCardActive) return true;

        float vjtHpRatio = (statusManager.spellMaxHP > 0f) ? (statusManager.spellHP / statusManager.spellMaxHP) : 0f;
        float vjtRemainingTime = statusManager.spellTimer;

        if (vjtHpRatio <= 0.25f || vjtRemainingTime <= 1.5f) return true;
        return false;
    }

    private Vector2 CalculatePotentialEvadeDirection()
    {
        Vector2 totalRepulsion = Vector2.zero;
        string targetBulletTag = (playerID == 1) ? "EnemyBullet" : "PlayerBullet";

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, _detectionRadius);
        bool hasDanger = false;

        int myBulletLayer = LayerMask.NameToLayer(playerID == 1 ? "Player1Bullet" : "Player2Bullet");

        float wallBound = 9.0f;
        float padding = 1.5f;

        bool isNearRightWall = transform.position.x > (wallBound - padding);
        bool isNearLeftWall = transform.position.x < (-wallBound + padding);
        bool isNearTopWall = transform.position.y > (wallBound - padding);
        bool isNearBottomWall = transform.position.y < (-wallBound + padding);

        var singleBullets = new System.Collections.Generic.List<Collider2D>();
        var clusterPoints = new System.Collections.Generic.List<Vector2>();
        var analyzedCounted = new System.Collections.Generic.HashSet<Collider2D>();

        foreach (var col in hitColliders)
        {
            if (col == null || !col.CompareTag(targetBulletTag) || analyzedCounted.Contains(col)) continue;

            var nearbyBulletsInGroup = new System.Collections.Generic.List<Collider2D>();
            nearbyBulletsInGroup.Add(col);

            foreach (var other in hitColliders)
            {
                if (other == null || other == col || !other.CompareTag(targetBulletTag) || analyzedCounted.Contains(other)) continue;

                if (Vector2.Distance(col.transform.position, other.transform.position) < 1.2f)
                {
                    nearbyBulletsInGroup.Add(other);
                }
            }

            if (nearbyBulletsInGroup.Count >= 4)
            {
                Vector2 groupCenter = Vector2.zero;
                foreach (var b in nearbyBulletsInGroup)
                {
                    groupCenter += (Vector2)b.transform.position;
                    analyzedCounted.Add(b);
                }
                groupCenter /= nearbyBulletsInGroup.Count;
                clusterPoints.Add(groupCenter);
            }
            else
            {
                singleBullets.Add(col);
            }
        }

        foreach (Vector2 clusterPos in clusterPoints)
        {
            Vector2 directionFromCluster = (Vector2)transform.position - clusterPos;
            float distance = directionFromCluster.magnitude;

            if (distance < 0.05f) continue;
            hasDanger = true;

            float force = 4.5f / Mathf.Max(0.5f, distance);
            Vector2 clusterRepulsion = directionFromCluster.normalized * force;

            if (isNearRightWall && clusterRepulsion.x > 0) { clusterRepulsion.y += (clusterRepulsion.y >= 0 ? 1f : -1f) * clusterRepulsion.x; clusterRepulsion.x = 0; }
            else if (isNearLeftWall && clusterRepulsion.x < 0) { clusterRepulsion.y += (clusterRepulsion.y >= 0 ? 1f : -1f) * Mathf.Abs(clusterRepulsion.x); clusterRepulsion.x = 0; }
            if (isNearTopWall && clusterRepulsion.y > 0) { clusterRepulsion.x += (clusterRepulsion.x >= 0 ? 1f : -1f) * clusterRepulsion.y; clusterRepulsion.y = 0; }
            else if (isNearBottomWall && clusterRepulsion.y < 0) { clusterRepulsion.x += (clusterRepulsion.x >= 0 ? 1f : -1f) * Mathf.Abs(clusterRepulsion.y); clusterRepulsion.y = 0; }

            totalRepulsion += clusterRepulsion;
        }

        foreach (var col in singleBullets)
        {
            if (col == null) continue;
            Vector2 directionFromBullet = (Vector2)transform.position - (Vector2)col.transform.position;
            float distance = directionFromBullet.magnitude;
            if (distance < 0.05f) continue;

            hasDanger = true;

            float force = 1.0f / (distance * distance);
            Vector2 bulletRepulsion = directionFromBullet.normalized * force;
            Vector2 finalBulletForce = bulletRepulsion;

            if (col.attachedRigidbody != null)
            {
                Vector2 bulletVelocity = col.attachedRigidbody.linearVelocity;
                if (bulletVelocity.sqrMagnitude > 0.1f)
                {
                    Vector2 bulletDir = bulletVelocity.normalized;
                    Vector2 sideForce1 = new Vector2(-bulletDir.y, bulletDir.x);
                    Vector2 sideForce2 = new Vector2(bulletDir.y, -bulletDir.x);

                    Vector2 bestSideForce = (Vector2.Dot(directionFromBullet, sideForce1) > 0f) ? sideForce1 : sideForce2;
                    finalBulletForce += bestSideForce * (force * 0.7f);
                }
            }

            if (isNearRightWall && finalBulletForce.x > 0) { finalBulletForce.y += (finalBulletForce.y >= 0 ? 1f : -1f) * finalBulletForce.x; finalBulletForce.x = 0; }
            else if (isNearLeftWall && finalBulletForce.x < 0) { finalBulletForce.y += (finalBulletForce.y >= 0 ? 1f : -1f) * Mathf.Abs(finalBulletForce.x); finalBulletForce.x = 0; }
            if (isNearTopWall && finalBulletForce.y > 0) { finalBulletForce.x += (finalBulletForce.x >= 0 ? 1f : -1f) * finalBulletForce.y; finalBulletForce.y = 0; }
            else if (isNearBottomWall && finalBulletForce.y < 0) { finalBulletForce.x += (finalBulletForce.x >= 0 ? 1f : -1f) * Mathf.Abs(finalBulletForce.y); finalBulletForce.y = 0; }

            totalRepulsion += finalBulletForce;
        }

        foreach (var col in hitColliders)
        {
            if (col == null || !col.CompareTag("Laser")) continue;
            if (col.gameObject.layer == myBulletLayer) continue;

            EnemyLaserBeam laser = col.GetComponent<EnemyLaserBeam>();
            if (laser != null)
            {
                hasDanger = true;

                Vector2 laserOrigin = col.transform.position;
                float laserRad = (col.transform.eulerAngles.z + 90f) * Mathf.Deg2Rad;
                Vector2 laserDirection = new Vector2(Mathf.Cos(laserRad), Mathf.Sin(laserRad)).normalized;

                Vector2 v = (Vector2)transform.position - laserOrigin;
                float projectionDistance = Vector2.Dot(v, laserDirection);
                float clampedProj = Mathf.Clamp(projectionDistance, 0f, laser.CurrentLength);

                Vector2 closestPointOnLaser = laserOrigin + laserDirection * clampedProj;
                Vector2 escapeVector = (Vector2)transform.position - closestPointOnLaser;
                float distanceToLaserLine = escapeVector.magnitude;

                if (distanceToLaserLine < 0.05f)
                {
                    escapeVector = Vector2.Perpendicular(laserDirection);
                    distanceToLaserLine = 0.1f;
                }

                float phaseWeight = laser.IsPreviewing ? 1.2f : 4.0f;
                float force = phaseWeight / (distanceToLaserLine * distanceToLaserLine);
                Vector2 laserRepulsion = escapeVector.normalized * force;

                if (isNearRightWall && laserRepulsion.x > 0) { laserRepulsion.y += (laserRepulsion.y >= 0 ? 1f : -1f) * laserRepulsion.x; laserRepulsion.x = 0; }
                else if (isNearLeftWall && laserRepulsion.x < 0) { laserRepulsion.y += (laserRepulsion.y >= 0 ? 1f : -1f) * Mathf.Abs(laserRepulsion.x); laserRepulsion.x = 0; }
                if (isNearTopWall && laserRepulsion.y > 0) { laserRepulsion.x += (laserRepulsion.x >= 0 ? 1f : -1f) * laserRepulsion.y; laserRepulsion.y = 0; }
                else if (isNearBottomWall && laserRepulsion.y < 0) { laserRepulsion.x += (escapeVector.y >= 0 ? 1f : -1f) * Mathf.Abs(laserRepulsion.y); laserRepulsion.y = 0; }

                totalRepulsion += laserRepulsion;
            }
        }

        if (transform.position.y < -2.5f && singleBullets.Count < 4)
        {
            float pushUpForce = Mathf.Abs(transform.position.y - (-1.5f)) * 0.22f;
            totalRepulsion += Vector2.up * pushUpForce;
        }

        float wallDangerFactor = ((isNearLeftWall || isNearRightWall) && (isNearBottomWall || isNearTopWall)) ? 1.0f : 0.0f;

        if (wallDangerFactor > 0.4f && singleBullets.Count + (clusterPoints.Count * 4) >= 4)
        {
            Vector2 escapeSwitchVector = Vector2.zero;

            if (isNearLeftWall && isNearBottomWall) escapeSwitchVector = (Vector2.up * 2.5f + Vector2.right * 1.0f).normalized;
            else if (isNearRightWall && isNearBottomWall) escapeSwitchVector = (Vector2.up * 2.5f + Vector2.left * 1.0f).normalized;
            else if (isNearLeftWall) escapeSwitchVector = Vector2.right * 2.0f;
            else if (isNearRightWall) escapeSwitchVector = Vector2.left * 2.0f;

            if (escapeSwitchVector != Vector2.zero && GameDifficultyManager.CurrentDifficulty != GameDifficulty.Easy)
            {
                totalRepulsion = (totalRepulsion * 0.3f) + escapeSwitchVector * 2.2f;
                AddReward(0.002f);
            }
        }

        if (transform.position.x > wallBound - padding) totalRepulsion += Vector2.left * (1.0f / Mathf.Max(0.1f, wallBound - transform.position.x));
        if (transform.position.x < -wallBound + padding) totalRepulsion += Vector2.right * (1.0f / Mathf.Max(0.1f, transform.position.x - (-wallBound)));
        if (transform.position.y > wallBound - padding) totalRepulsion += Vector2.down * (1.0f / Mathf.Max(0.1f, wallBound - transform.position.y));
        if (transform.position.y < -wallBound + padding) totalRepulsion += Vector2.up * (1.0f / Mathf.Max(0.1f, transform.position.y - (-wallBound)));

        if (!hasDanger || totalRepulsion.magnitude < 0.15f)
        {
            Vector3 wander3D = GetPerlinWanderVector(Time.time, 1.0f);
            Vector2 wander = new Vector2(wander3D.x, wander3D.y);
            totalRepulsion = (totalRepulsion + wander * 0.5f).normalized;
        }
        else
        {
            totalRepulsion = totalRepulsion.normalized;
        }

        return totalRepulsion;
    }

    private int EvaluateAndSelectTacticalSkill()
    {
        float currentMP = (playerMove != null) ? playerMove.currentEnergy : 100f;
        float currentUltGauge = (playerMove != null) ? playerMove.ultimateEnergy : 0f;

        bool isZReady = (skillManager != null) ? (skillManager.timerZ <= 0f) : true;
        bool isXReady = (skillManager != null) ? (skillManager.timerX <= 0f) : true;
        bool isCReady = (skillManager != null) ? (skillManager.timerC <= 0f) : true;
        bool isVReady = (skillManager != null) ? (skillManager.timerV <= 0f) : true;
        bool isUltReady = (skillManager != null) ? (skillManager.timerEX <= 0f) : true;

        int nearbyBulletCount = CountNearbyBullets();
        float distanceToEnemy = (opponent != null) ? Vector3.Distance(transform.position, opponent.position) : 10f;
        bool isMyVjtActive = (statusManager != null && statusManager.isSpellCardActive);

        // =========================================================================
        // 🔮【知性逆転の完全修正】：領域展開（VJT）の発動ジャッジロジック
        // =========================================================================
        if (!_disableEXAndVJTForDemo)
        {
            if (statusManager != null && !statusManager.isSpellCardActive && !statusManager.isOverheated)
            {
                // 🛑 A. 【領域返し（カウンター）のジャッジ】
                if (PlayerStatusManager.isAnyVJTActive)
                {
                    if (playerMove.Opponent != null)
                    {
                        PlayerStatusManager oppStatus = playerMove.Opponent.GetComponent<PlayerStatusManager>();
                        if (oppStatus != null && oppStatus.isSpellCardActive && currentUltGauge >= 200f)
                        {
                            float myProgress = Mathf.InverseLerp(200f, 300f, currentUltGauge);
                            float myExpectedDuration = Mathf.Lerp(statusManager.minSpellDuration, statusManager.maxSpellDuration, myProgress);
                            float oppRemainingTime = oppStatus.spellTimer;

                            // 🎯 Easyの時は「領域返し（カウンター技術）」を完全封印して知性を手加減
                            if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Easy)
                            {
                            }
                            else
                            {
                                // Normal以上は確率で執行（Normal: 30%, Hard: 70%, Lunatic: 100%）
                                float reactionChance = 1f;
                                if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Normal) reactionChance = 0.3f;
                                else if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Hard) reactionChance = 0.7f;

                                if (myExpectedDuration - oppRemainingTime > 10f && UnityEngine.Random.value <= reactionChance)
                                {
                                    Debug.Log($"<color=red>💥【AI領域返し執行】難易度：{GameDifficultyManager.CurrentDifficulty}。上書きカウンター！</color>");
                                    return 6;
                                }
                            }
                        }
                    }
                }
                // 🟢 B. 【通常の領域展開（先制・主導権掌握）のジャッジ】
                else
                {
                    // Easy➔ ゲージが200%溜まった瞬間、間合いに関係なく（画面端にいても）おバカに即暴発・無駄撃ち
                    // Normal➔ ゲージが300%MAXまでしっかりと溜まっており、かつ敵が射程圏内（6.5f以内）の時のみ賢く使う
                    // Hard/Lunatic➔ ゲージが200%以上あり、かつ敵が射程圏内（6.5f以内）の時に最速で最適に使う

                    if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Easy)
                    {
                        if (currentUltGauge >= 200f)
                        {
                            Debug.Log("<color=orange>⚠️【Easy領域暴発】間合いを一切測らず、200%で即無駄撃ちを執行します！</color>");
                            return 6;
                        }
                    }
                    else if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Normal)
                    {
                        if (currentUltGauge >= 300f && distanceToEnemy <= 6.5f)
                        {
                            Debug.Log("<color=yellow>💡【Normal戦術領域】300%MAXかつ適切な間合い（6.5f以内）で知的展開！</color>");
                            return 6;
                        }
                    }
                    else // Hard or Lunatic
                    {
                        if (currentUltGauge >= 200f && distanceToEnemy <= 6.5f)
                        {
                            Debug.Log($"<color=cyan>⚡【{GameDifficultyManager.CurrentDifficulty}最速領域】200%以上のジャストタイミングで展開！</color>");
                            return 6;
                        }
                    }
                }
            }
        }

        // 🎯 通常スキル使用間のタイマーがまだ残っているなら、通常スキルの発動をここで遮断
        if (_aiSkillIntervalTimer > 0f) return 0;

        // =========================================================================
        // 🔋【最核心進化】：再装填（ヒステリシス管理）閾値の難易度動的変調マトリクス
        // =========================================================================
        // 💡 理由：難易度が高いほどマナを極限まで引きつけてフルバーストさせ、 Easyはおバカに即撃ちさせます。
        float actualReadyThreshold = _mpReadyThreshold; // デフォルト (Lunatic: 95%近く)
        float actualSaveThreshold = _mpSaveThreshold;  // デフォルト

        if (isMyVjtActive)
        {
            actualReadyThreshold = 35f;
            actualSaveThreshold = 15f;
        }
        else
        {
            switch (GameDifficultyManager.CurrentDifficulty)
            {
                case GameDifficulty.Easy:
                    // ❌ Easy: 溜め撃ちの概念を完全破砕（下限も上限も0）➔ コストが溜まった瞬間におバカに単発即撃ち
                    actualReadyThreshold = 0f;
                    actualSaveThreshold = 0f;
                    break;
                case GameDifficulty.Normal:
                    // 🟢 Normal: 標準的な再装填（50%まで溜まったら連射解禁、20%で温存モードへ）
                    actualReadyThreshold = 50f;
                    actualSaveThreshold = 20f;
                    break;
                case GameDifficulty.Hard:
                    // 👑 Hard: 賢いバースト（75%までしっかりチャージしてから撃つ）
                    actualReadyThreshold = 75f;
                    actualSaveThreshold = 25f;
                    break;
                case GameDifficulty.Lunatic:
                    // 😈 Lunatic: 狂気のフルバースト（95%近くまで極限にエネルギーを装填して一気にハメ殺す）
                    actualReadyThreshold = 95f;
                    actualSaveThreshold = 30f;
                    break;
            }
        }

        if (_currentShootingState == ShootingState.Bursting)
        {
            if (currentMP <= actualSaveThreshold)
            {
                _currentShootingState = ShootingState.Charging;
            }
        }
        else
        {
            if (currentMP >= actualReadyThreshold)
            {
                _currentShootingState = ShootingState.Bursting;
            }
            else
            {
                return 0; // 絶賛チャージ中のため通常スキルは一律封印
            }
        }

        // =========================================================================
        // ⚔️ 通常スキルの発動判定と「使用間（手加減）」の動的注入
        // =========================================================================
        int selectedSkill = 0;

        if (isVReady && (isMyVjtActive ? (currentMP >= 20f) : (nearbyBulletCount >= 5 && currentMP >= 20f))) selectedSkill = 4;
        else if (isXReady && (isMyVjtActive ? (currentMP >= 25f) : (currentMP >= 25f && distanceToEnemy >= 2.0f && distanceToEnemy <= 8.5f))) selectedSkill = 2;
        else if (isCReady && (isMyVjtActive ? (currentMP >= 30f) : (distanceToEnemy >= 5.5f && nearbyBulletCount <= 1 && currentMP >= 30f))) selectedSkill = 3;
        else if (isZReady && currentMP >= 10f) selectedSkill = 1;

        if (selectedSkill == 0 && isUltReady && !_disableEXAndVJTForDemo)
        {
            if (isMyVjtActive && IsVjtCancelAllowed()) selectedSkill = 5;
            else if (!isMyVjtActive)
            {
                if (currentUltGauge >= 150f && currentUltGauge < 200f) { }
                else
                {
                    if (currentUltGauge >= 300f) selectedSkill = 5;
                    else if (currentUltGauge >= 200f && distanceToEnemy <= 8.5f) selectedSkill = 5;
                    else if (currentUltGauge >= 100f && distanceToEnemy < 4.0f && nearbyBulletCount <= 2) selectedSkill = 5;
                }
            }
        }

        if (selectedSkill > 0 && selectedSkill != 6)
        {
            float addedInterval = 0f;
            if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Easy) addedInterval = 1.5f;
            else if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Normal) addedInterval = 0.4f;

            _aiSkillIntervalTimer = addedInterval;
        }

        return selectedSkill;
    }

    private Vector3 GetPerlinWanderVector(float timeValue, float speedMultiplier)
    {
        float seedOffset = (playerID == 1) ? 0f : 500f;
        float noiseX = Mathf.PerlinNoise(timeValue * 1.8f + seedOffset, 0f) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(0f, timeValue * 1.8f + seedOffset) * 2f - 1f;
        return new Vector3(noiseX, noiseY).normalized * speedMultiplier;
    }

    private int CountNearbyBullets()
    {
        string targetBulletTag = (playerID == 1) ? "EnemyBullet" : "PlayerBullet";
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, _detectionRadius);
        int count = 0;
        foreach (var col in hitColliders)
        {
            if (col.CompareTag(targetBulletTag) || col.CompareTag("Laser")) count++;
        }
        return count;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
    }

    public override void OnEpisodeBegin()
    {
        _aiCurrentChargingSkillSlot = 0;
        _aiChargeFrameTimer = 0;
        _aiSkillIntervalTimer = 0f;
        skipFrameTimer = 0;
    }
}