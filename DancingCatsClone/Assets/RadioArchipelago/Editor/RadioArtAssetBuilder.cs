using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace RadioArchipelago.Editor
{
    public static class RadioArtAssetBuilder
    {
        public const string ArtPath = "Assets/RadioArchipelago/Content/RadioArtAssets.asset";

        public static void Run()
        {
            ImportTextResources();
            EnsureFolder("Assets/RadioArchipelago/Art/UI");
            EnsureFolder("Assets/RadioArchipelago/Content");
            RoundRect("Assets/RadioArchipelago/Art/UI/rounded-panel.png", 24);
            RoundRect("Assets/RadioArchipelago/Art/UI/rounded-button.png", 63);
            var art = AssetDatabase.LoadAssetAtPath<RadioArtAssets>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<RadioArtAssets>(); AssetDatabase.CreateAsset(art, ArtPath); }
            art.menuBackground = ImportSprite("Assets/RadioArchipelago/Art/radio-menu-background.png", Vector4.zero, 2048);
            art.brandLogo = ImportSprite("Assets/RadioArchipelago/Art/brand-logo.png", Vector4.zero, 512);
            art.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/RadioArchipelago/Fonts/Manrope-Regular.ttf");
            art.settingsIcon = Icon("settings"); art.mapIcon = Icon("map"); art.workshopIcon = Icon("wrench");
            art.playIcon = Icon("audio-lines"); art.previousIcon = Icon("chevron-left"); art.nextIcon = Icon("chevron-right");
            art.closeIcon = Icon("x"); art.pauseIcon = Icon("pause"); art.soundIcon = Icon("volume-2"); art.radioIcon = Icon("radio");
            art.roundedPanel = ImportSprite("Assets/RadioArchipelago/Art/UI/rounded-panel.png", new Vector4(25,25,25,25), 128);
            art.roundedButton = ImportSprite("Assets/RadioArchipelago/Art/UI/rounded-button.png", new Vector4(63,63,63,63), 128);
            if (art.font == null) throw new InvalidOperationException("The licensed Cyrillic UI font was not imported.");
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log("RADIO_ART PASS: original background/logo, licensed Manrope, licensed Lucide icons and sliced UI sprites");
        }

        public static void ImportTextResources()
        {
            if (TMP_Settings.LoadDefaultSettings() != null) return;
            // Resolve the installed UPM package; its physical directory can be in PackageCache.
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Settings).Assembly);
            string source = package == null ? "" : Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(source)) throw new FileNotFoundException("Installed TextMeshPro essentials package was not found.", source);
            AssetDatabase.ImportPackage(source, false);
            Debug.Log("RADIO_TEXT_RESOURCES: imported the installed Unity TMP essentials without examples");
        }

        private static Sprite Icon(string name) => ImportSprite("Assets/RadioArchipelago/Art/Icons/" + name + ".png", Vector4.zero, 128);

        private static Sprite ImportSprite(string path, Vector4 border, int maxSize)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) throw new FileNotFoundException("Missing interface texture", path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + path);
            return sprite;
        }

        private static void RoundRect(string path, float radius)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float center = size * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + .5f - center) - (center - radius - 1), 0);
                float dy = Mathf.Max(Mathf.Abs(y + .5f - center) - (center - radius - 1), 0);
                float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(.5f - distance) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
