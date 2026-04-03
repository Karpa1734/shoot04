// BossPracticeManager.cs
using UnityEngine;

public static class BossPracticeManager
{
    public static bool IsPracticeMode = false;
    public static int TargetPhaseIndex = 0;
    // ★追加：選ばれたボスのプレハブを保持
    public static GameObject SelectedBossPrefab = null;

    public static void SetPracticePhase(GameObject bossPrefab, int index)
    {
        IsPracticeMode = true;
        SelectedBossPrefab = bossPrefab;
        TargetPhaseIndex = index;
    }

    public static void Clear()
    {
        IsPracticeMode = false;
        TargetPhaseIndex = 0;
        SelectedBossPrefab = null;
    }
}