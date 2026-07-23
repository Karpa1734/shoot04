using System.Collections;
using UnityEngine;

/// <summary>
/// AIを使わない「通常攻撃（プログラム制御）」の移動・弾幕パターンを書く基底クラス
/// </summary>
public abstract class NormalAttackPattern : MonoBehaviour
{
    protected PlayerMove bossMove;
    protected PlayerStatusManager bossStatus;
    protected PlayerDanmakuEmitter activeEmitter;

    public virtual void Initialize(PlayerStatusManager status)
    {
        bossStatus = status;
        bossMove = status.GetComponent<PlayerMove>();

        PlayerDanmakuEmitter[] emitters = status.GetComponentsInChildren<PlayerDanmakuEmitter>(true);
        foreach (var em in emitters)
        {
            if (em.enabled) { activeEmitter = em; break; }
        }
    }

    /// <summary>
    /// 通常攻撃中に実行されるコルーチン
    /// </summary>
    public abstract IEnumerator ExecutePatternRoutine();

    public virtual void OnAttackEnd()
    {
        StopAllCoroutines();
    }
}