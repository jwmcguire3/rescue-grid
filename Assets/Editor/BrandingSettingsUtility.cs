#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Rescue.Unity.EditorTools
{
    public static class BrandingSettingsUtility
    {
        private const string SplashPath = "Assets/Rescue.Unity/Art/Splash/RescueGrid_Startup.png";
        private const string IconPath = "Assets/Rescue.Unity/Art/AppIcon/RescueGrid_AppIcon.png";

        [MenuItem("Rescue Grid/Branding/Apply Startup Branding")]
        public static void ApplyStartupBranding()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            ConfigureSplashImporter();

            AssetDatabase.ImportAsset(SplashPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Sprite splash = LoadRequiredAsset<Sprite>(SplashPath);
            Texture2D icon = LoadRequiredAsset<Texture2D>(IconPath);

            ApplySplashSettings(splash);
            ApplyAndroidOrientationSettings();
            ApplyAndroidIconSettings(icon);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("Rescue Grid startup branding applied.");
        }

        public static void ApplyStartupBrandingBatch()
        {
            try
            {
                ApplyStartupBranding();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateStartupBrandingBatch()
        {
            try
            {
                ValidateStartupBranding();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateStartupBranding()
        {
            Sprite splash = LoadRequiredAsset<Sprite>(SplashPath);
            Texture2D icon = LoadRequiredAsset<Texture2D>(IconPath);

            Require(PlayerSettings.SplashScreen.background == splash, "Landscape splash background is not RescueGrid_Startup.");
            Require(PlayerSettings.SplashScreen.backgroundPortrait == splash, "Portrait splash background is not RescueGrid_Startup.");
            Require(PlayerSettings.SplashScreen.show, "Unity splash screen container is disabled, so the Rescue Grid startup image will not display.");
            Require(!PlayerSettings.SplashScreen.showUnityLogo, "Unity logo is still enabled on the splash screen.");
            Require(!PlayerSettings.SplashScreen.blurBackgroundImage, "Splash background blur is still enabled.");
            Require(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait, "Default Orientation is not Portrait, so Android launch/splash can disagree with gameplay.");
            Require(PlayerSettings.allowedAutorotateToPortrait, "Portrait is not enabled in Allowed Orientations.");
            Require(!PlayerSettings.allowedAutorotateToPortraitUpsideDown, "Portrait Upside Down is enabled; this can launch the Android splash upside down.");
            Require(!PlayerSettings.allowedAutorotateToLandscapeLeft, "Landscape Left is enabled; gameplay currently forces portrait.");
            Require(!PlayerSettings.allowedAutorotateToLandscapeRight, "Landscape Right is enabled; gameplay currently forces portrait.");

            ValidateAndroidIconKind(AndroidPlatformIconKind.Adaptive, icon);
#pragma warning disable CS0618
            ValidateAndroidIconKind(AndroidPlatformIconKind.Round, icon);
            ValidateAndroidIconKind(AndroidPlatformIconKind.Legacy, icon);
#pragma warning restore CS0618

            Debug.Log("Rescue Grid startup branding validation passed.");
        }

        private static void ConfigureSplashImporter()
        {
            TextureImporter importer = GetRequiredTextureImporter(SplashPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ApplySplashSettings(Sprite splash)
        {
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.background = splash;
            PlayerSettings.SplashScreen.backgroundPortrait = splash;
            PlayerSettings.SplashScreen.blurBackgroundImage = false;
            PlayerSettings.SplashScreen.logos = Array.Empty<PlayerSettings.SplashScreenLogo>();
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
        }

        private static void ApplyAndroidOrientationSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        private static void ApplyAndroidIconSettings(Texture2D icon)
        {
            ApplyAndroidIconKind(AndroidPlatformIconKind.Adaptive, icon);
#pragma warning disable CS0618
            ApplyAndroidIconKind(AndroidPlatformIconKind.Round, icon);
            ApplyAndroidIconKind(AndroidPlatformIconKind.Legacy, icon);
#pragma warning restore CS0618
        }

        private static void ApplyAndroidIconKind(PlatformIconKind kind, Texture2D icon)
        {
            PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            for (int i = 0; i < slots.Length; i++)
            {
                Texture2D[] layers = new Texture2D[slots[i].maxLayerCount];
                for (int layer = 0; layer < layers.Length; layer++)
                {
                    layers[layer] = icon;
                }

                slots[i].SetTextures(layers);
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
        }

        private static void ValidateAndroidIconKind(PlatformIconKind kind, Texture2D icon)
        {
            PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            Require(slots.Length > 0, $"No Android icon slots found for {kind}.");

            foreach (PlatformIcon slot in slots)
            {
                Texture2D[] textures = slot.GetTextures();
                Require(textures.Length == slot.maxLayerCount, $"{kind} {slot.width}x{slot.height} has an unexpected layer count.");

                for (int layer = 0; layer < textures.Length; layer++)
                {
                    Require(textures[layer] == icon, $"{kind} {slot.width}x{slot.height} layer {layer} is not RescueGrid_AppIcon.");
                }
            }
        }

        private static TextureImporter GetRequiredTextureImporter(string path)
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer is TextureImporter textureImporter)
            {
                return textureImporter;
            }

            throw new InvalidOperationException($"Expected a texture importer at '{path}'.");
        }

        private static T LoadRequiredAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            throw new InvalidOperationException($"Could not load {typeof(T).Name} at '{path}'.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
#endif
