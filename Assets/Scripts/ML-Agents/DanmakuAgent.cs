using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DanmakuAgent : Agent
{
    private PlayerMove playerMove;

    // 他のエージェント（対戦相手）の位置を知るための参照
    [SerializeField] private Transform opponent;

    public override void Initialize()
    {
        playerMove = GetComponent<PlayerMove>();
    }

    public override void OnEpisodeBegin()
    {
        // エピソード開始時のリセット処理（位置を戻すなど）
        // 1vs1の場合は勝敗が決まった時に呼ばれる
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // AIが状況を判断するための「情報」

        // 1. 自分の位置 (Vector3 = 3つの数値)
        sensor.AddObservation(transform.localPosition);

        // 2. 相手の位置 (Vector3 = 3つの数値)
        if (opponent != null)
        {
            sensor.AddObservation(opponent.localPosition);
        }
        else
        {
            // 相手がいない場合も、0を3つ送って観測値の合計を6に固定する
            // これにより "Fewer observations than vector observation size" 警告を防ぎます
            sensor.AddObservation(Vector3.zero);
        }

        // ヒント：ここに RayPerceptionSensor2D を追加した場合は、
        // インスペクター側で「Use Child Sensors」にチェックを入れる必要があります。
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var discrete = actions.DiscreteActions;

        // 分割した Branch から入力を復元
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
        AddReward(0.001f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discrete = actionsOut.DiscreteActions;

        // 水平移動 (Branch 0)
        discrete[0] = 0;
        if (Input.GetKey(KeyCode.LeftArrow)) discrete[0] = 1;
        else if (Input.GetKey(KeyCode.RightArrow)) discrete[0] = 2;

        // 垂直移動 (Branch 1)
        discrete[1] = 0;
        if (Input.GetKey(KeyCode.UpArrow)) discrete[1] = 1;
        else if (Input.GetKey(KeyCode.DownArrow)) discrete[1] = 2;

        // スキル (Branch 2)
        discrete[2] = 0;
        if (Input.GetKey(KeyCode.Z)) discrete[2] = 1;
        else if (Input.GetKey(KeyCode.X)) discrete[2] = 2;
        else if (Input.GetKey(KeyCode.C)) discrete[2] = 3; // discrete[3]を[2]へ
        else if (Input.GetKey(KeyCode.V)) discrete[2] = 4; // discrete[4]を[2]へ

        // 低速 (Branch 3)
        discrete[3] = Input.GetKey(KeyCode.LeftShift) ? 1 : 0; // discrete[3]へ
    }
}