[System.Serializable]
public class GameSaveData
{
    public int unlockedLevelIndex = 1;  // 1 = first level (0 = main menu)
    public int totalDeaths = 0;         // Increments every death, never resets
}