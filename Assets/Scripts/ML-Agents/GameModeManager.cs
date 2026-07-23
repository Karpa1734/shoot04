using UnityEngine;

/// <summary>
/// ゲーム全体のモード（ストーリー / VS）を管理するマネージャー
/// </summary>
public class GameModeManager : MonoBehaviour
{
    public enum Mode { Versus, Story }

    public static Mode CurrentMode = Mode.Story;
    public static bool IsStoryMode => CurrentMode == Mode.Story;

    [Header("Game Mode Settings")]
    [Tooltip("【デバッグ用】ゲームシーンを直接再生した時のみ適用されます")]
    [SerializeField] private Mode _editorMode = Mode.Story;

    private void Awake()
    {
        // 🌟【核心の修正】：
        // キャラ選択画面を通過して遷移してきた（FromCharacterSelect == true）場合は、
        // タイトルやキャラ選択で決定したモード（Story）を絶対保護し、インスペクター値での上書きをブロック！
        if (!PlayerStatusManager.FromCharacterSelect)
        {
            CurrentMode = _editorMode;
            Debug.Log($"<color=yellow>🔧 [DEBUG MODE] シーン直接起動のため、インスペクター設定（{_editorMode}）を適用しました。</color>");
        }
    }

    private void OnValidate()
    {
        // エディタ上で手動変更した時のみ反映
        if (!Application.isPlaying)
        {
            CurrentMode = _editorMode;
        }
    }
}