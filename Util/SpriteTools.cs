using System.Collections.Generic;
using UnityEngine;

namespace Cards.Util;

public static class SpriteTools
{
    private static Dictionary<string, Sprite>? _cache;

    /// <summary>Warm the sprite cache from the loaded card bundle. Safe to call more than once.</summary>
    public static void EnsureLoaded()
    {
        if (_cache != null)
            return;

        _cache = new Dictionary<string, Sprite>();

        var dir = Loader.FindAsset("faces");
        if (dir == null || !System.IO.Directory.Exists(dir))
        {
            Jotunn.Logger.LogError("Cannot load card faces: Assets/faces is missing.");
            return;
        }

        foreach (var file in System.IO.Directory.GetFiles(dir, "*.png"))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "CardFace_" + name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!CardBacks.LoadPng(tex, System.IO.File.ReadAllBytes(file)))
                continue;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            _cache[name] = sprite;
        }

        Jotunn.Logger.LogInfo($"Loaded {_cache.Count} card faces from Assets/faces.");
    }

    public static Sprite? GetSprite(string spriteName)
    {
        EnsureLoaded();

        if (_cache != null && _cache.TryGetValue(spriteName, out var sprite))
            return sprite;

        Jotunn.Logger.LogWarning($"Sprite '{spriteName}' not found in cache.");
        return null;
    }

    /// <summary>Add a generated sprite (card back, deck box) so it can be found by name like the bundle's.</summary>
    public static void Register(string spriteName, Sprite sprite)
    {
        EnsureLoaded();
        if (_cache != null && sprite != null)
            _cache[spriteName] = sprite;
    }

    /// <summary>Lookup without warning (optional backs, aliases).</summary>
    public static Sprite? TryGetSprite(string spriteName)
    {
        EnsureLoaded();
        if (_cache != null && _cache.TryGetValue(spriteName, out var sprite))
            return sprite;
        return null;
    }
}
