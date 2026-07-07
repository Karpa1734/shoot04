// --- DanmakuAgent.cs Hard/Lunatic超精密型流体回避・しの字包囲網学習適合版 ---
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

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
    // 📐 【新設：AI専用マルチ通常スキルチャージインフラマネージャー】
    // =========================================================================
    // 💡 0: チャージなし, 1: Zスキルチャージ中
    private int _aiCurrentChargingSkillSlot = 0;
    private int _aiChargeFrameTimer = 0;
    private const int AI_GENERIC_CHARGE_TARGET_FRAMES = 62; // 💡 基準の最大溜めターゲット（約1.0秒）

    public override void Initialize()
    {
        playerMove = GetComponent<PlayerMove>(); //
        hitHandler = GetComponentInChildren<PlayerHitHandler>(); //
        skillManager = GetComponent<SkillManager>(); //
        statusManager = GetComponent<PlayerStatusManager>(); //
        _initialPosition = transform.position; //

        // =========================================================================
        // ⭕【新規追加】：タイトル画面での選択結果を、エージェントのAI起動スイッチに直結
        // 💡 2P側（playerID == 2）として生成されたオブジェクトのみ、VsComならAIオン、VsPlayerならAIオフに上書き
        // =========================================================================
        if (playerID == 2)
        {
            _useAutoEvadeAI = GameSelectionData.UseAutoEvadeAI;
            Debug.Log($"<color=magenta>🤖【AI同期】2Pエージェント生成。自動回避AI状態: {_useAutoEvadeAI}</color>");
        }
        else
        {
            // 1P側はプレイヤーが操作するため、基本的には自動回避は常にOFFにしておきます
            if (GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsPlayer ||
                GameSelectionData.CurrentMode == GameSelectionData.GameMode.VsCom)
            {
                _useAutoEvadeAI = false;
            }
        }

        // 既存の初期化処理
        _aiCurrentChargingSkillSlot = 0; //
        _aiChargeFrameTimer = 0; //
    }

    void FixedUpdate()
    {
        // 🚨 試合終了時やカウントダウン中、被弾中など、動けない時は入力をクリア
        if (!PlayerMove.CanShoot)
        {
            _timeSinceMatchEnd += Time.fixedDeltaTime;
            if (playerMove != null) playerMove.currentFrameInput = new PlayerMove.ReplayFrame();

            // 🤖 ルールベースAI（自動よけ）が真にONの時だけ、終了後の自動徘徊を許可
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

        // 💡 ML-Agentsの意思決定サイクルを回すため、通常時は毎フレーム行動を要求します
        RequestAction();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition); // (既存)
        if (opponent != null) // (既存)
        {
            sensor.AddObservation(opponent.localPosition); // (既存)
            Rigidbody2D oppRb = opponent.GetComponent<Rigidbody2D>(); // (既存)
            sensor.AddObservation(oppRb != null ? oppRb.linearVelocity : Vector2.zero); // (既存)
        }
        else
        {
            sensor.AddObservation(Vector3.zero); // (既存)
            sensor.AddObservation(Vector2.zero); // (既存)
        }

        if (playerMove != null) // (既存)
        {
            sensor.AddObservation(playerMove.currentEnergy / playerMove.maxEnergy); // (既存)
            sensor.AddObservation(playerMove.ultimateEnergy); // (既存)
        }
        else
        {
            sensor.AddObservation(0f); // (既存)
            sensor.AddObservation(0f); // (既存)
        }

        // =========================================================================
        // 🔮【強化学習用・新規拡張】：しの字の包囲重心 ＆ 画面端のプレッシャー観測
        // =========================================================================
        // ① 壁際のプレッシャー度合い（四隅のどこにどれだけ追いつめられているか）
        float wallX = 4.0f; // DanmakuControllerの境界値
        float wallY = 4.5f;
        float cornerX = Mathf.Clamp01((Mathf.Abs(transform.localPosition.x) - 2.5f) / (wallX - 2.5f));
        float cornerY = Mathf.Clamp01((Mathf.Abs(transform.localPosition.y) - 3.0f) / (wallY - 3.0f));
        sensor.AddObservation(cornerX); // 左右の壁への肉薄度 (0〜1)
        sensor.AddObservation(cornerY); // 上下の壁への肉薄度 (0〜1)

        // ② 周囲の敵弾の「重心ベクトル」と「平均速度ベクトル」
        string targetBulletTag = (playerID == 1) ? "EnemyBullet" : "PlayerBullet"; // (既存)
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, _detectionRadius); // (既存)

        int bulletCount = 0;
        Vector2 bulletCenterGroup = Vector2.zero;
        Vector2 bulletAverageVelocity = Vector2.zero;

        foreach (var col in hitColliders)
        {
            if (col == null) continue;
            if (col.CompareTag(targetBulletTag) || col.CompareTag("Laser")) // (既存)
            {
                Vector2 relativePos = col.transform.position - transform.position;
                bulletCenterGroup += relativePos;
                bulletCount++;

                if (col.attachedRigidbody != null) // (既存)
                {
                    bulletAverageVelocity += col.attachedRigidbody.linearVelocity; // (既存)
                }
            }
        }

        if (bulletCount > 0)
        {
            bulletCenterGroup /= bulletCount;
            bulletAverageVelocity /= bulletCount;
            sensor.AddObservation(bulletCenterGroup);     // 迫りくる「しの字の丸まった頂点」の相対位置 (Vector2)
            sensor.AddObservation(bulletAverageVelocity); // 弾幕が移動している方向ベクトル (Vector2)
        }
        else
        {
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(Vector2.zero);
        }

        if (PlayerMove.CanShoot) // (既存)
        {
            AddReward(0.0005f); // (既存)

            // 💡【重要】：観測のタイミング（毎フレーム）で、包囲網と切り返しの報酬評価を実行します
            EvaluateDeadEndSurroundingPenalty();
        }
    }

    /// <summary>
    /// 自機の周囲の弾の包囲網を4象限で計算し、袋小路（つの字の内部）にいる場合にペナルティを課す
    /// </summary>
    /// <summary>
    /// 🧠 強化学習用：画面端でしの字に包囲された状態を減点し、中央・上空への「切り返し移動」を肯定調教する
    /// </summary>
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

        // 💡 壁際（四隅のいずれかのコーナー）に追い詰められているかの判定
        float wallX = 4.0f; float wallY = 4.5f;
        bool isCornered = (Mathf.Abs(transform.localPosition.x) > wallX - 1.5f) &&
                          (Mathf.Abs(transform.localPosition.y) > wallY - 1.5f);

        // 🚨 1. 【袋小路ペナルティ】：画面隅にハメられ、かつ目の前に弾の壁（4発以上）がある場合
        if (isCornered && (leftCount + rightCount + upCount + downCount) >= 4)
        {
            // 被弾する前のこの「詰み空間」にスタックしていること自体に持続的な減点を与える
            AddReward(_deadEndFramePenalty);
        }

        // 🌟 2. 【切り返しの肯定】：左下や右下のピンチから、上方向や中央方向へダッシュして脱出しようとする入力を選んだら褒める
        if (isCornered && playerMove != null)
        {
            // 左下 (x < 0, y < 0) にいる時、上(v > 0) や 右(h > 0) の脱出アクションを起こしていればインセンティブを支給
            if (transform.localPosition.x < 0 && transform.localPosition.y < 0)
            {
                if (playerMove.currentFrameInput.v > 0 || playerMove.currentFrameInput.h > 0)
                {
                    AddReward(0.005f); // 切り返し誘導ボーナス
                }
            }
            // 右下 (x > 0, y < 0) にいる時、上(v > 0) や 左(h < 0) の脱出アクションを起こしていればボーナス
            else if (transform.localPosition.x > 0 && transform.localPosition.y < 0)
            {
                if (playerMove.currentFrameInput.v > 0 || playerMove.currentFrameInput.h < 0)
                {
                    AddReward(0.005f);
                }
            }
        }
    }

    public void GiveDamageReward() => AddReward(0.3f);
    public void GiveGrazeReward() => AddReward(0.05f);
    public void GiveHitPenalty() => AddReward(-0.5f);

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 🛑 動けない状態、またはスタン中の場合は入力を空にして即リターン
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


        // =========================================================================
        // 🧠【強化学習専用ハッキング＆温存レール】
        // =========================================================================
        if (Unity.MLAgents.Academy.Instance.IsCommunicatorOn)
        {
            float currentUltGauge = (playerMove != null) ? playerMove.ultimateEnergy : 0f;
            bool isMyVjtActive = (statusManager != null && statusManager.isSpellCardActive);

            if (!isMyVjtActive && currentUltGauge >= 200f && !statusManager.isOverheated)
            {
                if (attackAction >= 1 && attackAction <= 5)
                {
                    attackAction = 6; // 2本以上あるなら問答無用で領域展開にハック
                }
            }
            else if (attackAction == 5)
            {
                if (isMyVjtActive)
                {
                    if (!IsVjtCancelAllowed()) attackAction = 0; // 領域破壊の暴発ロック
                }
                else
                {
                    if (currentUltGauge >= 100f && currentUltGauge < 200f)
                    {
                        AddReward(-0.02f); // 必殺暴発へのお叱りペナルティ
                        attackAction = 0;
                    }
                }
            }
        }

        bool autoSlowToggle = (discrete[3] == 1);

        // 🌟 決定された移動・低速ステートを一時バッファして生成
        PlayerMove.ReplayFrame frameInput = new PlayerMove.ReplayFrame
        {
            h = h,
            v = v,
            slow = autoSlowToggle
        };

        // =========================================================================
        // 🔮 【一元化マルチ通常スキルチャージAIインフラの割り込み（AI完全思考解放版）】
        // =========================================================================
        // 🛑 A. すでにチャージ中の場合
        if (_aiCurrentChargingSkillSlot > 0)
        {
            _aiChargeFrameTimer++;

            // 💡 核心：AIが今このフレームでも「1:Z」を選び続けている、かつ最大チャージ（62F）未満ならホールドを継続！
            if (attackAction == 1 && _aiChargeFrameTimer < AI_GENERIC_CHARGE_TARGET_FRAMES)
            {
                if (_aiCurrentChargingSkillSlot == 1) frameInput.shotZ = true;
            }
            else
            {
                // 🎯【AIの意思で解放】：AIが「1:Z」以外を選んだ（ボタンを離した）、または最大タメに達した瞬間！
                if (_aiCurrentChargingSkillSlot == 1) frameInput.shotZ = false;

                if (_aiChargeFrameTimer >= AI_GENERIC_CHARGE_TARGET_FRAMES)
                {
                    Debug.Log($"<color=lime>🎯 [AI CHARGE RELEASE] 最大チャージ満了による自動リリース！</color>");
                }
                else
                {
                    Debug.Log($"<color=cyan>⚡ [AI TACTICAL RELEASE] AIが敵の動きを読み、{_aiChargeFrameTimer}Fで戦略的に途中リリースしました！</color>");
                }

                _aiCurrentChargingSkillSlot = 0;
                _aiChargeFrameTimer = 0;
            }
        }
        // 🛑 B. 新規発動の監査
        else
        {
            frameInput.shotZ = (attackAction == 1);
            frameInput.shotX = (attackAction == 2);
            frameInput.shotC = (attackAction == 3);
            frameInput.shotV = (attackAction == 4);
            frameInput.ultimate = (attackAction == 5);

            // Zスキル（1）が選ばれ、かつアセットがチャージ技である場合はチャージロックを起動
            if (attackAction == 1 && statusManager != null && statusManager.characterData != null)
            {
                if (statusManager.characterData.skillZ.isChargeSkill)
                {
                    _aiCurrentChargingSkillSlot = 1;
                    _aiChargeFrameTimer = 1;
                    frameInput.shotZ = true; // 1フレーム目の点火ホールド
                    Debug.Log($"<color=orange>ToT [AI CHARGE START] AIの意思でZスキルのチャージを開始しました。</color>");
                }
            }
        }

        // 完成したパーフェクトなフレーム入力を自機の実体に直撃同期！
        playerMove.currentFrameInput = frameInput;

        // 💡 領域展開（VJT）の発動執行
        if (attackAction == 6 && statusManager != null && !statusManager.isSpellCardActive)
        {
            if (playerMove != null && playerMove.ultimateEnergy >= 200f && !statusManager.isOverheated)
            {
                statusManager.ActivateSpellCard();
                if (Unity.MLAgents.Academy.Instance.IsCommunicatorOn) AddReward(0.5f);
            }
        }
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
                else if (isNearBottomWall && laserRepulsion.y < 0) { laserRepulsion.x += (laserRepulsion.x >= 0 ? 1f : -1f) * Mathf.Abs(laserRepulsion.y); laserRepulsion.y = 0; }

                totalRepulsion += laserRepulsion;
            }
        }

        if (transform.position.y < -2.5f && singleBullets.Count < 4)
        {
            float pushUpForce = Mathf.Abs(transform.position.y - (-1.5f)) * 0.22f;
            totalRepulsion += Vector2.up * pushUpForce;
        }

        // 💡【エラー根治】：既存の壁際判定フラグから、四隅（コーナー）に追い詰められているかを動的に算出
        //                   上下のどちらかの壁、かつ左右のどちらかの壁に同時に触れている場合は危険度を1.0（最大）にします。
        float wallDangerFactor = ((isNearLeftWall || isNearRightWall) && (isNearBottomWall || isNearTopWall)) ? 1.0f : 0.0f;

        // =========================================================================
        // 💫【最核心進化】：画面端・四隅に追い詰められた際の「切り返し（脱出）」ベクトルの結合
        // =========================================================================
        // 💡 理由：背後が壁、かつ前方が弾幕（4発以上）の際、ただ弾から離れようとすると壁に激突して静止します。
        //          この絶対絶命のピンチを検知した瞬間、AIに「弾幕の隙間（高度の高い上方向、または中央）」へ
        //          一気に滑り込んで位置を入れ替える（切り返す）ための強烈な推進力を与えます。
        if (wallDangerFactor > 0.4f && singleBullets.Count + (clusterPoints.Count * 4) >= 4)
        {
            Vector2 escapeSwitchVector = Vector2.zero;

            // 1. 左下の四隅に追い詰められている場合（カリンのしの字に捕まったスクショの状況）
            if (isNearLeftWall && isNearBottomWall)
            {
                // 💡 左下からは「真上（コルーチン側で弾が薄くなる高度）」か「右（中央）」へ一気に切り返す！
                escapeSwitchVector = (Vector2.up * 2.5f + Vector2.right * 1.0f).normalized;
                Debug.Log("<color=orange>⚡【AI緊急切り返し】左下デッドロックを検知。上空の隙間へ高速カットイン！</color>");
            }
            // 2. 右下の四隅に追い詰められている場合
            else if (isNearRightWall && isNearBottomWall)
            {
                escapeSwitchVector = (Vector2.up * 2.5f + Vector2.left * 1.0f).normalized;
                Debug.Log("<color=orange>⚡【AI緊急切り返し】右下デッドロックを検知。上空の隙間へ高速カットイン！</color>");
            }
            // 3. 左側の壁際に張り付かされている場合
            else if (isNearLeftWall)
            {
                escapeSwitchVector = Vector2.right * 2.0f; // 中央へ大きく切り返し
            }
            // 4. 右側の壁際に張り付かされている場合
            else if (isNearRightWall)
            {
                escapeSwitchVector = Vector2.left * 2.0f;  // 中央へ大きく切り返し
            }

            // 💡【流体オーバーライド】：これまでのフリーズしていた斥力を完全にねじ伏せ、
            //                          この切り返しベクトルを最大出力で運動エネルギーに直撃結合します！
            if (escapeSwitchVector != Vector2.zero)
            {
                totalRepulsion = (totalRepulsion * 0.3f) + escapeSwitchVector * 2.2f;

                // 🧠 ML-Agents 学習用報酬調教：切り返しに成功して生き残る選択肢を脳に強く肯定させるため、
                //                            画面端のピンチから脱出方向へ動き出した瞬間に微小なボーナスを支給
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

        if (!_disableEXAndVJTForDemo)
        {
            if (PlayerStatusManager.isAnyVJTActive && statusManager != null && !statusManager.isSpellCardActive && !statusManager.isOverheated && currentUltGauge >= 200f)
            {
                if (playerMove.Opponent != null)
                {
                    PlayerStatusManager oppStatus = playerMove.Opponent.GetComponent<PlayerStatusManager>();
                    if (oppStatus != null && oppStatus.isSpellCardActive)
                    {
                        float myProgress = Mathf.InverseLerp(200f, 300f, currentUltGauge);
                        float myExpectedDuration = Mathf.Lerp(statusManager.minSpellDuration, statusManager.maxSpellDuration, myProgress);
                        float oppRemainingTime = oppStatus.spellTimer;

                        if (myExpectedDuration - oppRemainingTime > 10f)
                        {
                            Debug.Log($"<color=red>【AI領域返し執行】敵の結界の隙を看破！ 割り込み領域返しをトリガーします！</color>");
                            return 6;
                        }
                    }
                }
            }
            if (!PlayerStatusManager.isAnyVJTActive && statusManager != null && !statusManager.isSpellCardActive && !statusManager.isOverheated && currentUltGauge >= 200f)
            {
                if (distanceToEnemy <= 6.5f)
                {
                    Debug.Log($"<color=cyan>🤖【AI通常領域展開】必殺リソース2本以上蓄積。主導権掌握のためVJTを展開します！</color>");
                    return 6;
                }
            }
        }

        if (isVReady && (isMyVjtActive ? (currentMP >= 20f) : (nearbyBulletCount >= 5 && currentMP >= 20f))) return 4;

        float vjtMpReadyThreshold = 35f;
        float vjtMpSaveThreshold = 15f;

        if (isMyVjtActive)
        {
            if (_currentShootingState == ShootingState.Bursting)
            {
                if (currentMP <= vjtMpSaveThreshold) _currentShootingState = ShootingState.Charging;
            }
            else
            {
                if (currentMP >= vjtMpReadyThreshold) _currentShootingState = ShootingState.Bursting;
                else return 0;
            }
        }
        else
        {
            if (_currentShootingState == ShootingState.Bursting)
            {
                if (currentMP <= _mpSaveThreshold) _currentShootingState = ShootingState.Charging;
            }
            else
            {
                if (currentMP >= _mpReadyThreshold) _currentShootingState = ShootingState.Bursting;
                else return 0;
            }
        }

        if (isXReady && (isMyVjtActive ? (currentMP >= 25f) : (currentMP >= 25f && distanceToEnemy >= 2.0f && distanceToEnemy <= 8.5f))) return 2;
        if (isCReady && (isMyVjtActive ? (currentMP >= 30f) : (distanceToEnemy >= 5.5f && nearbyBulletCount <= 1 && currentMP >= 30f))) return 3;
        if (isZReady && currentMP >= 10f) return 1;

        // --- 必殺技（EX/ULT）のAI発動ジャッジ ---
        if (isUltReady && !_disableEXAndVJTForDemo)
        {
            // =========================================================================
            // 👑【超重要：領域維持＆終了間際パージアタック戦略】
            // =========================================================================
            if (isMyVjtActive)
            {
                // 💡【破壊温存ロジック】：
                // 領域中に必殺を撃つと領域が壊れるため、基本は「絶対温存」。
                // ただし、IsVjtCancelAllowed() が true（残り時間わずか、または領域HPが瀕死）を
                // 返してきた時だけ、消滅直前の最後の悪あがき（最大効率のパージアタック）として必殺技のトリガーを許可します！
                if (IsVjtCancelAllowed())
                {
                    Debug.Log("<color=magenta>⚡【AI領域パージアタック】結界の限界を検知。消滅直前に必殺技を叩き込みます！</color>");
                    return 5;
                }

                // まだ領域が元気な間は、マナ超回復を活かして通常スキルを連射させるため、必殺技は絶対に撃たせない
                //（ここでリターンせず、下の溜め込み・温存ロジックにも進ませないようにガード）
                return 0;
            }

            // =========================================================================
            // 🔷 通常時（領域が張られていない時）の立ち回りロジック
            // =========================================================================
            // 💡【戦略的温存ロジック】：
            // ゲージが150〜199の間など「あと少しで強力な領域展開（200）ができる」という超重要フェーズでは、
            // 目先の必殺技でゲージを100消費してドブに捨てるのを防ぐため、あえて発動を「封印（温存）」させます。
            if (currentUltGauge >= 150f && currentUltGauge < 200f)
            {
                // 領域展開のために我慢させる（何もしない）
            }
            else
            {
                if (currentUltGauge >= 300f) return 5;
                if (currentUltGauge >= 200f && distanceToEnemy <= 8.5f) return 5;
                // 1ストック(100)の時は、本当に敵が目の前にいて確実に仕留められる時だけ許可
                if (currentUltGauge >= 100f && distanceToEnemy < 4.0f && nearbyBulletCount <= 2) return 5;
            }
        }

        return 0;
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
        // 既存の初期化処理はそのまま維持
        _aiCurrentChargingSkillSlot = 0;
        _aiChargeFrameTimer = 0;
    }
}