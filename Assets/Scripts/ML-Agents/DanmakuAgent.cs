using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem; // ★ 追加

public class DanmakuAgent : Agent
{
    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler;
    [SerializeField] private Transform opponent;
    public int playerID = 1;

    [Header("Input System Actions")]
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction skillZAction;
    [SerializeField] private InputAction skillXAction;
    [SerializeField] private InputAction skillCAction;
    [SerializeField] private InputAction skillVAction;
    [SerializeField] private InputAction slowAction;
    [SerializeField] private InputAction barrierAction;

    public override void Initialize()
    {
        playerMove = GetComponent<PlayerMove>();
        hitHandler = GetComponentInChildren<PlayerHitHandler>();

        // アクションの有効化
        moveAction.Enable();
        skillZAction.Enable();
        skillXAction.Enable();
        skillCAction.Enable();
        skillVAction.Enable();
        slowAction.Enable();
        barrierAction.Enable();
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!PlayerMove.CanInput || (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal))
        {
            playerMove.currentFrameInput = new PlayerMove.ReplayFrame();
            return;
        }

        var discrete = actions.DiscreteActions;

        // 移動の復元
        float h = 0, v = 0;
        if (discrete[0] == 1) h = -1; else if (discrete[0] == 2) h = 1;
        if (discrete[1] == 1) v = 1; else if (discrete[1] == 2) v = -1;

        // スキルとアルティメットの復元
        bool z = (discrete[2] == 1);
        bool x = (discrete[2] == 2);
        bool c = (discrete[2] == 3);
        bool v_key = (discrete[2] == 4);
        bool ultimate = (discrete[2] == 5); // ★ 同時押し判定の結果

        playerMove.currentFrameInput = new PlayerMove.ReplayFrame
        {
            h = h,
            v = v,
            slow = (discrete[3] == 1),
            shotZ = z,
            shotX = x,
            shotC = c,
            shotV = v_key,
            //barrier = (discrete[4] == 1), // ★ 追加
            //ultimate = ultimate            // ★ 追加
        };

        AddReward(0.001f);
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. 自分の座標 (3つのfloat: x, y, z)
        sensor.AddObservation(transform.localPosition);

        // 2. 相手の座標 (3つのfloat: x, y, z)
        if (opponent != null)
        {
            sensor.AddObservation(opponent.localPosition);
        }
        else
        {
            // 相手がいない場合はゼロで埋める (サイズを維持するため)
            sensor.AddObservation(Vector3.zero);
        }
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (InputManager.Instance == null || !PlayerMove.CanInput ||
            (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal))
        {
            return;
        }

        var discrete = actionsOut.DiscreteActions;
        discrete.Clear();

        // ★ 自分のIDに応じて参照先を切り替える (playerIDが1なら1P用、それ以外なら2P用)
        var inputSet = (playerID == 1) ? InputManager.Instance.player1 : InputManager.Instance.player2;

        // 1. 移動 (inputSetから読み取る)
        Vector2 m = inputSet.move.action.ReadValue<Vector2>();
        if (m.x < -0.5f) discrete[0] = 1; else if (m.x > 0.5f) discrete[0] = 2;
        if (m.y > 0.5f) discrete[1] = 1; else if (m.y < -0.5f) discrete[1] = 2;

        // 2. スキル判定
        bool pZ = inputSet.skillZ.action.IsPressed();
        bool pX = inputSet.skillX.action.IsPressed();
        bool pC = inputSet.skillC.action.IsPressed();
        bool pV = inputSet.skillV.action.IsPressed();

        if (pZ && pX) discrete[2] = 5;
        else if (pZ) discrete[2] = 1;
        else if (pX) discrete[2] = 2;
        else if (pC) discrete[2] = 3;
        else if (pV) discrete[2] = 4;

        // 3. 低速移動とバリア
        if (inputSet.slow.action.IsPressed()) discrete[3] = 1;
        if (inputSet.barrier.action.IsPressed()) discrete[4] = 1;
    }
}