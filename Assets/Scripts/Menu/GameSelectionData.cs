using UnityEngine;

public static class GameSelectionData
{
    public enum GameMode { Story, VsCom, VsPlayer, VsNetwork, Practice }
    public static GameMode CurrentMode;

    public static int SelectedCharacterP1 = 0; // 0-7: ƒLƒƒƒ‰, 8: Random
    public static int SelectedCharacterP2 = 1;
}