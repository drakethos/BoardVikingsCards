using System.IO;
using UnityEngine;

namespace Cards.Util;

public static class Loader
{
    /// <summary>Path of a loose asset file shipped with the mod (plugin folder first, then beside the DLL), or null.</summary>
    public static string? FindAsset(string fileName)
    {
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

        string assemblyDir = Path.GetDirectoryName(typeof(Loader).Assembly.Location) ?? string.Empty;
        string beside = Path.Combine(assemblyDir, "Assets", fileName);
        return File.Exists(beside) || Directory.Exists(beside) ? beside : null;
    }
}
