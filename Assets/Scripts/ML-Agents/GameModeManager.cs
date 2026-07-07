using UnityEngine;

/// <summary>
/// ゲーム全体のモード（ストーリー / VS）を管理するマネージャー
/// インスペクターからリアルタイムに切り替え可能
/// </summary>
public class GameModeManager : MonoBehaviour
{
    public enum Mode { Versus, Story }

    // ★ 他のスクリプトからの静的参照（GameModeManager.CurrentMode）を維持するために static で残す
    public static Mode CurrentMode = Mode.Story;
    public static bool IsStoryMode => CurrentMode == Mode.Story;

    [Header("Game Mode Settings")]
    [Tooltip("インスペクターからモードを切り替えます")]
    [SerializeField] private Mode _editorMode = Mode.Story;

    private void Awake()
    {
        // ゲーム開始時に、インスペクターで設定されたモードを静的データに同期
        CurrentMode = _editorMode;
    }

    /// <summary>
    /// Unityエディタ上でインスペクターの値を変更した瞬間に呼び出される特殊関数
    /// </summary>
    private void OnValidate()
    {
        // ゲームを実行していなくても、エディタ上でドロップダウンを切り替えた瞬間に即座に static へ反映
        CurrentMode = _editorMode;
    }
}