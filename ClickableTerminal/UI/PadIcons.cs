using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;
using LethalCompanyInputUtils.Glyphs;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace ClickableTerminal
{
    // CorrectControllerIcon, if present, makes InputUtils hand out the pictures of the controller picked there.
    internal static class PadIcons
    {
        internal const int South = 0;
        internal const int West = 1;
        internal const int North = 2;
        internal const int East = 3;
        internal const int Select = 4;

        private static readonly string[] Controls = { "buttonSouth", "buttonWest", "buttonNorth", "buttonEast", "select" };
        private static readonly bool HasInputUtils = Chainloader.PluginInfos.ContainsKey("com.rune580.LethalCompanyInputUtils");

        private static readonly Sprite[] Sprites = new Sprite[Controls.Length];
        private static object builtFor;
        private static TMP_SpriteAsset sheet;
        private static bool broken;

        internal static Sprite Get(int button)
        {
            Refresh();
            return Sprites[button];
        }

        internal static TMP_SpriteAsset Sheet
        {
            get
            {
                Refresh();
                return sheet;
            }
        }

        internal static string Markup(int button)
        {
            return Sheet != null && Sprites[button] != null ? $"<sprite name=\"pad{button}\" tint=1>" : null;
        }

        private static void Refresh()
        {
            if (!HasInputUtils || broken)
            {
                return;
            }

            try
            {
                FromInputUtils();
            }
            catch (Exception e)
            {
                broken = true;
                sheet = null;
                Array.Clear(Sprites, 0, Sprites.Length);
                Plugin.mlg.LogWarning($"Controller icons from InputUtils could not be used, button names are written instead: {e}");
            }
        }

        // A method of its own, so InputUtils is only loaded when it is there.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FromInputUtils()
        {
            var glyphs = ControllerGlyph.GetBestMatching();
            if (ReferenceEquals(glyphs, builtFor))
            {
                return;
            }

            builtFor = glyphs;
            for (var i = 0; i < Controls.Length; i++)
            {
                Sprites[i] = glyphs != null ? glyphs["<Gamepad>/" + Controls[i]] : null;
            }

            sheet = Build();
        }

        private static TMP_SpriteAsset Build()
        {
            var cell = 0;
            var count = 0;
            foreach (var sprite in Sprites)
            {
                if (sprite != null)
                {
                    count++;
                    cell = Mathf.Max(cell, Mathf.CeilToInt(sprite.textureRect.width), Mathf.CeilToInt(sprite.textureRect.height));
                }
            }

            if (count == 0)
            {
                return null;
            }

            const int gap = 2;
            var step = cell + gap;
            var atlas = new Texture2D(Sprites.Length * step, step, TextureFormat.RGBA32, false) { name = "ClickableTerminal Pad Icons" };
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = atlas.name;
            asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(asset.name);

            // Without a version TextMeshPro takes the asset for an old one and tries to convert it.
            AccessTools.Field(typeof(TMP_SpriteAsset), "m_Version")?.SetValue(asset, "1.1.0");

            var copies = new Dictionary<Texture, Texture2D>();
            for (var i = 0; i < Sprites.Length; i++)
            {
                var sprite = Sprites[i];
                if (sprite == null)
                {
                    continue;
                }

                var rect = sprite.textureRect;
                var width = Mathf.RoundToInt(rect.width);
                var height = Mathf.RoundToInt(rect.height);
                var x = i * step;

                if (!copies.TryGetValue(sprite.texture, out var copy))
                {
                    copies[sprite.texture] = copy = Readable(sprite.texture);
                }

                atlas.SetPixels(x, 0, width, height, copy.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height));

                // White, so the text's own colour tints it.
                var glyph = new TMP_SpriteGlyph
                {
                    index = (uint)asset.spriteGlyphTable.Count,
                    metrics = new GlyphMetrics(width, height, 0f, height * 0.8f, width),
                    glyphRect = new GlyphRect(x, 0, width, height),
                    scale = 1f,
                    atlasIndex = 0
                };
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = "pad" + i, scale = 1.1f });
            }

            atlas.Apply(false, true);
            foreach (var copy in copies.Values)
            {
                UnityEngine.Object.Destroy(copy);
            }

            var shader = Shader.Find("TextMeshPro/Sprite");
            if (shader == null && TMP_Settings.defaultSpriteAsset != null && TMP_Settings.defaultSpriteAsset.material != null)
            {
                shader = TMP_Settings.defaultSpriteAsset.material.shader;
            }

            asset.spriteSheet = atlas;
            asset.material = new Material(shader != null ? shader : Shader.Find("UI/Default")) { name = asset.name, mainTexture = atlas, hideFlags = HideFlags.HideAndDontSave };
            asset.hideFlags = HideFlags.HideAndDontSave;
            atlas.hideFlags = HideFlags.HideAndDontSave;
            asset.UpdateLookupTables();
            return asset;
        }

        // Textures from an asset bundle cannot be read directly, a copy drawn through the graphics card can.
        private static Texture2D Readable(Texture source)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var before = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = before;
            RenderTexture.ReleaseTemporary(target);
            return copy;
        }
    }
}
