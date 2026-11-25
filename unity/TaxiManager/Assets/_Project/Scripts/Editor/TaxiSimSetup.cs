using System;
using System.Collections.Generic;
using System.Linq;
using TaxiSim.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TaxiSim.EditorTools
{
    /// <summary>
    /// One-click setup: menu  Taxi Manager → Setup BKC 3D World (Ultra).
    ///  1. Linear colour space, highest quality level
    ///  2. URP asset tuned for "ultra" (HDR, 4K soft shadows with 4 cascades, MSAA 4x, depth/opaque
    ///     textures for water, per-pixel additional lights, HDR colour grading)
    ///  3. Adds Screen Space Ambient Occlusion to the renderer if it is missing
    ///  4. Creates Assets/_Project/Scenes/BKC_World.unity with the world bootstrap and opens it
    /// </summary>
    public static class TaxiSimSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/BKC_World.unity";
        private const string ShaderDir = "Assets/_Project/Shaders/";

        [MenuItem("Taxi Manager/Setup BKC 3D World (Ultra)", priority = 0)]
        public static void SetupAll()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EnableSerialPorts();

            string[] levels = QualitySettings.names;
            if (levels.Length > 0) QualitySettings.SetQualityLevel(levels.Length - 1, true);

            UniversalRenderPipelineAsset urp = EnsureUrpAsset();
            if (urp == null)
            {
                EditorUtility.DisplayDialog("Taxi Manager", "Could not find or create a URP asset. Install 'Universal RP' from the Package Manager (or create the project from the 'Universal 3D' template) and run this again.", "OK");
                return;
            }

            foreach (UniversalRenderPipelineAsset asset in AllUrpAssets()) ConfigureUrp(asset);

            CreateScene();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Taxi Manager",
                "BKC 3D world is ready.\n\nScene: " + ScenePath + "\n\nPress Play, then START DISPATCH SHIFT (or press ACCEPT on the console). " +
                "The USB console is found automatically. Press I in free-roam for the city controls.", "Let's go");
        }

        /// <summary>
        /// The ESP32 console link uses System.IO.Ports, which only exists with the ".NET Framework" API level
        /// (value 3 = NET_Unity_4_8, formerly NET_4_6). Unity recompiles the scripts after this change.
        /// </summary>
        private static void EnableSerialPorts()
        {
            const ApiCompatibilityLevel netFramework = (ApiCompatibilityLevel)3;
#if UNITY_2021_2_OR_NEWER
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            if (PlayerSettings.GetApiCompatibilityLevel(target) != netFramework)
            {
                PlayerSettings.SetApiCompatibilityLevel(target, netFramework);
                Debug.Log("[TaxiSim] Api Compatibility Level set to .NET Framework (needed for the USB dispatch console).");
            }
#else
            if (PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone) != netFramework)
                PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, netFramework);
#endif
        }

        [MenuItem("Taxi Manager/Open BKC World Scene", priority = 1)]
        public static void OpenScene()
        {
            if (System.IO.File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else SetupAll();
        }

        // ------------------------------------------------------------------ URP

        private static UniversalRenderPipelineAsset EnsureUrpAsset()
        {
            var current = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (current != null) return current;

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            System.IO.Directory.CreateDirectory("Assets/_Project/Settings");
            AssetDatabase.CreateAsset(rendererData, "Assets/_Project/Settings/TaxiSim_Renderer.asset");
            UniversalRenderPipelineAsset asset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(asset, "Assets/_Project/Settings/TaxiSim_URP.asset");
            GraphicsSettings.defaultRenderPipeline = asset;
            QualitySettings.renderPipeline = asset;
            Debug.Log("[TaxiSim] Created a new URP asset at Assets/_Project/Settings.");
            return asset;
        }

        private static IEnumerable<UniversalRenderPipelineAsset> AllUrpAssets()
        {
            var set = new HashSet<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset d) set.Add(d);
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset q) set.Add(q);
            return set;
        }

        private static void ConfigureUrp(UniversalRenderPipelineAsset urp)
        {
            var so = new SerializedObject(urp);
            SetBool(so, "m_RequireDepthTexture", true);
            SetBool(so, "m_RequireOpaqueTexture", true);
            SetBool(so, "m_SupportsHDR", true);
            SetInt(so, "m_MSAA", 4);
            SetFloat(so, "m_RenderScale", 1f);
            SetFloat(so, "m_ShadowDistance", 600f);
            SetInt(so, "m_ShadowCascadeCount", 4);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_SoftShadowQuality", 3);
            SetInt(so, "m_AdditionalLightsRenderingMode", 1);
            SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
            SetInt(so, "m_ColorGradingMode", 1);
            SetInt(so, "m_ColorGradingLutSize", 64);
            SetInt(so, "m_HDRColorBufferPrecision", 1);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urp);

            FieldInfoRendererList(urp, data => EnsureSsao(data));
        }

        private static void FieldInfoRendererList(UniversalRenderPipelineAsset urp, Action<ScriptableRendererData> action)
        {
            var so = new SerializedObject(urp);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list == null) return;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData data) action(data);
        }

        private static void EnsureSsao(ScriptableRendererData data)
        {
            if (data.rendererFeatures.Any(f => f != null && f.GetType().Name.Contains("AmbientOcclusion"))) return;
            Type ssaoType = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            if (ssaoType == null)
            {
                Debug.LogWarning("[TaxiSim] SSAO type not found. Add it manually: select your Renderer asset → Add Renderer Feature → Screen Space Ambient Occlusion.");
                return;
            }
            try
            {
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
                feature.name = "ScreenSpaceAmbientOcclusion";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                var so = new SerializedObject(data);
                SerializedProperty features = so.FindProperty("m_RendererFeatures");
                SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                if (map != null)
                {
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
                Debug.Log($"[TaxiSim] Added SSAO to renderer '{data.name}'.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TaxiSim] Could not add SSAO automatically ({e.Message}). Add it manually: Renderer asset → Add Renderer Feature → Screen Space Ambient Occlusion.");
            }
        }

        private static void SetBool(SerializedObject so, string name, bool v) { SerializedProperty p = so.FindProperty(name); if (p != null) p.boolValue = v; else Missing(name); }
        private static void SetInt(SerializedObject so, string name, int v)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) { Missing(name); return; }
            if (p.propertyType == SerializedPropertyType.Enum) p.intValue = v;
            else p.intValue = v;
        }
        private static void SetFloat(SerializedObject so, string name, float v) { SerializedProperty p = so.FindProperty(name); if (p != null) p.floatValue = v; else Missing(name); }
        private static void Missing(string name) => Debug.Log($"[TaxiSim] URP setting '{name}' not present in this URP version (skipped).");

        // ------------------------------------------------------------------ scene

        private static void CreateScene()
        {
            System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("BKC World (generated on Play)");
            var boot = go.AddComponent<BkcWorldBootstrap>();
            boot.Shaders = new ShaderSet
            {
                tower = LoadShader("CityTower"),
                road = LoadShader("Road"),
                surface = LoadShader("Surface"),
                pbr = LoadShader("PBR"),
                water = LoadShader("Water"),
                flowLine = LoadShader("FlowLine"),
                holo = LoadShader("Holo"),
                particle = LoadShader("Particle"),
                sky = LoadShader("Sky"),
                holoAd = LoadShader("HoloAd"),
                volumeBeam = LoadShader("VolumeBeam"),
                foliage = LoadShader("Foliage"),
                billboard = LoadShader("Billboard"),
                character = LoadShader("Character"),
                hubFloor = LoadShader("FleetHubFloor")
            };

            // A preview camera so the scene view is not empty before Play (disabled automatically on Play).
            var preview = new GameObject("Editor Preview Camera");
            var cam = preview.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(new Vector3(250f, 90f, 0f), Quaternion.Euler(35f, -35f, 0f));
            cam.farClipPlane = 3000f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("[TaxiSim] Created " + ScenePath);
        }

        private static Shader LoadShader(string file)
        {
            var s = AssetDatabase.LoadAssetAtPath<Shader>(ShaderDir + file + ".shader");
            if (s == null) Debug.LogWarning($"[TaxiSim] Shader {file}.shader not found in {ShaderDir}.");
            return s;
        }
    }
}
