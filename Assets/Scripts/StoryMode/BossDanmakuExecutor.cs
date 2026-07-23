using UnityEngine;

/// <summary>
/// AIの手を離れたストーリーボス専用の独立弾幕実行モジュール
/// PlayerDanmakuEmitter を汚さずに、シンプルな全方位弾・N-Way弾などを一括生成します
/// </summary>
public class BossDanmakuExecutor : MonoBehaviour
{
    private PlayerDanmakuEmitter _emitter;
    private PlayerStatusManager _statusManager;

    private void Awake()
    {
        _statusManager = GetComponentInParent<PlayerStatusManager>();

        // 自身（または親）にアタッチされている有効な Emitter を自動取得
        PlayerDanmakuEmitter[] emitters = GetComponentsInChildren<PlayerDanmakuEmitter>(true);
        foreach (var em in emitters)
        {
            if (em != null) { _emitter = em; break; }
        }
    }

    /// <summary>
    /// ⭕ 全方位弾（RoundShot）を射出
    /// </summary>
    public void FireRoundShot(BulletData data, Vector3 pos, int count, float speed, float startAngle = 0f, float delay = 0f)
    {
        if (data == null || count <= 0 || _emitter == null) return;

        float stepAngle = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + (stepAngle * i);
            _emitter.ExecuteSubShot(data, pos, speed, angle, 0f, speed, GetAssignedTag(), GetAssignedLayer(), delay);
        }
    }

    /// <summary>
    /// 📐 扇形・N-Way弾（WideShot）を指定角度に向けて射出
    /// </summary>
    public void FireWideShot(BulletData data, Vector3 pos, int way, float speed, float centerAngle, float wideAngle, float delay = 0f)
    {
        if (data == null || way <= 0 || _emitter == null) return;

        if (way == 1)
        {
            _emitter.ExecuteSubShot(data, pos, speed, centerAngle, 0f, speed, GetAssignedTag(), GetAssignedLayer(), delay);
            return;
        }

        float startAngle = centerAngle - (wideAngle / 2f);
        float stepAngle = wideAngle / (way - 1);

        for (int i = 0; i < way; i++)
        {
            float angle = startAngle + (stepAngle * i);
            _emitter.ExecuteSubShot(data, pos, speed, angle, 0f, speed, GetAssignedTag(), GetAssignedLayer(), delay);
        }
    }

    /// <summary>
    /// 🎯 自機狙い / 自機外しの N-Way弾（AimedNWay）を相手に向けて射出
    /// </summary>
    public void FireAimedNWayShot(BulletData data, Vector3 pos, int way, float speed, float wideAngle, float angleOffset = 0f, float delay = 0f)
    {
        if (data == null || way <= 0) return;

        float targetAngle = GetAngleToOpponent(pos) + angleOffset;
        FireWideShot(data, pos, way, speed, targetAngle, wideAngle, delay);
    }

    /// <summary>
    /// 📏 同一方向に指定数の弾を並べて撃つ直線連射弾（LineShot）
    /// </summary>
    public void FireLineShot(BulletData data, Vector3 pos, int count, float speed, float angle, float speedStep = 0.3f, float delay = 0f)
    {
        if (data == null || count <= 0 || _emitter == null) return;

        for (int i = 0; i < count; i++)
        {
            float currentSpeed = speed + (speedStep * i);
            _emitter.ExecuteSubShot(data, pos, currentSpeed, angle, 0f, currentSpeed, GetAssignedTag(), GetAssignedLayer(), delay);
        }
    }

    // --- 内部ヘルパー ---
    private string GetAssignedTag() => (_statusManager != null && _statusManager.playerId == 1) ? "PlayerBullet" : "EnemyBullet";
    private int GetAssignedLayer() => LayerMask.NameToLayer((_statusManager != null && _statusManager.playerId == 1) ? "Player1Bullet" : "Player2Bullet");

    private float GetAngleToOpponent(Vector3 fromPos)
    {
        Transform target = null;
        foreach (var p in PlayerMove.AllPlayers)
        {
            if (p != null && p.gameObject != transform.root.gameObject)
            {
                target = p.transform;
                break;
            }
        }
        if (target != null)
        {
            Vector3 dir = target.position - fromPos;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
        return (_statusManager != null && _statusManager.playerId == 2) ? 180f : 0f;
    }
}