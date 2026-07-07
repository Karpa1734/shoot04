using UnityEngine;

public static class GameSelectionData
{
    public enum GameMode { Story, VsCom, VsPlayer, VsNetwork, Practice }
    public static GameMode CurrentMode;

    public static int SelectedCharacterP1 = 0; // 0-7: キャラ, 8: Random
    public static int SelectedCharacterP2 = 1;
    // =========================================================================
    // ⭕【新規追加】：バトルシーン生成時に2P(CPU)の自動回避AIを起動させるための静的フラグ
    // =========================================================================
    public static bool UseAutoEvadeAI = false;
}