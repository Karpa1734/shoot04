using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DanmakuAgent : Agent
{
    private PlayerMove playerMove;
    private PlayerHitHandler hitHandler; // ★ 追加
    [SerializeField] private Transform opponent;
    public int playerID = 1;

    public override void Initialize()
    {
        playerMove = GetComponent<PlayerMove>(); 
        hitHandler = GetComponentInChildren<PlayerHitHandler>();
    }

    public override void OnEpisodeBegin()
    {
        if (opponent == null)
        {
            var pMove = GetComponent<PlayerMove>();
            foreach (var p in PlayerMove.AllPlayers)
            {
                if (p != pMove)
                {
                    opponent = p.transform;
                    break;
                }
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);

        if (opponent != null)
        {
            sensor.AddObservation(opponent.localPosition);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // ★ 追加：カウントダウン中（入力禁止時）は入力をゼロにする
        // ★ カウントダウン中、またはスタン中（Normal以外）は入力をゼロにする
        if (!PlayerMove.CanInput || (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal))
        {
            playerMove.currentFrameInput = new PlayerMove.ReplayFrame();
            return;
        }

        var discrete = actions.DiscreteActions;

        float h = 0, v = 0;
        if (discrete[0] == 1) h = -1; else if (discrete[0] == 2) h = 1;
        if (discrete[1] == 1) v = 1; else if (discrete[1] == 2) v = -1;

        bool z = (discrete[2] == 1);
        bool x = (discrete[2] == 2);
        bool c = (discrete[2] == 3);
        bool v_key = (discrete[2] == 4);
        bool slow = (discrete[3] == 1);

        playerMove.currentFrameInput = new PlayerMove.ReplayFrame
        {
            h = h,
            v = v,
            slow = slow,
            shotZ = z,
            shotX = x,
            shotC = c,
            shotV = v_key
        };

        // 学習用の微小な報酬（カウントダウン中は加算されない）
        AddReward(0.001f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // カウントダウン中やスタン中は入力を受け付けない
        if (!PlayerMove.CanInput || (hitHandler != null && hitHandler.currentState != PlayerHitHandler.PlayerState.Normal))
        {
            return;
        }

        var discrete = actionsOut.DiscreteActions;
        discrete.Clear();

        if (playerID == 1)
        {
            // 移動
            if (Input.GetKey(KeyCode.LeftArrow)) discrete[0] = 1;
            else if (Input.GetKey(KeyCode.RightArrow)) discrete[0] = 2;
            if (Input.GetKey(KeyCode.UpArrow)) discrete[1] = 1;
            else if (Input.GetKey(KeyCode.DownArrow)) discrete[1] = 2;

            // スキル
            if (Input.GetKey(KeyCode.Z)) discrete[2] = 1;
            else if (Input.GetKey(KeyCode.X)) discrete[2] = 2;
            else if (Input.GetKey(KeyCode.C)) discrete[2] = 3;
            else if (Input.GetKey(KeyCode.V)) discrete[2] = 4;

            // ★ 追加：低速移動 (LeftShift)
            if (Input.GetKey(KeyCode.LeftShift)) discrete[3] = 1;
        }
        else
        {
            // P2 (A,D,W,S)
            if (Input.GetKey(KeyCode.A)) discrete[0] = 1;
            else if (Input.GetKey(KeyCode.D)) discrete[0] = 2;
            if (Input.GetKey(KeyCode.W)) discrete[1] = 1;
            else if (Input.GetKey(KeyCode.S)) discrete[1] = 2;

            // P2 スキル (F,G,H,Jなど)
            if (Input.GetKey(KeyCode.F)) discrete[2] = 1;
            else if (Input.GetKey(KeyCode.G)) discrete[2] = 2;

            // ★ 追加：P2 低速移動 (RightShift)
            if (Input.GetKey(KeyCode.RightShift)) discrete[3] = 1;
        }
    }
}