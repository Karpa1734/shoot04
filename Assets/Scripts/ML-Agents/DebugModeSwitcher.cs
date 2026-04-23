using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugModeSwitcher : MonoBehaviour
{
    void Update()
    {
        // [V]キー：対戦モードに切り替えてリロード
        if (Input.GetKeyDown(KeyCode.V))
        {
            GameModeManager.CurrentMode = GameModeManager.Mode.Versus;
            ReloadScene();
        }

        // [S]キー：ストーリーモードに切り替えてリロード
        if (Input.GetKeyDown(KeyCode.S))
        {
            GameModeManager.CurrentMode = GameModeManager.Mode.Story;
            ReloadScene();
        }
    }

    private void ReloadScene()
    {
        // モードを反映させるため、現在のシーンを再読み込みする
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        Debug.Log($"Game Mode Switched: {GameModeManager.CurrentMode}");
    }
}