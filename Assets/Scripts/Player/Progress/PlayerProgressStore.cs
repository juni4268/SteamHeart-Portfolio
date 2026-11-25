using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class PlayerProgressData
{
    public int version = 2;
    [FormerlySerializedAs("coins")]
    public int scrap;
    public int attackPowerBonus;
    [FormerlySerializedAs("healthShards")]
    public int healthPieces;
    public int upgradeMaterials;
    public int bonusMaxHP;
    public float hitInvincibilityBonus;
    public string[] ownedWeaponSkins = Array.Empty<string>();
    public string equippedWeaponSkin = string.Empty;
    public CutsceneId[] watchedCutscenes = Array.Empty<CutsceneId>();
}

public enum PlayerProgressLoadResult
{
    Missing,
    Loaded,
    Failed
}

public static class PlayerProgressStore
{
    public static string SavePath => Path.Combine(Application.persistentDataPath, "progress.json");

    public static bool TryLoad(out PlayerProgressData progress)
        => Load(out progress) == PlayerProgressLoadResult.Loaded;

    public static PlayerProgressLoadResult Load(out PlayerProgressData progress)
        => Load(SavePath, out progress);

    internal static PlayerProgressLoadResult Load(string path, out PlayerProgressData progress)
    {
        progress = null;
        try
        {
            PlayerProgressData loaded = Deserialize(File.ReadAllText(path));
            if (!IsValid(loaded))
            {
                Debug.LogWarning("Progress save has an unsupported version or invalid data.");
                return PlayerProgressLoadResult.Failed;
            }
            progress = loaded;
            return PlayerProgressLoadResult.Loaded;
        }
        catch (FileNotFoundException) { return PlayerProgressLoadResult.Missing; }
        catch (DirectoryNotFoundException) { return PlayerProgressLoadResult.Missing; }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException)
        {
            Debug.LogWarning($"Could not read progress: {error.Message}");
            return PlayerProgressLoadResult.Failed;
        }
    }

    public static bool Save(PlayerProgressData progress)
        => Save(SavePath, progress);

    internal static bool Save(string path, PlayerProgressData progress)
    {
        if (!IsValid(progress)) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(progress, true));
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
            return true;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            Debug.LogWarning($"Could not save progress: {error.Message}");
            return false;
        }
    }

    public static bool IsValid(PlayerProgressData progress)
    {
        return progress != null && (progress.version == 1 || progress.version == 2) && progress.scrap >= 0 &&
            progress.attackPowerBonus >= 0 && progress.healthPieces >= 0 && progress.upgradeMaterials >= 0 &&
            progress.bonusMaxHP >= 0 && progress.hitInvincibilityBonus >= 0f &&
            !float.IsNaN(progress.hitInvincibilityBonus) && !float.IsInfinity(progress.hitInvincibilityBonus) &&
            (progress.ownedWeaponSkins == null || Array.TrueForAll(progress.ownedWeaponSkins, id => !string.IsNullOrWhiteSpace(id))) &&
            (progress.watchedCutscenes == null || Array.TrueForAll(progress.watchedCutscenes,
                id => id != CutsceneId.None && Enum.IsDefined(typeof(CutsceneId), id)));
    }

    public static PlayerProgressData Deserialize(string json)
    {
        if (json == null) return null;
        // FormerlySerializedAs handles Unity assets, but not JsonUtility.FromJson field aliases.
        json = RenameLegacyField(json, "coins", nameof(PlayerProgressData.scrap));
        json = RenameLegacyField(json, "healthShards", nameof(PlayerProgressData.healthPieces));
        // Missing JSON fields retain their existing values. Do not let the DTO's
        // default version turn an empty or unrelated object into a valid save.
        var progress = new PlayerProgressData { version = 0 };
        JsonUtility.FromJsonOverwrite(json, progress);
        return progress;
    }

    private static string RenameLegacyField(string json, string oldName, string currentName)
    {
        if (Regex.IsMatch(json, "\"" + currentName + "\"\\s*:")) return json;
        return Regex.Replace(json, "\"" + oldName + "\"(?=\\s*:)", "\"" + currentName + "\"");
    }
}
