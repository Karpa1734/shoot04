// PracticeResultUI.cs (新規作成)
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PracticeResultUI : MonoBehaviour
{
    public static PracticeResultUI Instance;

    [Header("UI Panels")]
    public GameObject menuPanel; // ポーズメニュー流用のパネル

    [Header("Buttons")]
    public Button retryButton;   // 「最初から始める」
    public Button titleButton;   // 「タイトルに戻る」
    public Button replayButton;  // 「リプレイを保存する」 (今回は非表示)

    void Awake()
    {
        Instance = this;
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    public void ShowMenu()
    {
        if (menuPanel == null) return;

        // メニューを表示
        menuPanel.SetActive(true);

        // リプレイ保存ボタンはいったん非表示にする
        if (replayButton != null) replayButton.gameObject.SetActive(false);

        // 時間を止める（スロー演出等がない場合）
        Time.timeScale = 0f;
    }

    // ボタンに割り当てるメソッド：最初から始める
    public void OnRetrySelected()
    {
        Time.timeScale = 1f;
        // BossPracticeManager の TargetPhaseIndex を維持したままリロード
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ボタンに割り当てるメソッド：タイトルに戻る
    public void OnTitleSelected()
    {
        Time.timeScale = 1f;
        // 練習モードのフラグは保持したままタイトルへ（前回の修正により自動で演習メニューが開く）
        SceneManager.LoadScene("Title");
    }
}