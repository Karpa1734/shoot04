// --- SpellCardManager.cs (新規作成) ---
using UnityEngine;

public class SpellCardManager : MonoBehaviour
{
    private static SpellCardManager _instance;
    public static SpellCardManager Instance => _instance;

    // 現在、画面全体に「聖少女領域（VJT）」を展開しているプレイヤーの参照（誰もいなければnull）
    private PlayerStatusManager activeZoneOwner = null;
    public bool IsAnyZoneActive => activeZoneOwner != null;

    void Awake()
    {
        if (_instance == null) _instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 🌟【早い者勝ちジャッジ】：領域展開を要求する
    /// </summary>
    public bool TryRequestVJT(PlayerStatusManager requestPlayer)
    {
        // 既に誰かが領域を展開している場合は、要求を完全にはじく（早い者勝ちルール）
        if (IsAnyZoneActive)
        {
            Debug.Log($"<color=red>❌【VJT排他拒否】{requestPlayer.gameObject.name} の展開要求は、既に領域がアクティブなため却下されました。</color>");
            return false;
        }

        // 誰も展開していなければ、主権をこのプレイヤーに渡して承認
        activeZoneOwner = requestPlayer;
        Debug.Log($"<color=magenta>🔮【VJT主権確立】{requestPlayer.gameObject.name} が「聖少女領域（VJT）」を展開しました！</color>");
        return true;
    }

    /// <summary>
    /// 領域の解除（主権の破棄）
    /// </summary>
    public void ReleaseVJT(PlayerStatusManager requestPlayer)
    {
        if (activeZoneOwner == requestPlayer)
        {
            activeZoneOwner = null;
            Debug.Log($"<color=gray>🏳️【VJT領域解放】{requestPlayer.gameObject.name} の領域が終了し、世界が通常状態に戻りました。</color>");
        }
    }
}