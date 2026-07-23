using System.Collections;
using UnityEngine;

/// <summary>
/// 各スペルカードの具体的な移動・弾幕パターンを記述する基底クラス
/// </summary>
public abstract class SpellCardPattern : MonoBehaviour
{
    protected PlayerMove bossMove;
    protected PlayerStatusManager bossStatus;
    protected PlayerDanmakuEmitter activeEmitter;

    public virtual void Initialize(PlayerStatusManager status)
    {
        bossStatus = status;
        bossMove = status.GetComponent<PlayerMove>();

        // 現在アクティブなEmitterを取得
        PlayerDanmakuEmitter[] emitters = status.GetComponentsInChildren<PlayerDanmakuEmitter>(true);
        foreach (var em in emitters)
        {
            if (em.enabled) { activeEmitter = em; break; }
        }
    }

    /// <summary>
    /// スペルカード開始時に実行されるルーチン（移動や弾幕射出を記述）
    /// </summary>
    public abstract IEnumerator ExecutePatternRoutine();

    /// <summary>
    /// スペルカード終了（撃破・時間切れ）時の後処理
    /// </summary>
    public virtual void OnSpellEnd()
    {
        StopAllCoroutines();
    }
}