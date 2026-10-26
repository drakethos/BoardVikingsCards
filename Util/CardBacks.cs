using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cards.Util;

/// <summary>
/// The card back designs. They live on one picture (Assets/card_backs.png, 3 columns by 5 rows) that is cut into tiles when the
/// game starts, so changing a back is a file swap: no Unity build. A deck is made in one of these backs, cards pulled from it
/// carry that back, and a giant card can show any of them.
/// </summary>
internal static class CardBacks
{
    internal const string SheetFile = "card_backs.png";

    /// <summary>The back used where none is chosen (the red lattice).</summary>
    internal const int DefaultBack = 3;

    /// <summary>Item custom data key holding a card's back design.</summary>
    internal const string BackKey = "BoardVikings_Back";

    private const int Cols = 3, Rows = 5;
    private const int X0 = 2, Y0 = 2, PitchX = 140, PitchY = 190, TileW = 136, TileH = 186;
    private const int CornerRadius = 12;

    /// <summary>Names in sheet order (left to right, top to bottom).</summary>
    internal static readonly string[] Names =
    {
        "Red Suits", "Green Suits", "Blue Suits",
        "Red Lattice", "Green Lattice", "Blue Lattice",
        "Red Frame", "Blue Plain", "Blue Frame",
        "Red Diamonds", "Green Diamonds", "Blue Diamonds",
        "Red Plain", "Green Plain", "Green Frame"
    };

    internal static int Count => Names.Length;

    /// <summary>The back designs, portrait, with rounded corners.</summary>
    internal static Sprite[] Sprites { get; private set; } = Array.Empty<Sprite>();

    /// <summary>One deck box icon per back design.</summary>
    internal static Sprite[] DeckIcons { get; private set; } = Array.Empty<Sprite>();

    internal static void Init()
    {
        if (Sprites.Length > 0)
            return;

        var tiles = new List<Sprite>(Count);
        var path = Loader.FindAsset(SheetFile);
        if (path != null)
        {
            try
            {
                var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (LoadPng(sheet, File.ReadAllBytes(path)))
                {
                    for (int row = 0; row < Rows; row++)
                    {
                        for (int col = 0; col < Cols; col++)
                            tiles.Add(Cut(sheet, col, row, Names[row * Cols + col]));
                    }

                    Jotunn.Logger.LogInfo($"Loaded {tiles.Count} card backs from {SheetFile}.");
                }
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Could not read {SheetFile} ({ex.Message}); using the plain back.");
                tiles.Clear();
            }
        }
        else
        {
            Jotunn.Logger.LogWarning($"{SheetFile} not found next to the mod; using the plain back.");
        }

        if (tiles.Count != Count)
        {
            tiles.Clear();
            var plain = CardIcons.FallbackBack();
            for (int i = 0; i < Count; i++)
                tiles.Add(plain);
        }

        Sprites = tiles.ToArray();
        var icons = new Sprite[Count];
        for (int i = 0; i < Count; i++)
            icons[i] = CardIcons.DeckBoxFor(Sprites[i].texture, "CardDeckBox_" + Names[i]);
        DeckIcons = icons;

        SpriteTools.Register("back", Sprites[DefaultBack]);
        SpriteTools.Register("deck", DeckIcons[DefaultBack]);
    }

    internal static Sprite Back(int index) => Sprites[Clamp(index)];

    internal static Sprite DeckIcon(int index) => DeckIcons[Clamp(index)];

    internal static int Clamp(int index) => Math.Max(0, Math.Min(Count - 1, index));

    /// <summary>The back design a card item carries (the default when it carries none).</summary>
    internal static int BackOf(ItemDrop.ItemData? item)
    {
        if (item?.m_customData != null && item.m_customData.TryGetValue(BackKey, out var raw) &&
            int.TryParse(raw, out var index))
            return Clamp(index);
        return DefaultBack;
    }

    internal static void SetBack(ItemDrop.ItemData item, int index)
    {
        item.m_customData ??= new Dictionary<string, string>();
        item.m_customData[BackKey] = Clamp(index).ToString();
    }

    /// <summary>
    /// Texture2D.LoadImage lives in a Unity module whose reference does not compile against the store's reference set, so it is
    /// called by name; it is part of every game build.
    /// </summary>
    private static bool LoadPng(Texture2D target, byte[] bytes)
    {
        var type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
        var method = type?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
        return method != null && (bool)method.Invoke(null, new object[] { target, bytes, false });
    }

    private static Sprite Cut(Texture2D sheet, int col, int row, string name)
    {
        int x = X0 + col * PitchX;
        int yTop = Y0 + row * PitchY;
        int y = sheet.height - yTop - TileH; // textures count rows from the bottom
        var pixels = sheet.GetPixels(x, y, TileW, TileH);

        for (int py = 0; py < TileH; py++)
        {
            for (int px = 0; px < TileW; px++)
            {
                if (Outside(px, py))
                    pixels[py * TileW + px] = Color.clear;
            }
        }

        var tile = new Texture2D(TileW, TileH, TextureFormat.RGBA32, false)
        {
            name = "CardBack_" + name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        tile.SetPixels(pixels);
        tile.Apply(false, false);
        var sprite = Sprite.Create(tile, new Rect(0, 0, TileW, TileH), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "CardBack_" + name;
        return sprite;
    }

    private static bool Outside(int x, int y)
    {
        int cx = x < CornerRadius ? CornerRadius : (x >= TileW - CornerRadius ? TileW - 1 - CornerRadius : x);
        int cy = y < CornerRadius ? CornerRadius : (y >= TileH - CornerRadius ? TileH - 1 - CornerRadius : y);
        int dx = x - cx, dy = y - cy;
        return dx * dx + dy * dy > CornerRadius * CornerRadius;
    }
}
