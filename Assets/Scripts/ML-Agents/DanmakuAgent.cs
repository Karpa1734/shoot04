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
        switch (GameDifficultyManager.CurrentDifficulty)
        {
            case GameDifficulty.Easy: _detectionRadius = 1.5f; break;
            case GameDifficulty.Normal: _detectionRadius = 2.5f; break;
            case GameDifficulty.Hard: _detectionRadius = 3.5f; break;
            case GameDifficulty.Lunatic: _detectionRadius = 5.0f; break;
        }

        if (_aiSkillIntervalTimer > 0f)
        {
            _aiSkillIntervalTimer -= Time.fixedDeltaTime;
        }

        if (skipFrameTimer > 0)
        {
            skipFrameTimer--;
        }

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
            float currentUltGauge = (playerMove != null ? playerMove.ultimateEnergy : 0f);
            bool isMyVjtActive = (statusManager != null && statusManager.isSpellCardActive);

            if (!isMyVjtActive)
            {
                if (currentUltGauge >= 200f && !statusManager.isOverheated)
                {
                    if (attackAction >= 1 && attackAction <= 5) attackAction = 6;
                }
                else if (attackAction == 5)
                {
                    if (currentUltGauge >= 100f && currentUltGauge < 200f)
                    {
                        AddReward(-0.02f);
                        attackAction = 0;
                    }
                }
            }
            else
            {
                if (attackAction == 5 && !IsVjtCancelAllowed())
                {
                    attackAction = 0;
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

        bool isCurrentZCharge = (statusManager != null && statusManager.characterData != null && statusManager.characterData.skillZ.isChargeSkill);

        if (isCurrentZCharge && attackAction == 1)
        {
            _aiChargeFrameTimer++;
            if (_aiChargeFrameTimer <= 45)
            {
                frameInput.shotZ = true;
            }
            else
            {
                frameInput.shotZ = false;
                _aiChargeFrameTimer = 0;
            }
        }
        else
        {
            if (attackAction != 1) _aiChargeFrameTimer = 0;
            UpdateAttackFrameAction(attackAction, autoSlowToggle, ref frameInput);
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

        if (pEX)
        {
            bool isMyVjtActive = (statusManager != null && statusManager.isSpellCardActive);

            // 🌟【最重要ガード】：領域展開中であり、かつ経過時間が8.0秒未満の場合は、EX入力を一切受け付けない！
            if (isMyVjtActive && statusManager.timeSinceVJTActivated < 8.0f)
            {
                discrete[2] = 0; // 入力を無効化
            }
            else if (isMyVjtActive && !IsVjtCancelAllowed())
            {
                discrete[2] = 0;
            }
            else
            {
                discrete[2] = 5;
            }
        }
        else
        {
            if (inputSet.skillZ != null && inputSet.skillZ.action != null && inputSet.skillZ.action.IsPressed()) discrete[2] = 1;
            else if (inputSet.skillX != null && inputSet.skillX.action != null && inputSet.skillX.action.IsPressed()) discrete[2] = 2;
            else if (isCPressed) discrete[2] = 3;
            else if (isVPressed) discrete[2] = 4;
        }

        if (inputSet.slow != null && inputSet.slow.action != null && inputSet.slow.action.IsPressed()) discrete[3] = 1;
    }
    /// <summary>
    /// 🤖【AI専用】：領域展開中のEX（ULT）使用を厳しく制限するルール関数
    /// </summary>
    private bool IsAIEXAllowed()
    {
        if (statusManager == null || !statusManager.isSpellCardActive) return true;

        // 🌟【AIのみの制限】：領域発動からの経過時間が「8秒（8.0秒）」未満のときは、AIは絶対にEXを使用させない！
        if (statusManager.timeSinceVJTActivated < 8.0f)
        {
            return false;
        }

        float vjtHpRatio = (statusManager.spellMaxHP > 0f) ? (statusManager.spellHP / statusManager.spellMaxHP) : 1f;
        float vjtRemainingTime = statusManager.spellTimer;

        // 🌟【ピンチ時の即時使用・締めでの使用】：体力が10％以下（0.1以下）に落ちた大ピンチ、または残り時間がわずかなときはEXを許可
        if (vjtHpRatio <= 0.1f || vjtRemainingTime <= 2.0f)
        {
            return true;
        }

        return false;
    }
    private bool IsVjtCancelAllowed()
    {
        if (statusManager == null || !statusManager.isSpellCardActive) return true;

        // 🌟 1. 領域発動からの経過時間が「8秒（8.0秒）」未満のときは、絶対にEXスキル（必殺）を許可しない！
        if (statusManager.timeSinceVJTActivated < 8.0f)
        {
            return false;
        }

        // 💡 2. 領域のバリア体力の割合を計算（0.0 ～ 1.0）
        float vjtHpRatio = (statusManager.spellMaxHP > 0f) ? (statusManager.spellHP / statusManager.spellMaxHP) : 1f;
        float vjtRemainingTime = statusManager.spellTimer;

        // 🌟 3. 【最重要要件】：体力が10％を切っている（vjtHpRatio <= 0.1f）場合は、即座にEX（反撃）を許可する！
        if (vjtHpRatio <= 0.1f)
        {
            return true;
        }

        // 4. 領域の残り時間がわずか（例: 2秒以下）になった場合も締めとしてEXを許可
        if (vjtRemainingTime <= 2.0f)
        {
            return true;
        }

        return false;
    }

    private bool isHpLowAndTimeOut(float hpRatio, float remTime)
    {
        return (hpRatio < 0.2f && remTime <= 7.5f);
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

            float force = 12.5f / Mathf.Max(0.4f, distance * distance);
            Vector2 clusterRepulsion = directionFromCluster.normalized * force;

            Vector2 clusterDir = directionFromCluster.normalized;
            Vector2 orbitForce = new Vector2(-clusterDir.y, clusterDir.x) * (force * 0.8f);
            clusterRepulsion += orbitForce;

            if (isNearRightWall && clusterRepulsion.x > 0) { clusterRepulsion.y += (clusterRepulsion.y >= 0 ? 1f : -1f) * clusterRepulsion.x; clusterRepulsion.x = 0; }
            else if (isNearLeftWall && clusterRepulsion.x < 0) { clusterRepulsion.y += (clusterRepulsion.y >= 0 ? 1f : -1f) * Mathf.Abs(clusterRepulsion.x); clusterRepulsion.x = 0; }
            if (isNearTopWall && clusterRepulsion.y > 0) { clusterRepulsion.x += (clusterRepulsion.x >= 0 ? 1f : -1f) * clusterRepulsion.y; clusterRepulsion.y = 0; }
            else if (isNearBottomWall && clusterRepulsion.y < 0) { clusterRepulsion.x += (clusterRepulsion.x >= 0 ? 1f : -1f) * Mathf.Abs(clusterRepulsion.y); clusterRepulsion.y = 0; }

            totalRepulsion += clusterRepulsion;
        }

        foreach (var col in singleBullets)
        {
            if (col == null) continue;

            Vector2 bulletPos = col.transform.position;
            Vector2 bulletVel = Vector2.zero;

            if (col.attachedRigidbody != null)
            {
                bulletVel = col.attachedRigidbody.linearVelocity;
                bulletPos += bulletVel * 0.25f;
            }

            Vector2 directionFromBullet = (Vector2)transform.position - bulletPos;
            float distance = directionFromBullet.magnitude;
            if (distance < 0.05f) continue;

            hasDanger = true;

            float force = 1.8f / (distance * distance);
            if (bulletVel.sqrMagnitude > 0.1f)
            {
                Vector2 toMe = ((Vector2)transform.position - (Vector2)col.transform.position).normalized;
                if (Vector2.Dot(bulletVel.normalized, toMe) > 0.3f)
                {
                    force *= 3.0f;
                }
            }

            Vector2 bulletRepulsion = directionFromBullet.normalized * force;
            Vector2 finalBulletForce = bulletRepulsion;

            if (bulletVel.sqrMagnitude > 0.1f)
            {
                Vector2 bulletDir = bulletVel.normalized;
                Vector2 sideForce1 = new Vector2(-bulletDir.y, bulletDir.x);
                Vector2 sideForce2 = new Vector2(bulletDir.y, -bulletDir.x);

                Vector2 bestSideForce = (Vector2.Dot(directionFromBullet, sideForce1) > 0f) ? sideForce1 : sideForce2;
                finalBulletForce += bestSideForce * (force * 1.2f);
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

    private bool _isWaitingForRecharge = false;
    private int EvaluateAndSelectTacticalSkill()
    {
        float currentMP = (playerMove != null) ? playerMove.currentEnergy : 100f;
        float maxMP = (playerMove != null) ? playerMove.maxEnergy : 100f;
        float currentUltGauge = (playerMove != null ? playerMove.ultimateEnergy : 0f);

        bool isZReady = (skillManager != null) ? (skillManager.timerZ <= 0f) : true;
        bool isXReady = (skillManager != null) ? (skillManager.timerX <= 0f) : true;
        bool isCReady = (skillManager != null) ? (skillManager.timerC <= 0f) : true;
        bool isVReady = (skillManager != null) ? (skillManager.timerV <= 0f) : true;
        bool isUltReady = (skillManager != null) ? (skillManager.timerEX <= 0f) : true;

        int nearbyBulletCount = CountNearbyBullets();
        float distanceToEnemy = (opponent != null) ? Vector3.Distance(transform.position, opponent.position) : 10f;
        bool isMyVjtActive = (statusManager != null && statusManager.isSpellCardActive);

        PlayerSkillData charData = (statusManager != null) ? statusManager.characterData : null;

        int selectedSkill = 0;

        // =========================================================================
        // 🔮 1. 領域展開（VJT）のAI発動判定
        // =========================================================================
        if (!_disableEXAndVJTForDemo && statusManager != null && !statusManager.isSpellCardActive && !statusManager.isOverheated)
        {
            if (currentUltGauge >= 200f && distanceToEnemy <= 6.5f)
            {
                return 6; // 6 = 領域展開アクション
            }
        }

        // =========================================================================
        // 👑 EXスキル（ULT）のAI発動判定
        // =========================================================================
        bool canUseEX = false;
        if (isMyVjtActive)
        {
            // 🤖 AIは領域中のEX使用に IsAIEXAllowed()（8秒経過 ＆ ピンチ等）の制限を強制
            canUseEX = isUltReady && IsAIEXAllowed();
        }
        else
        {
            canUseEX = isUltReady && (currentUltGauge >= 100f);
        }

        if (canUseEX && !_disableEXAndVJTForDemo)
        {
            if (isMyVjtActive)
            {
                if (IsAIEXAllowed())
                {
                    return 5; // EX発動
                }
            }
            else if (!isMyVjtActive && (currentUltGauge >= 300f || (currentUltGauge >= 200f && distanceToEnemy <= 8.5f) || (currentUltGauge >= 100f && distanceToEnemy < 4.0f)))
            {
                return 5; // EX発動
            }
        }

        if (_aiSkillIntervalTimer > 0f) return 0;

        float highThreshold = maxMP * 0.8f;
        float lowThreshold = maxMP * 0.1f;

        if (!_isWaitingForRecharge && currentMP <= lowThreshold)
        {
            _isWaitingForRecharge = true;
        }
        else if (_isWaitingForRecharge && currentMP >= highThreshold)
        {
            _isWaitingForRecharge = false;
        }

        if (_isWaitingForRecharge)
        {
            return 0;
        }

        float costZ = (charData != null) ? charData.skillZ.cost : 10f;
        float costX = (charData != null) ? charData.skillX.cost : 20f;
        float costC = (charData != null) ? charData.skillC.cost : 25f;
        float costV = (charData != null) ? charData.skillV.cost : 20f;

        bool canUseZ = isZReady && (currentMP >= costZ);
        bool canUseX = isXReady && (currentMP >= costX);
        bool canUseC = isCReady && (currentMP >= costC);

        bool isEmergencyThreat = (nearbyBulletCount >= 4);
        bool canUseV = isVReady && (currentMP >= costV) && (!isMyVjtActive || isEmergencyThreat);

        if (selectedSkill == 0)
        {
            System.Collections.Generic.List<int> candidateSkills = new System.Collections.Generic.List<int>();

            if (canUseZ)
            {
                candidateSkills.Add(1);
                candidateSkills.Add(1);
            }
            if (canUseX)
            {
                if (distanceIdBetween(distanceToEnemy, 2.0f, 8.5f)) { candidateSkills.Add(2); candidateSkills.Add(2); }
                else { candidateSkills.Add(2); }
            }
            if (canUseC)
            {
                if (distanceToEnemy >= 4.0f && nearbyBulletCount <= 2) { candidateSkills.Add(3); candidateSkills.Add(3); }
                else { candidateSkills.Add(3); }
            }
            if (canUseV)
            {
                if (isEmergencyThreat)
                {
                    candidateSkills.Add(4);
                    candidateSkills.Add(4);
                    candidateSkills.Add(4);
                }
                else if (nearbyBulletCount >= 2)
                {
                    candidateSkills.Add(4);
                }
            }

            if (candidateSkills.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, candidateSkills.Count);
                selectedSkill = candidateSkills[randomIndex];
            }
        }

        if (selectedSkill > 0 && selectedSkill != 6)
        {
            float addedInterval = 0f;
            if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Easy) addedInterval = 1.0f;
            else if (GameDifficultyManager.CurrentDifficulty == GameDifficulty.Normal) addedInterval = 0.3f;
            else addedInterval = 0.1f;

            _aiSkillIntervalTimer = addedInterval;
        }

        return selectedSkill;
    }

    private bool distanceIdBetween(float val, float min, float max)
    {
        return val >= min && val <= max;
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