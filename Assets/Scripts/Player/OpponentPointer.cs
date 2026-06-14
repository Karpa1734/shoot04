using UnityEngine;

public class OpponentPointer : MonoBehaviour
{
    private PlayerMove _myMove;
    private Transform _target;

    [Header("🎯 スケール固定設定")]
    [SerializeField, Tooltip("親の大きさに影響されず、画面上で維持したい矢印の絶対サイズ")]
    private Vector3 targetScale = new Vector3(1f, 1f, 1f);

    void Start()
    {
        // 自分の親にある PlayerMove を取得
        _myMove = GetComponentInParent<PlayerMove>();
    }

    void Update()
    {
        // ターゲットがまだ設定されていない場合は探す
        if (_target == null)
        {
            FindTarget();
            if (_target == null) return;
        }

        // =========================================================================
        // 📐 1. 向き・回転（Rotation）の計算
        // =========================================================================
        // 相手への方向ベクトルを計算
        Vector3 direction = _target.position - transform.position;

        // ベクトルから角度（ラジアン）を求め、度数法に変換
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 矢印の回転を更新（Z軸回転）
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // =========================================================================
        // 🌟 2. 【大修正】親の巨大化・縮小化を相殺する「絶対スケール固定」
        // =========================================================================
        // 💡 理由：Unityの親子関係では、自身の localScale は「親のlocalScale × 自分のlocalScale」になります。
        //          そのため、設定したい目標サイズ(targetScale)を、親の現在のスケールで割ることで、
        //          親が1.5倍になろうが10倍になろうが、矢印自体の画面上の大きさを100%完璧に固定化します！
        if (transform.parent != null)
        {
            Vector3 parentScale = transform.parent.localScale;

            // 親のスケールが0になることによるZeroDivisionクラッシュを安全弁でガード
            transform.localScale = new Vector3(
                parentScale.x > 0.001f ? targetScale.x / parentScale.x : targetScale.x,
                parentScale.y > 0.001f ? targetScale.y / parentScale.y : targetScale.y,
                parentScale.z > 0.001f ? targetScale.z / parentScale.z : targetScale.z
            );
        }
        else
        {
            // もし親がいない単動状態であれば、そのままの絶対値を適用
            transform.localScale = targetScale;
        }
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