using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cards.Util;

/// <summary>
/// Icons made at startup. Valheim draws item icons into square slots, so a portrait card would be squashed: every card gets a
/// square icon with the card centred at its true proportions. Also draws the card back and the deck box (the bundle has neither).
/// </summary>
internal static class CardIcons
{
    private const int IconSize = 128;
    private static readonly Dictionary<string, Sprite> Squares = new Dictionary<string, Sprite>(StringComparer.Ordinal);

    private static readonly Color Cream = new Color(0.97f, 0.95f, 0.90f, 1f);
    private static readonly Color Maroon = new Color(0.52f, 0.07f, 0.11f, 1f);
    private static readonly Color MaroonDark = new Color(0.36f, 0.04f, 0.08f, 1f);
    private static readonly Color MaroonLight = new Color(0.68f, 0.16f, 0.20f, 1f);
    private static readonly Color Gold = new Color(0.90f, 0.72f, 0.30f, 1f);

    /// <summary>
    /// Fallback only: when the back sheet is missing, make sure a "back" exists. The real backs come from <see cref="CardBacks"/>.
    /// </summary>
    internal static void Init()
    {
        if (SpriteTools.TryGetSprite("back") == null)
            SpriteTools.Register("back", FallbackBack());
        if (SpriteTools.TryGetSprite("deck") == null)
            SpriteTools.Register("deck", DeckBoxFor(FallbackBack().texture, "CardDeckBox"));
    }

    private static Sprite? _fallbackBack;

    /// <summary>A plain drawn back, used only when Assets/card_backs.png cannot be loaded.</summary>
    internal static Sprite FallbackBack() => _fallbackBack ??= MakeBack();

    /// <summary>Square, aspect-correct icon for a card code (cached). Falls back to the original sprite if drawing fails.</summary>
    internal static Sprite? Square(string code)
    {
        if (Squares.TryGetValue(code, out var cached))
            return cached;

        var source = SpriteTools.TryGetSprite(code);
        if (source == null)
            return null;

        Sprite made;
        try
        {
            made = PadToSquare(source, IconSize);
        }
        catch (Exception ex)
        {
            Jotunn.Logger.LogWarning($"Square icon for {code} failed ({ex.Message}); using the plain sprite.");
            made = source;
        }

        Squares[code] = made;
        return made;
    }

    /// <summary>The deck item's icon is already square.</summary>

    // ---------------------------------------------------------------- square padding on the GPU (works on unreadable textures)

    private static Sprite PadToSquare(Sprite src, int size)
    {
        var tex = src.texture;
        var r = src.textureRect;
        var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = rt;
            GL.Clear(false, true, new Color(0f, 0f, 0f, 0f));

            float aspect = r.width / r.height;
            float limit = size * 0.94f;
            float h = limit;
            float w = h * aspect;
            if (w > limit)
            {
                w = limit;
                h = w / aspect;
            }

            var dest = new Rect((size - w) * 0.5f, (size - h) * 0.5f, w, h);
            var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, size, 0f, size);
            Graphics.DrawTexture(dest, tex, uv, 0, 0, 0, 0);
            GL.PopMatrix();

            var result = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CardIcon_" + src.name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply(false, false);
            var sprite = Sprite.Create(result, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "CardIcon_" + src.name;
            return sprite;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    // ---------------------------------------------------------------- the card back

    private static Sprite MakeBack()
    {
        const int w = 140, h = 196;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                px[y * w + x] = BackPixel(x, y, w, h);
        }

        return Finish(px, w, h, "CardBack");
    }

    private static Color BackPixel(int x, int y, int w, int h)
    {
        if (OutsideRounded(x, y, w, h, 12))
            return Color.clear;

        int edge = Math.Min(Math.Min(x, w - 1 - x), Math.Min(y, h - 1 - y));
        if (edge < 7)
            return Cream;
        if (edge < 9)
            return MaroonDark;
        if (edge < 11)
            return Gold;
        if (edge < 13)
            return MaroonDark;

        // Diamond lattice.
        bool line = ((x + y) % 16) < 2 || (((x - y) % 16) + 16) % 16 < 2;
        if (line)
            return MaroonLight;

        // A cream diamond medallion in the middle.
        float dx = Math.Abs(x - w * 0.5f) / (w * 0.30f);
        float dy = Math.Abs(y - h * 0.5f) / (h * 0.21f);
        float d = dx + dy;
        if (d < 1f)
            return d > 0.82f ? Gold : (d > 0.7f ? MaroonDark : Cream);
        return Maroon;
    }

    // ---------------------------------------------------------------- the deck box

    /// <summary>A tuck box whose front is the given card back (the box takes the back's colours): front, side and top faces and a lid.</summary>
    internal static Sprite DeckBoxFor(Texture2D tile, string name)
    {
        const int s = 256;
        var px = new Color[s * s];

        // Front face 44..164 wide, 22..200 tall; the side goes back and up by (34, 24).
        const int fx0 = 44, fx1 = 164, fy0 = 22, fy1 = 200, dx = 34, dy = 24;
        int fw = fx1 - fx0 + 1, fh = fy1 - fy0 + 1;

        var average = Average(tile);
        var sideColor = Color.Lerp(average, Color.black, 0.45f);
        var sideEdge = Color.Lerp(Cream, sideColor, 0.45f);
        var topColor = Color.Lerp(average, Cream, 0.30f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Color c = Color.clear;

                if (InParallelogram(x, y, fx0, fy1, fx1, fy1, dx, dy))
                    c = topColor;

                if (InSide(x, y, fx1, fy0, fy1, dx, dy))
                    c = (x - fx1) / (float)dx > 0.86f ? sideEdge : sideColor;

                if (x >= fx0 && x <= fx1 && y >= fy0 && y <= fy1)
                    c = FrontPixel(tile, x - fx0, y - fy0, fw, fh, average);

                px[y * s + x] = c;
            }
        }

        // Soft dark outline around the opaque area so it reads on any slot colour.
        px = Outline(px, s, s, new Color(0.10f, 0.06f, 0.05f, 0.95f));
        return Finish(px, s, s, name);
    }

    private static Color FrontPixel(Texture2D tile, int x, int y, int w, int h, Color average)
    {
        int edge = Math.Min(Math.Min(x, w - 1 - x), Math.Min(y, h - 1 - y));
        if (edge < 4)
            return Cream;

        // The back design fills the front; the lid is the top fifth, a little darker, with a gold seam under it.
        var c = tile.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
        int lidTop = (int)(h * 0.80f);
        if (y == lidTop || y == lidTop + 1)
            return Gold;
        if (y > lidTop)
            return Color.Lerp(c, Color.black, 0.18f);
        return c;
    }

    private static Color Average(Texture2D tile)
    {
        var p = tile.GetPixels();
        float r = 0, g = 0, b = 0;
        int n = 0;
        for (int i = 0; i < p.Length; i += 7)
        {
            if (p[i].a < 0.5f)
                continue;
            r += p[i].r;
            g += p[i].g;
            b += p[i].b;
            n++;
        }

        return n == 0 ? Maroon : new Color(r / n, g / n, b / n, 1f);
    }

    private static bool InParallelogram(int x, int y, int x0, int y0, int x1, int y1, int dx, int dy)
    {
        // Top face: bottom edge from (x0,y0) to (x1,y0), rising to the right/back by (dx,dy).
        if (y < y0 || y > y0 + dy)
            return false;
        float shift = (y - y0) / (float)dy * dx;
        return x >= x0 + shift && x <= x1 + shift;
    }

    private static bool InSide(int x, int y, int fx1, int fy0, int fy1, int dx, int dy)
    {
        if (x <= fx1 || x > fx1 + dx)
            return false;
        float lift = (x - fx1) / (float)dx * dy;
        return y >= fy0 + lift && y <= fy1 + lift;
    }

    // ---------------------------------------------------------------- helpers

    private static bool OutsideRounded(int x, int y, int w, int h, int r)
    {
        int cx = x < r ? r : (x >= w - r ? w - 1 - r : x);
        int cy = y < r ? r : (y >= h - r ? h - 1 - r : y);
        int ddx = x - cx, ddy = y - cy;
        return ddx * ddx + ddy * ddy > r * r;
    }

    private static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);

    private static Color[] Outline(Color[] px, int w, int h, Color outline)
    {
        var result = (Color[])px.Clone();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (px[y * w + x].a > 0.5f)
                    continue;
                bool near = false;
                for (int oy = -2; oy <= 2 && !near; oy++)
                {
                    for (int ox = -2; ox <= 2; ox++)
                    {
                        int nx = x + ox, ny = y + oy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            continue;
                        if (px[ny * w + nx].a > 0.5f)
                        {
                            near = true;
                            break;
                        }
                    }
                }

                if (near)
                    result[y * w + x] = outline;
            }
        }

        return result;
    }

    private static Sprite Finish(Color[] px, int w, int h, string name)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(px);
        tex.Apply(false, false);
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        return sprite;
    }
}
