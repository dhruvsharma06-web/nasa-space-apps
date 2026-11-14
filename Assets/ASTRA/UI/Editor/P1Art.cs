using System.IO;
using UnityEditor;
using UnityEngine;

namespace Astra.UI.Editor
{
    /// <summary>
    /// Procedurally generated P1 art: a rounded 9-slice panel sprite, its outline variant and a fictional starfield.
    /// Files are written once (kept with their .meta), so regeneration never changes GUIDs.
    /// </summary>
    public static class P1Art
    {
        public const string RoundedPath = "Assets/ASTRA/UI/Sprites/Rounded.png";
        public const string OutlinePath = "Assets/ASTRA/UI/Sprites/RoundedOutline.png";
        public const string StarfieldPath = "Assets/ASTRA/XR/Materials/Starfield.png";
        private const int SpriteSize = 96;
        private const int Radius = 28;
        private const int Border = 32;

        public static Sprite Rounded => AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
        public static Sprite Outline => AssetDatabase.LoadAssetAtPath<Sprite>(OutlinePath);
        public static Texture2D Starfield => AssetDatabase.LoadAssetAtPath<Texture2D>(StarfieldPath);

        public static void EnsureArt()
        {
            WriteSprite(RoundedPath, 0f);
            WriteSprite(OutlinePath, 5f);
            WriteStarfield();
        }

        private static void WriteSprite(string path, float stroke)
        {
            if (!File.Exists(path))
            {
                var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[SpriteSize * SpriteSize];
                for (var y = 0; y < SpriteSize; y++)
                for (var x = 0; x < SpriteSize; x++)
                {
                    var outside = RoundedDistance(x + 0.5f, y + 0.5f);
                    var alpha = Mathf.Clamp01(0.5f - outside);
                    if (stroke > 0f)
                        alpha *= Mathf.Clamp01(outside + stroke + 0.5f);
                    pixels[y * SpriteSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
                texture.SetPixels32(pixels);
                Write(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == new Vector4(Border, Border, Border, Border))
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(Border, Border, Border, Border);
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        /// <summary>Signed distance (pixels) to the rounded square's edge; negative inside.</summary>
        private static float RoundedDistance(float x, float y)
        {
            var half = SpriteSize * 0.5f;
            var qx = Mathf.Abs(x - half) - (half - Radius);
            var qy = Mathf.Abs(y - half) - (half - Radius);
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - Radius;
        }

        private static void WriteStarfield()
        {
            if (!File.Exists(StarfieldPath))
            {
                const int width = 2048, height = 1024;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                var pixels = new Color32[width * height];
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    // Faint fictional nebula band so the window is not flat black.
                    var band = Mathf.Exp(-Mathf.Pow((y - height * (0.55f + 0.12f * Mathf.Sin(x * 0.0031f))) / (height * 0.16f), 2f));
                    var noise = Mathf.PerlinNoise(x * 0.004f, y * 0.006f);
                    var glow = band * noise * 0.22f;
                    pixels[y * width + x] = new Color(0.012f + glow * 0.35f, 0.02f + glow * 0.45f, 0.05f + glow, 1f);
                }
                var random = new System.Random(20261114);
                for (var i = 0; i < 2600; i++)
                {
                    var x = random.Next(width);
                    var y = random.Next(height);
                    var bright = (float)System.Math.Pow(random.NextDouble(), 3.0);
                    var tint = random.NextDouble();
                    var star = new Color(0.75f + 0.25f * (float)tint, 0.82f + 0.1f * bright, 1f, 1f) * (0.35f + 0.65f * bright);
                    var size = bright > 0.7f ? 1 : 0;
                    for (var dy = -size; dy <= size; dy++)
                    for (var dx = -size; dx <= size; dx++)
                    {
                        var px = (x + dx + width) % width;
                        var py = Mathf.Clamp(y + dy, 0, height - 1);
                        var falloff = dx == 0 && dy == 0 ? 1f : 0.35f;
                        Color current = pixels[py * width + px];
                        pixels[py * width + px] = Color.Lerp(current, star, falloff);
                    }
                }
                texture.SetPixels32(pixels);
                Write(StarfieldPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(StarfieldPath);
            if (importer.wrapMode == TextureWrapMode.Clamp && importer.mipmapEnabled)
                return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void Write(string path, byte[] png)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
