public static class BossDebugManager
{
    public static bool IsDebugMode = false;
    public static int SelectedPhaseIndex = 0;

    // タイトル画面のボタンからこれを呼んでシーン遷移する
    public static void SetDebugPhase(int index)
    {
        IsDebugMode = true;
        SelectedPhaseIndex = index;
        // その後、UnityEngine.SceneManagement.SceneManager.LoadScene(...) を呼ぶ
    }

    public static void Reset()
    {
        IsDebugMode = false;
        SelectedPhaseIndex = 0;
    }
}