// --- DanmakuAgent.cs Hard/Lunatic超精密型流体回避・低速自動適合版 ---
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

    public override void Initialize()
    {
        playerMove = GetComponent<PlayerMove>();
        hitHandler = GetComponentInChildren<PlayerHitHandler>();
        skillManager = GetComponent<SkillManager>();
        statusManager = GetComponent<PlayerStatusManager>();
        _initialPosition = transform.position;

        moveAction.Enable();
        skillZAction.Enable();
        skillXAction.Enable();
        skillCAction.Enable();
        skillVAction.Enable();
        slowAction.Enable();
    }

    void FixedUpdate()
    {
        if (!_useAutoEvadeAI) return;
        if (!PlayerMove.CanShoot)
        {
            _timeSinceMatchEnd += Time.fixedDeltaTime;
            if (playerMove != null) playerMove.currentFrameInput = new PlayerMove.ReplayFrame();

            if (_timeSinceMatchEnd < 1.5f) transform.position += GetPerlinWanderVector(Time.time, 0.015f);
            else
            {
                if (Vector3.Distance(transform.position, _initialPosition) > 0.1f)
                    transform.position = Vector3.MoveTowards(transform.position, _initialPosition, 2.5f * Time.fixedDeltaTime);
                transform.position += GetPerlinWanderVector(Time.time, 0.025f);
            }
            return;
        }
        _timeSinceMatchEnd = 0f;
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

        if (PlayerMove.CanShoot)
        {
            AddReward(0.0005f);
        }
    }

    public void GiveDamageReward() => AddReward(0.3f);
    public void GiveGrazeReward() => AddReward(0.05f);
    public void GiveHitPenalty() => AddReward(-0.5f);

    /// <summary>
    /// AIの頭脳モデル、またはHeuristicから上がってきた確定アクションをReplayFrameへ安全にデコード・インジェクション
    /// </summary>
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

        // 領域解除隙ガード制御
        if (attackAction == 5 && !IsVjtCancelAllowed())
        {
            attackAction = 0;
        }

        // =========================================================================
        // ⚡【核心の進化3】：危険域（半径1.2）での「超精密低速自動切り替えスイッチ」の自動割り込み
        // =========================================================================
        bool autoSlowToggle = (discrete[3] == 1); // AIの基本判断をベースラインとして取得

        if (_useAutoEvadeAI)
        {
            // 自機の肌に触れる超至近距離（1.2ユニット内）をOverlapCircleで1フレーム高速スキャン
            string targetBulletTag = (playerID == 1) ? "EnemyBullet" : "PlayerBullet";
            Collider2D[] closeColliders = Physics2D.OverlapCircleAll(transform.position, 1.2f);
            bool isImmediateDanger = false;

            foreach (var col in closeColliders)
            {
                if (col != null && (col.CompareTag(targetBulletTag) || col.CompareTag("Laser")))
                {
                    isImmediateDanger = true;
                    break;
                }
            }

            // 💡【ジャッジ】：肌に触れる距離まで弾が肉薄していれば、AIのボタン入力を上書きして強制的に低速（シフト）ON！
            //                 これにより、隙間の大回り時は「高速移動」、いざ被弾寸前の微細回避時は「1コマ単位の精密ドット避け」へ自動変調します。
            if (isImmediateDanger)
            {
                autoSlowToggle = true;
            }
        }

        playerMove.currentFrameInput = new PlayerMove.ReplayFrame
        {
            h = h,
            v = v,
            slow = autoSlowToggle, // 自動調停されたシフト判定を同期インジェクション
            shotZ = (attackAction == 1),
            shotX = (attackAction == 2),
            shotC = (attackAction == 3),
            shotV = (attackAction == 4),
            ultimate = (attackAction == 5)
        };

        if (attackAction == 6 && statusManager != null && !statusManager.isSpellCardActive)
        {
            statusManager.ActivateSpellCard();
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

    /// <summary>
    /// 📐【Hard/Lunatic適合型】：弾幕クラスター、流体（横滑り）回避、画面下張り付き防止を統合した
    /// プロフェッショナル仕様の回避物理ベクトル算出マトリクス。
    /// </summary>
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

        // 弾幕塊（クラスター）の動的スキャンバッファ
        var singleBullets = new System.Collections.Generic.List<Collider2D>();
        var clusterPoints = new System.Collections.Generic.List<Vector2>();
        var analyzedCounted = new System.Collections.Generic.HashSet<Collider2D>();

        // 1. 周囲の弾同士の距離を全走査し、塊（クラスター）を抽出
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

        // 2. 【塊（クラスター）】からの大回り大避ベクトル演算
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

        // =========================================================================
        // ⚡【核心の進化1】：単発弾に対する「流体（横滑り）回避ベクトル」の完全数理溶接
        // =========================================================================
        foreach (var col in singleBullets)
        {
            if (col == null) continue;
            Vector2 directionFromBullet = (Vector2)transform.position - (Vector2)col.transform.position;
            float distance = directionFromBullet.magnitude;
            if (distance < 0.05f) continue;

            hasDanger = true;

            // ベースとなる斥力（距離の2乗に反比例）
            float force = 1.0f / (distance * distance);
            Vector2 bulletRepulsion = directionFromBullet.normalized * force;

            Vector2 finalBulletForce = bulletRepulsion;

            // 弾のRigidbodyからリアルタイムな「進行ベクトル」を取得
            if (col.attachedRigidbody != null)
            {
                Vector2 bulletVelocity = col.attachedRigidbody.linearVelocity;
                if (bulletVelocity.sqrMagnitude > 0.1f)
                {
                    Vector2 bulletDir = bulletVelocity.normalized;

                    // 弾の進行方向に対して「真横（90度）」を向く左右の垂直ベクトルを生成
                    Vector2 sideForce1 = new Vector2(-bulletDir.y, bulletDir.x);
                    Vector2 sideForce2 = new Vector2(bulletDir.y, -bulletDir.x);

                    // 自機の現在地から弾の横軸を評価し、より安全にすれ違える「近い方の横方向」をロック
                    Vector2 bestSideForce = (Vector2.Dot(directionFromBullet, sideForce1) > 0f) ? sideForce1 : sideForce2;

                    // 💡【流体合算】：正面から離れる力(1.0) ＋ 弾の真横に滑り込む力(0.7) を絶妙にブレンド！
                    //                 これより、Lunaticの超高速弾が来ても、チョン避けのように綺麗に脇をすり抜けます。
                    finalBulletForce += bestSideForce * (force * 0.7f);
                }
            }

            // 壁際クランプの適用
            if (isNearRightWall && finalBulletForce.x > 0) { finalBulletForce.y += (finalBulletForce.y >= 0 ? 1f : -1f) * finalBulletForce.x; finalBulletForce.x = 0; }
            else if (isNearLeftWall && finalBulletForce.x < 0) { finalBulletForce.y += (finalBulletForce.y >= 0 ? 1f : -1f) * Mathf.Abs(finalBulletForce.x); finalBulletForce.x = 0; }
            if (isNearTopWall && finalBulletForce.y > 0) { finalBulletForce.x += (finalBulletForce.x >= 0 ? 1f : -1f) * finalBulletForce.y; finalBulletForce.y = 0; }
            else if (isNearBottomWall && finalBulletForce.y < 0) { finalBulletForce.x += (finalBulletForce.x >= 0 ? 1f : -1f) * Mathf.Abs(finalBulletForce.y); finalBulletForce.y = 0; }

            totalRepulsion += finalBulletForce;
        }

        // レーザー判定（既存ロジック完全維持）
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

        // =========================================================================
        // ⚡【核心の進化2】：画面最下段「張り付き圧殺」防止用・中央復帰アシストベクトルの溶接
        // =========================================================================
        // 💡 理由：AIが弾から逃げるあまり最下段（Y = -4.0付近）に固定されると、逃げ道がなくなって即詰みます。
        //          そのため、周囲の弾がパラパラ（4発未満）で余裕がある時だけ、画面の少し上（Y = -1.5付近）へ戻そうとする
        //          マイルドな引き戻しベクトルのテン力をかけ、前へ出る勇気を与えます。
        if (transform.position.y < -2.5f && singleBullets.Count < 4)
        {
            float pushUpForce = Mathf.Abs(transform.position.y - (-1.5f)) * 0.22f;
            totalRepulsion += Vector2.up * pushUpForce;
        }

        // 外壁衝突防止（既存の処理）
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
                        Debug.Log($"<color=red>🤖【AI領域返し執行】敵の結界の隙を看破！ 割り込み領域返しをトリガーします！</color>");
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

        if (isUltReady)
        {
            if (statusManager != null && statusManager.isSpellCardActive) return 5;
            if (currentUltGauge >= 300f) return 5;
            if (currentUltGauge >= 200f && distanceToEnemy <= 8.5f) return 5;
            if (currentUltGauge >= 100f && distanceToEnemy < 6.0f && nearbyBulletCount <= 2) return 5;
        }

        if (nearbyBulletCount >= 5 && isVReady && currentMP >= 20f) return 4;

        if (_currentShootingState == ShootingState.Bursting)
        {
            if (currentMP <= _mpSaveThreshold) _currentShootingState = ShootingState.Charging;
        }
        else
        {
            if (currentMP >= _mpReadyThreshold) _currentShootingState = ShootingState.Bursting;
            else return 0;
        }

        if (isXReady && currentMP >= 25f && distanceToEnemy >= 2.0f && distanceToEnemy <= 8.5f) return 2;
        if (isCReady && distanceToEnemy >= 5.5f && nearbyBulletCount <= 1 && currentMP >= 30f) return 3;
        if (isZReady && currentMP >= 10f) return 1;

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
}