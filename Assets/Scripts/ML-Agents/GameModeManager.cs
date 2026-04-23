public static class GameModeManager
{
    public enum Mode { Versus, Story }
    public static Mode CurrentMode = Mode.Story;

    public static bool IsStoryMode => CurrentMode == Mode.Story;
}