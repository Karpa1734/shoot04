using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StoryModeManager : MonoBehaviour
{
    public static StoryModeManager Instance { get; private set; }

    [Header("📖 全キャラのルート設定アセット一覧 (0〜7)")]
    public StoryRouteData[] allPlayerRoutes;

    public static int CurrentStageNumber = 1;
    public static StoryRouteData CurrentActiveRoute { get; private set; }

    private void Awake()
    {
        // 1. 重複破棄ロジック
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        // 🌟【重要修正】：Awake の時点でルートとボスID（シャル=1）を最速セット！
        if (CurrentActiveRoute == null && allPlayerRoutes != null && allPlayerRoutes.Length > 0)
        {
            CurrentActiveRoute = allPlayerRoutes[0]; // カリンのルート
            CurrentStageNumber = 1;

            if (CurrentActiveRoute != null && CurrentActiveRoute.stages.Count > 0)
            {
                // 1面のボスID（シャル = 1）を先回りしてセット
                int firstBossId = CurrentActiveRoute.stages[0].bossCharacterId;
                GameSelectionData.SelectedCharacterP2 = firstBossId;

                // PlayerStatusManager 側に「データベースから引け！」の指示を出す
                PlayerStatusManager.FromCharacterSelect = true;

                Debug.Log($"<color=yellow>🔧 [DEBUG AWAKE] Shoot直起動を検知。1面ボスID [{firstBossId}] (Charlotte) を先行セットしました。</color>");
            }
        }
#endif
    }

    // Start() に書いていたデバッグ処理は削除（または空にしてOK）
    private void Start()
    {
    }

    public void StartStoryMode(int selectedPlayerId)
    {
        CurrentStageNumber = 1;

        if (allPlayerRoutes != null && selectedPlayerId >= 0 && selectedPlayerId < allPlayerRoutes.Length)
        {
            CurrentActiveRoute = allPlayerRoutes[selectedPlayerId];
        }

        if (CurrentActiveRoute == null)
        {
            Debug.LogError("❌ [StoryMode] 該当する StoryRouteData が見つかりません！");
            return;
        }

        SetupAndLoadStage(CurrentStageNumber);
    }

    public void SetupAndLoadStage(int stageNum)
    {
        CurrentStageNumber = stageNum;

        if (CurrentActiveRoute == null) return;

        StoryRouteData.StageBossConfig targetStage = null;
        foreach (var st in CurrentActiveRoute.stages)
        {
            if (st.stageNumber == CurrentStageNumber)
            {
                targetStage = st;
                break;
            }
        }

        if (targetStage != null)
        {
            GameSelectionData.SelectedCharacterP2 = targetStage.bossCharacterId;
            PlayerStatusManager.FromCharacterSelect = true; // 強制ロードフラグ

            Debug.Log($"<color=gold>📖【Story Route】第 {CurrentStageNumber} 面開始！ ボスID: {targetStage.bossCharacterId}</color>");
            SceneManager.LoadScene("Shoot");
        }
        else
        {
            Debug.Log("<color=gold>🏆🏆🏆【STORY CLEAR】全ステージクリア！タイトルへ戻ります。</color>");
            SceneManager.LoadScene("Title");
        }
    }

    public void OnStageCleared()
    {
        StartCoroutine(NextStageRoutine());
    }

    private IEnumerator NextStageRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        CurrentStageNumber++;
        SetupAndLoadStage(CurrentStageNumber);
    }
}