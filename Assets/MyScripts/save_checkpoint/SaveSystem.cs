using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string SavePath =>
        Application.persistentDataPath + "/spideroid_save.json";

    // ── Write save to disk ──────────────────────────────
    public static void Save(GameSaveData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath, json);
        Debug.Log("[SaveSystem] Game saved to: " + SavePath);
    }

    // ── Read save from disk ─────────────────────────────
    public static GameSaveData Load()
    {
        if (!File.Exists(SavePath))
            return null; // No save file exists yet

        string json = File.ReadAllText(SavePath);
        return JsonUtility.FromJson<GameSaveData>(json);
    }

    // ── Check if a save file exists ─────────────────────
    public static bool HasSave()
    {
        return File.Exists(SavePath);
    }

    // ── Wipe the save file (New Game) ───────────────────
    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);

        Debug.Log("[SaveSystem] Save file deleted.");
    }
}