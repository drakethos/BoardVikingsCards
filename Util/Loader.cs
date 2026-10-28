using System.IO;
using UnityEngine;

namespace Cards.Util;

public static class Loader
{
    /// <summary>
    /// Path of a loose asset file shipped with the mod, or null. The package installs as
    /// BepInEx/plugins/DrakeMods-BoardVikings_CardGames/{BoardVikings-CardGames.dll, Assets/...}, so the Assets folder is looked
    /// for beside the DLL first (works wherever a mod manager or a manual install put the mod folder); the plugin folders by
    /// name are only a fallback for installs where the DLL was moved away from its Assets folder.
    /// </summary>
    public static string? FindAsset(string fileName)
    {
        string assemblyDir = Path.GetDirectoryName(typeof(Loader).Assembly.Location) ?? string.Empty;
        if (assemblyDir.Length > 0)
        {
            string beside = Path.Combine(assemblyDir, "Assets", fileName);
            if (File.Exists(beside) || Directory.Exists(beside))
                return beside;
        }

        string[] candidateFolders =
        {
            "DrakeMods-BoardVikings_CardGames",
            "DrakeMods-BoardVikings-CardGames",
            "DrakeMod-BoardVikings-CardGames",
            "BoardVikings-CardGames"
        };

        foreach (string folder in candidateFolders)
        {
            string path = Path.Combine(BepInEx.Paths.PluginPath, folder, "Assets", fileName);
            if (File.Exists(path) || Directory.Exists(path))
                return path;
        }

        return null;
    }
}
