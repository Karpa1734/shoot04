// PracticeRetryManager.cs (新規作成)
using UnityEngine;
using UnityEngine.SceneManagement;

public class PracticeRetryManager : MonoBehaviour
{
    void Update()
    {
        // 練習モード中、Rキーでクイックリトライ
        if (BossPracticeManager.IsPracticeMode && Input.GetKeyDown(KeyCode.R))
        {
            Retry();
        }
    }

    public void Retry()
    {
        // BossPracticeManager の TargetPhaseIndex は静的なので
        // シーンをリロードするだけで EnemyStatus が自動的にその段階から開始します
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}