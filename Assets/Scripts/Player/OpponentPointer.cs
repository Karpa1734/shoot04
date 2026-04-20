using UnityEngine;

public class OpponentPointer : MonoBehaviour
{
    private PlayerMove _myMove;
    private Transform _target;

    void Start()
    {
        // 自分の親にある PlayerMove を取得
        _myMove = GetComponentInParent<PlayerMove>();
    }

    void Update()
    {
        //if (!PlayerMove.CanInput) return;
        // ターゲットがまだ設定されていない場合は探す
        if (_target == null)
        {
            FindTarget();
            if (_target == null) return;
        }

        // 相手への方向ベクトルを計算
        Vector3 direction = _target.position - transform.position;

        // ベクトルから角度（ラジアン）を求め、度数法に変換
        // Arrow2.png は右向きなので、計算結果そのままで適合します
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 矢印の回転を更新（Z軸回転）
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void FindTarget()
    {
        // PlayerMove が管理しているリストから自分以外のプレイヤーを探す
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != _myMove)
            {
                _target = p.transform;
                break;
            }
        }
    }
}