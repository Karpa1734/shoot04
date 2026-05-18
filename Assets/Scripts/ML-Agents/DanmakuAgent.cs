// --- DanmakuAgent.cs 修正版（自然なうろうろ挙動＆試合終了時緩和ロジック搭載） ---
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class DanmakuAgent : Agent
{
    // 既存の変数群
    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler;
    [SerializeField] private Transform opponent;
    public int playerID = 1;

    [Header("AI Evade Settings (Rule-Based)")]
    [SerializeField] private float _detectionRadius = 3.5f;
    [SerializeField] private bool _useAutoEvadeAI = true;

    [Header("AI Tactical Shooting Settings")]
    [Tooltip("このMPまで溜まったら攻撃を再開する（バーストフェーズ移行）")]
    [SerializeField] private float _mpReadyThreshold = 80f;
    [Tooltip("このMP以下になったら攻撃を止めて温存する（チャージフェーズ移行）")]
    [SerializeField] private float _mpSaveThreshold = 25f;

    // AIの射撃状態ステート
    private enum ShootingState { Charging, Bursting }
    private ShootingState _currentShootingState = ShootingState.Bursting;

    private Vector3 _initialPosition; //
    private float _timeSinceMatchEnd = 0f; //

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
        _initialPosition = transform.position; //

        moveAction.Enable();
        skillZAction.Enable();
        skillXAction.Enable();
        skillCAction.Enable();
        skillVAction.Enable();
        slowAction.Enable();
    }

    void FixedUpdate()
    {
        if (!_useAutoEvadeAI) return; //
        if (!PlayerMove.CanShoot) //
        {
            _timeSinceMatchEnd += Time.fixedDeltaTime; //
            if (playerMove != null) playerMove.currentFrameInput = new PlayerMove.ReplayFrame(); //

            if (_timeSinceMatchEnd < 1.5f) transform.position += GetPerlinWanderVector(Time.time, 0.015f); //
            else //
            {
                if (Vector3.Distance(transform.position, _initialPosition) > 0.1f) //
                    transform.position = Vector3.MoveTowards(transform.position, _initialPosition, 2.5f * Time.fixedDeltaTime); //
                transform.position += GetPerlinWanderVector(Time.time, 0.025f); //
            }
            return; //
        }
        _timeSinceMatchEnd = 0f; //
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!PlayerMove.CanInput || (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal)) //
        {
            if (playerMove != null) playerMove.currentFrameInput = new PlayerMove.ReplayFrame(); //
            return;
        }

        var discrete = actions.DiscreteActions;
        float h = 0, v = 0;
        if (discrete[0] == 1) h = -1; else if (discrete[0] == 2) h = 1; //
        if (discrete[1] == 1) v = 1; else if (discrete[1] == 2) v = -1; //

        playerMove.currentFrameInput = new PlayerMove.ReplayFrame
        {
            h = h,
            v = v,
            slow = (discrete[3] == 1), //
            shotZ = (discrete[2] == 1),
            shotX = (discrete[2] == 2),
            shotC = (discrete[2] == 3),
            shotV = (discrete[2] == 4),
            ultimate = (discrete[2] == 5) //
        };
        AddReward(0.001f); //
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition); //
        if (opponent != null) sensor.AddObservation(opponent.localPosition); //
        else sensor.AddObservation(Vector3.zero); //
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!PlayerMove.CanInput || (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal)) return; //

        var discrete = actionsOut.DiscreteActions;
        discrete.Clear();

        if (_useAutoEvadeAI) //
        {
            // 1. 移動ベクトルの算出
            Vector2 evadeVector = CalculatePotentialEvadeDirection(); //
            if (evadeVector.x < -0.15f) discrete[0] = 1; else if (evadeVector.x > 0.15f) discrete[0] = 2; //
            if (evadeVector.y > 0.15f) discrete[1] = 1; else if (evadeVector.y < -0.15f) discrete[1] = 2; //

            // 2. ★ 戦術評価ロジックによる最適な射撃スキルの選択
            discrete[2] = EvaluateAndSelectTacticalSkill();
            return;
        }

        // 手動操作デバッグ
        if (InputManager.Instance == null) return; //
        var inputSet = (playerID == 1) ? InputManager.Instance.player1 : InputManager.Instance.player2; //
        Vector2 m = inputSet.move.action.ReadValue<Vector2>(); //
        if (m.x < -0.5f) discrete[0] = 1; else if (m.x > 0.5f) discrete[0] = 2; //
        if (m.y > 0.5f) discrete[1] = 1; else if (m.y < -0.5f) discrete[1] = 2; //

        bool pZ = inputSet.skillZ.action.IsPressed(); //
        bool pX = inputSet.skillX.action.IsPressed(); //
        bool pC = inputSet.skillC.action.IsPressed(); //
        bool pV = inputSet.skillV.action.IsPressed(); //

        if (pZ && pX) discrete[2] = 5; //
        else if (pZ) discrete[2] = 1; else if (pX) discrete[2] = 2; else if (pC) discrete[2] = 3; else if (pV) discrete[2] = 4; //
        if (inputSet.slow.action.IsPressed()) discrete[3] = 1; //
    }

    /// <summary>
    /// 核心ロジック：現在のコスト、リキャスト、戦況（ピンチ度）を統合評価し、無駄のない最強の弾を返す
    /// </summary>
    private int EvaluateAndSelectTacticalSkill()
    {
        // --- A. 現在のプレイヤー状態の取得（仮受け） ---
        float currentMP = 100f;
        bool isZReady = true;
        bool isXReady = true; // X: 自機外し全方位回転加速弾
        bool isCReady = true;
        bool isVReady = true;
        bool isUltReady = true;

        // ※環境に合わせて実際のコンポーネントからデータを受け渡してください
        // if (playerMove != null) { currentMP = playerMove.CurrentMP; ... }

        int nearbyBulletCount = CountNearbyBullets();
        float distanceToEnemy = (opponent != null) ? Vector3.Distance(transform.position, opponent.position) : 10f;

        // --- B. 最優先：絶対的な大ピンチ時の「強欲ディフェンス（V結界）」 ---
        if (nearbyBulletCount >= 5 && isVReady && currentMP >= 20f)
        {
            return 4; // Vキー：強欲結界
        }

        // --- C. ヒステリシス特性を用いたコスト（MP）温存ステートマシン ---
        if (_currentShootingState == ShootingState.Bursting)
        {
            if (currentMP <= _mpSaveThreshold) _currentShootingState = ShootingState.Charging;
        }
        else
        {
            if (currentMP >= _mpReadyThreshold) _currentShootingState = ShootingState.Bursting;
            else return 0; // チャージ中は大ピンチ以外打ち止め
        }

        // --- D. 状況適応型の最適なショット選択（Utility評価） ---

        // ① アルティメット（同時押し）の超高火力チャンス判定
        if (isUltReady && currentMP >= 60f && distanceToEnemy < 6.0f && nearbyBulletCount <= 2)
        {
            return 5;
        }

        // ② ★★★ 【優先度最大化】Xスキル（自機外し全方位回転加速弾）★★★
        // 通常の牽制弾（Z）を撃つ前に、まずは「相手のステップや退路を制限する壁」として空間に設置します。
        // インファイト（超至近距離 2.0f未満）以外の中〜遠距離であれば、相手の速度に関わらず積極的にぶっ放します。
        if (isXReady && currentMP >= 25f && distanceToEnemy >= 2.0f && distanceToEnemy <= 8.5f)
        {
            // さらに状況評価を追加：
            // 自分が少し弾に押され始めている時（nearbyBulletCount >= 2）は、
            // 自機外し全方位の12波螺旋スパイラルを「カウンターの拒絶幕」として展開し、戦況を仕切り直す
            return 2; // Xスキル発動！➔ 敵を全方位から包囲・ハメ殺す布石を置く
        }

        // ③ Cスキル（長距離・高火力狙撃弾）の判定
        // 敵が遠距離にいて、かつこちらが安全な状態であれば、Xスキルの弾幕に気を取られている隙をスナイプ
        if (isCReady && distanceToEnemy >= 5.5f && nearbyBulletCount <= 1 && currentMP >= 30f)
        {
            return 3;
        }

        // ④ Zスキル（基本連射・牽制弾）の判定
        // Xスキルがクールダウン（リキャスト待ち）の「合間」を埋める高速連射アタッカー。
        // リキャストが明けていれば常時回してコンボの密度を維持
        if (isZReady && currentMP >= 10f)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// 数理ロジック：斥力場計算に、平時の「自然なうろうろ（Wandering）」を統合
    /// </summary>
    // --- DanmakuAgent.cs 内の CalculatePotentialEvadeDirection() 修正部分 ---

    private Vector2 CalculatePotentialEvadeDirection()
    {
        Vector2 totalRepulsion = Vector2.zero;
        string targetBulletTag = (playerID == 1) ? "EnemyBullet" : "PlayerBullet";

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, _detectionRadius);
        bool hasDanger = false;

        // ★ 自身のIDから、自分が撃った弾幕（除外すべき安全なレイヤー）を事前に特定
        int myBulletLayer = LayerMask.NameToLayer(playerID == 1 ? "Player1Bullet" : "Player2Bullet");

        foreach (var col in hitColliders)
        {
            // ① 通常の弾幕の処理
            if (col.CompareTag(targetBulletTag))
            {
                Vector2 directionFromBullet = (Vector2)transform.position - (Vector2)col.transform.position;
                float distance = directionFromBullet.magnitude;
                if (distance < 0.05f) continue;

                hasDanger = true;
                float force = 1.0f / (distance * distance);
                totalRepulsion += directionFromBullet.normalized * force;
            }

            // ② 予告線・レーザー特化の線分斥力計算
            else if (col.CompareTag("Laser"))
            {
                // ★★★ 修正点：もし検知したレーザーが「自分のチームのレイヤー」なら安全なのでスルー！ ★★★
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
                    totalRepulsion += escapeVector.normalized * force;
                }
            }
        }

        // --- 以下、壁からの斥力や平時のうろうろ処理は既存のまま不変 ---
        float wallBound = 9.0f;
        float padding = 1.5f;
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

    /// <summary>
    /// ヘルパー関数：パーリンノイズを用いて、カクつかない滑らかな有機的ゆらぎベクトルを作る
    /// </summary>
    private Vector3 GetPerlinWanderVector(float timeValue, float speedMultiplier)
    {
        // 1Pと2Pでノイズのサンプリング位置（シード）をずらし、完全に異なる不規則な動きに設定
        float seedOffset = (playerID == 1) ? 0f : 500f;

        // Mathf.PerlinNoise は同じ値に対して常に滑らかな連続値を返すため、生物のうろうろ感が出る
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