// --- DanmakuAgent.cs 修正完全復活版（自動回避完全保護・データ完全同期型） ---
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class DanmakuAgent : Agent
{
    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler;
    private SkillManager skillManager; // 🌟 追加：本物のクールダウンとコストを覗き込むための窓口
    [SerializeField] private Transform opponent;
    public int playerID = 1;

    [Header("AI Evade Settings (Rule-Based)")]
    [SerializeField] private float _detectionRadius = 3.5f;
    public bool _useAutoEvadeAI = false; // ★ インスペクターまたは外部から読み込めるように public/またはプロパティ化

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

    public override void Initialize()
    {
        playerMove = GetComponent<PlayerMove>();
        hitHandler = GetComponentInChildren<PlayerHitHandler>();
        skillManager = GetComponent<SkillManager>(); // 🌟 本物の戦闘マネージャーとがっちり溶接
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

        // 🌟 アクション5番が立っていたら、ReplayFrameパケットの ultimate を確実にTrueにする
        playerMove.currentFrameInput = new PlayerMove.ReplayFrame
        {
            h = h,
            v = v,
            slow = (discrete[3] == 1),
            shotZ = (discrete[2] == 1),
            shotX = (discrete[2] == 2),
            shotC = (discrete[2] == 3),
            shotV = (discrete[2] == 4),
            ultimate = (discrete[2] == 5) // 🌟 これにより、AI自律射撃モード時の全フラグが開通します！
        };
    }

    /// <summary>
    /// 手動操作デバッグ時のキーボード読み取り処理（Nullクラッシュ完全ガード付き）
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!PlayerMove.CanInput || (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal)) return;

        var discrete = actionsOut.DiscreteActions;
        discrete.Clear();

        // 幾何学AIモードの処理
        if (_useAutoEvadeAI)
        {
            Vector2 evadeVector = CalculatePotentialEvadeDirection();
            if (evadeVector.x < -0.15f) discrete[0] = 1; else if (evadeVector.x > 0.15f) discrete[0] = 2;
            if (evadeVector.y > 0.15f) discrete[1] = 1; else if (evadeVector.y < -0.15f) discrete[1] = 2;
            discrete[2] = EvaluateAndSelectTacticalSkill(); // 🌟 リアルタイム同期思考ロジックへ
            return;
        }

        // 手動操作デバッグ
        if (InputManager.Instance == null) return;
        var inputSet = (playerID == 1) ? InputManager.Instance.player1 : InputManager.Instance.player2;
        Vector2 m = inputSet.move.action.ReadValue<Vector2>();
        if (m.x < -0.5f) discrete[0] = 1; else if (m.x > 0.5f) discrete[0] = 2;
        if (m.y > 0.5f) discrete[1] = 1; else if (m.y < -0.5f) discrete[1] = 2;

        bool isCPressed = (inputSet.skillC != null && inputSet.skillC.action != null) && inputSet.skillC.action.IsPressed();
        bool isVPressed = (inputSet.skillV != null && inputSet.skillV.action != null) && inputSet.skillV.action.IsPressed();

        // インスペクター画像で新設していただいた Skill_EX アセットの入力参照
        bool pEX = false;
        if (inputSet.skillEX != null && inputSet.skillEX.action != null)
        {
            pEX = inputSet.skillEX.action.IsPressed();
        }
        else
        {
            pEX = (isCPressed && isVPressed);
        }

        // 同時押し（アクション5番）の優先度ソート
        if (pEX)
        {
            discrete[2] = 5;
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
    /// 【完全強豪化】ダミー値を徹底駆逐し、本物のMP・ゲージ・クールダウンUIと完全直結させたAIの戦術思考
    /// </summary>
    private int EvaluateAndSelectTacticalSkill()
    {
        // 🌟 修正：仮受けデータを完全消去し、PlayerMoveおよびSkillManagerの本物のリアルタイム実数へと結合！
        float currentMP = (playerMove != null) ? playerMove.currentEnergy : 100f;
        float currentUltGauge = (playerMove != null) ? playerMove.ultimateEnergy : 0f;

        // SkillManager側の実際の最新クールダウン残時間（<= 0 で撃てる）を正確にマッピング同期
        bool isZReady = (skillManager != null) ? (skillManager.timerZ <= 0f) : true;
        bool isXReady = (skillManager != null) ? (skillManager.timerX <= 0f) : true;
        bool isCReady = (skillManager != null) ? (skillManager.timerC <= 0f) : true;
        bool isVReady = (skillManager != null) ? (skillManager.timerV <= 0f) : true;
        bool isUltReady = (skillManager != null) ? (skillManager.timerEX <= 0f) : true;

        int nearbyBulletCount = CountNearbyBullets();
        float distanceToEnemy = (opponent != null) ? Vector3.Distance(transform.position, opponent.position) : 10f;

        // --- A. 最優先：絶対的な大ピンチ時の「強欲ディフェンス（V結界）」 ---
        if (nearbyBulletCount >= 5 && isVReady && currentMP >= 20f)
        {
            return 4; // Vキー：強欲結界
        }

        // --- B. ヒステリシス特性を用いたコスト（MP）温存ステートマシン ---
        if (_currentShootingState == ShootingState.Bursting)
        {
            if (currentMP <= _mpSaveThreshold) _currentShootingState = ShootingState.Charging;
        }
        else
        {
            if (currentMP >= _mpReadyThreshold) _currentShootingState = ShootingState.Bursting;
            else return 0; // チャージ中は大ピンチ以外打ち止め
        }

        // --- C. 状況適応型の最適なショット選択（Utility評価） ---

        // ① アルティメット（同時押し）の超高火力チャンス判定
        // 条件：必殺ゲージが1ストック分(100)以上溜まっており、かつリキャスト完了、敵が射程圏内(6.0f)の時！
        if (isUltReady && currentUltGauge >= 100f && distanceToEnemy < 6.0f && nearbyBulletCount <= 2)
        {
            Debug.Log("<color=gold>🤖【AIの戦術的決断】超必殺ゲージMAX。必殺のEX陰章オーブアサルトを解放します！</color>");
            return 5; // アクション5番を発動！
        }

        // ② ★★★ Xスキル（自機外し全方位回転加速弾）のハメ設置 ★★★
        if (isXReady && currentMP >= 25f && distanceToEnemy >= 2.0f && distanceToEnemy <= 8.5f)
        {
            return 2;
        }

        // ③ Cスキル（長距離・高火力狙撃弾）の隙間スナイプ
        if (isCReady && distanceToEnemy >= 5.5f && nearbyBulletCount <= 1 && currentMP >= 30f)
        {
            return 3;
        }

        // ④ Zスキル（基本連射・牽制弾）の隙間埋めコンボ連射
        if (isZReady && currentMP >= 10f)
        {
            return 1;
        }

        return 0;
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

        foreach (var col in hitColliders)
        {
            if (col.CompareTag(targetBulletTag))
            {
                Vector2 directionFromBullet = (Vector2)transform.position - (Vector2)col.transform.position;
                float distance = directionFromBullet.magnitude;
                if (distance < 0.05f) continue;

                hasDanger = true;
                float force = 1.0f / (distance * distance);
                Vector2 bulletRepulsion = directionFromBullet.normalized * force;

                if (isNearRightWall && bulletRepulsion.x > 0)
                {
                    bulletRepulsion.y += (bulletRepulsion.y >= 0 ? 1f : -1f) * bulletRepulsion.x;
                    bulletRepulsion.x = 0;
                }
                else if (isNearLeftWall && bulletRepulsion.x < 0)
                {
                    bulletRepulsion.y += (bulletRepulsion.y >= 0 ? 1f : -1f) * Mathf.Abs(bulletRepulsion.x);
                    bulletRepulsion.x = 0;
                }

                if (isNearTopWall && bulletRepulsion.y > 0)
                {
                    bulletRepulsion.x += (bulletRepulsion.x >= 0 ? 1f : -1f) * bulletRepulsion.y;
                    bulletRepulsion.y = 0;
                }
                else if (isNearBottomWall && bulletRepulsion.y < 0)
                {
                    bulletRepulsion.x += (bulletRepulsion.x >= 0 ? 1f : -1f) * Mathf.Abs(bulletRepulsion.y);
                    bulletRepulsion.y = 0;
                }

                totalRepulsion += bulletRepulsion;
            }
            else if (col.CompareTag("Laser"))
            {
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

                    if (isNearRightWall && laserRepulsion.x > 0)
                    {
                        laserRepulsion.y += (laserRepulsion.y >= 0 ? 1f : -1f) * laserRepulsion.x;
                        laserRepulsion.x = 0;
                    }
                    else if (isNearLeftWall && laserRepulsion.x < 0)
                    {
                        laserRepulsion.y += (laserRepulsion.y >= 0 ? 1f : -1f) * Mathf.Abs(laserRepulsion.x);
                        laserRepulsion.x = 0;
                    }

                    if (isNearTopWall && laserRepulsion.y > 0)
                    {
                        laserRepulsion.x += (laserRepulsion.x >= 0 ? 1f : -1f) * laserRepulsion.y;
                        laserRepulsion.y = 0;
                    }
                    else if (isNearBottomWall && laserRepulsion.y < 0)
                    {
                        laserRepulsion.x += (laserRepulsion.x >= 0 ? 1f : -1f) * Mathf.Abs(laserRepulsion.y);
                        laserRepulsion.y = 0;
                    }

                    totalRepulsion += laserRepulsion;
                }
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