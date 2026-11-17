#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.ARFoundation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Proje Unity'de açıldığında veya güncellendiğinde üstteki panelleri kaldırıp
/// tüm özellikleri alttan açılan Turuncu Hamburger Menü (BottomMenuDrawer) yapısına otomatik geçirir.
/// </summary>
[InitializeOnLoad]
public static class FurnishARAutoInitializer
{
    static FurnishARAutoInitializer()
    {
        EditorApplication.delayCall += TryAutoBuildOrUpdate;
    }

    private static void TryAutoBuildOrUpdate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoBuildOrUpdate;
            return;
        }

        if (!File.Exists("Assets/Scenes/FurnishARScene.unity") || !File.Exists("Assets/Prefabs/ModernSofa.prefab"))
        {
            FurnishARProjectSetup.BuildCompleteProject();
            return;
        }

        // Eğer sahnede hâlâ eski üst panel (TopHeaderPanel) varsa veya yeni BottomMenuDrawer yoksa UI'ı otomatik güncelle
        if (GameObject.Find("TopHeaderPanel") != null || GameObject.Find("BottomMenuDrawer") == null)
        {
            FurnishARProjectSetup.UpgradeSceneToBottomHamburgerUI();
        }

        FurnishARProjectSetup.EnsureAppNameAndIconConfigured();
    }
}

public class FurnishARAppBrandingWindow : EditorWindow
{
    private string appName = "Turuncu AR Mobilya";
    private string companyName = "TuruncuAR Studio";
    private Texture2D appIcon;

    [MenuItem("Furnish AR/Uygulama İsmi ve İkonunu Değiştir...")]
    public static void ShowWindow()
    {
        var win = GetWindow<FurnishARAppBrandingWindow>("Uygulama İsim & İkon");
        win.minSize = new Vector2(400f, 220f);
        win.appName = PlayerSettings.productName;
        win.companyName = PlayerSettings.companyName;
        win.appIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Sprites/AppIcon.png");
        win.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(12f);
        EditorGUILayout.LabelField("Uygulama Kimliği ve İkon Ayarları", EditorStyles.boldLabel);
        GUILayout.Space(8f);

        appName = EditorGUILayout.TextField("Uygulama İsmi (Telefonda):", appName);
        companyName = EditorGUILayout.TextField("Geliştirici / Şirket İsmi:", companyName);
        appIcon = (Texture2D)EditorGUILayout.ObjectField("Uygulama İkonu (PNG):", appIcon, typeof(Texture2D), false);

        GUILayout.Space(16f);
        GUI.backgroundColor = new Color(1.0f, 0.50f, 0.10f, 1f);
        if (GUILayout.Button("İsmi ve İkonu Uygula & Kaydet", GUILayout.Height(38f)))
        {
            FurnishARProjectSetup.ApplyCustomBranding(appName, companyName, appIcon);
            EditorUtility.DisplayDialog("Başarılı", $"Uygulama ismi '{appName}' ve uygulama ikonu başarıyla güncellendi!", "Tamam");
        }
        GUI.backgroundColor = Color.white;
    }
}

public static class FurnishARProjectSetup
{
    private static readonly string[] ModelKeys = new string[]
    {
        "ModernSofa",
        "CozyArmchair",
        "CoffeeTable",
        "FloorLamp",
        "ModernBookshelf"
    };

    private static readonly string[] DisplayNames = new string[]
    {
        "Modern Kanepe",
        "Tekli Berjer",
        "Orta Sehpa",
        "Lambader",
        "Kitaplık"
    };

    [MenuItem("Furnish AR/Telefon Bağlamadan Doğrudan APK Oluştur (Builds Klasörüne)")]
    public static void BuildApkWithoutDevice()
    {
        string scenePath = "Assets/Scenes/FurnishARScene.unity";
        if (!File.Exists(scenePath))
        {
            BuildCompleteProject();
        }

        ConfigurePlayerAndBuildSettings(scenePath);

        string buildsDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
        if (!Directory.Exists(buildsDir))
            Directory.CreateDirectory(buildsDir);

        string apkPath = Path.Combine(buildsDir, "TuruncuARMobilya.apk");

        BuildPlayerOptions buildOptions = new BuildPlayerOptions
        {
            scenes = new string[] { scenePath },
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None // Cihaz aramadan sadece APK dosyasını üretir!
        };

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorUtility.RevealInFinder(apkPath);
            EditorUtility.DisplayDialog("APK Hazır!", $"APK dosyası başarıyla oluşturuldu:\n{apkPath}\n\nBu dosyayı arkadaşınıza doğrudan gönderebilirsiniz.", "Tamam");
        }
        else
        {
            Debug.LogError("[FurnishAR] APK oluşturma durumu: " + report.summary.result);
        }
    }

    [MenuItem("Furnish AR/Projeyi ve Alttan Açılan Hamburger Arayüzü Kur")]
    public static void BuildCompleteProject()
    {
        Debug.Log("[FurnishAR] Otomatik proje ve sahne kurulumu başlatılıyor...");

        EnsureDirectories();
        ConfigureSpriteImporters();
        Dictionary<string, Material> matMap = CreateAllMaterials();
        GameObject planePrefab = CreateARDefaultPlanePrefab();
        GameObject[] furniturePrefabs = CreateFurniturePrefabs(matMap);
        string scenePath = CreateFurnishARScene(planePrefab, furniturePrefabs);
        ConfigurePlayerAndBuildSettings(scenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FurnishAR] Kurulum başarıyla tamamlandı! Sahne hazır: " + scenePath);
    }

    [MenuItem("Furnish AR/Arayüzü Alttan Açılan Hamburger Menüye Güncelle")]
    public static void UpgradeSceneToBottomHamburgerUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        ConfigureSpriteImporters();

        FurniturePlacementManager manager = GameObject.FindObjectOfType<FurniturePlacementManager>();
        if (manager == null) return;

        // Mevcut Canvas'ı silip yeni alttan açılan Hamburger Canvas'ı kur
        Canvas oldCanvas = GameObject.FindObjectOfType<Canvas>();
        if (oldCanvas != null)
        {
            GameObject.DestroyImmediate(oldCanvas.gameObject);
        }

        GameObject[] furniturePrefabs = manager.furnitureCatalog;
        if (furniturePrefabs == null || furniturePrefabs.Length == 0)
        {
            List<GameObject> loaded = new List<GameObject>();
            foreach (string k in ModelKeys)
            {
                GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{k}.prefab");
                if (p != null) loaded.Add(p);
            }
            furniturePrefabs = loaded.ToArray();
            manager.furnitureCatalog = furniturePrefabs;
        }

        BuildOrangeThemeUI(manager, furniturePrefabs);

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[FurnishAR] Üstteki paneller kaldırıldı; tüm özellikler alttan açılan Turuncu Hamburger Menüye taşındı!");
    }

    private static void EnsureDirectories()
    {
        string[] dirs = new string[]
        {
            "Assets/Materials",
            "Assets/Materials/Furnitures",
            "Assets/Prefabs",
            "Assets/Scenes",
            "Assets/Scripts",
            "Assets/UI/Sprites",
            "Assets/Models/Furnitures"
        };

        foreach (string d in dirs)
        {
            if (!Directory.Exists(d))
                Directory.CreateDirectory(d);
        }
        AssetDatabase.Refresh();
    }

    private static void ConfigureSpriteImporters()
    {
        if (!Directory.Exists("Assets/UI/Sprites")) return;
        string[] pngFiles = Directory.GetFiles("Assets/UI/Sprites", "*.png");
        foreach (string file in pngFiles)
        {
            string assetPath = file.Replace("\\", "/");
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                if (assetPath.EndsWith("AppIcon.png"))
                    ti.isReadable = true;
                ti.SaveAndReimport();
            }
        }
    }

    private static Dictionary<string, Material> CreateAllMaterials()
    {
        var specs = new Dictionary<string, (Color col, float metallic, float smoothness, bool emissive)>
        {
            { "Wood_Oak",             (new Color(0.76f, 0.54f, 0.34f), 0.05f, 0.45f, false) },
            { "Wood_Walnut",          (new Color(0.42f, 0.26f, 0.16f), 0.05f, 0.50f, false) },
            { "Fabric_OrangeMain",    (new Color(0.93f, 0.42f, 0.12f), 0.00f, 0.22f, false) },
            { "Fabric_Terracotta",    (new Color(0.78f, 0.30f, 0.14f), 0.00f, 0.20f, false) },
            { "Fabric_Cream",         (new Color(0.96f, 0.93f, 0.88f), 0.00f, 0.25f, false) },
            { "Fabric_DarkWarm",      (new Color(0.24f, 0.19f, 0.17f), 0.05f, 0.30f, false) },
            { "Accent_VibrantOrange", (new Color(1.00f, 0.55f, 0.10f), 0.00f, 0.32f, false) },
            { "Metal_Charcoal",       (new Color(0.18f, 0.18f, 0.20f), 0.75f, 0.65f, false) },
            { "Lamp_WarmOrange",      (new Color(0.98f, 0.58f, 0.20f), 0.00f, 0.35f, true)  },
            { "Lamp_Glow",            (new Color(1.00f, 0.88f, 0.65f), 0.00f, 0.60f, true)  }
        };

        var result = new Dictionary<string, Material>();
        Shader stdShader = Shader.Find("Standard");

        foreach (var kv in specs)
        {
            string matPath = $"Assets/Materials/Furnitures/{kv.Key}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(stdShader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.color = kv.Value.col;
            mat.SetFloat("_Metallic", kv.Value.metallic);
            mat.SetFloat("_Glossiness", kv.Value.smoothness);
            if (kv.Value.emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", kv.Value.col * 0.45f);
            }
            EditorUtility.SetDirty(mat);
            result[kv.Key] = mat;
        }

        string planeMatPath = "Assets/Materials/PlaneMat.mat";
        Material planeMat = AssetDatabase.LoadAssetAtPath<Material>(planeMatPath);
        if (planeMat == null)
        {
            planeMat = new Material(stdShader);
            AssetDatabase.CreateAsset(planeMat, planeMatPath);
        }
        planeMat.SetFloat("_Mode", 3f);
        planeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        planeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        planeMat.SetInt("_ZWrite", 0);
        planeMat.DisableKeyword("_ALPHATEST_ON");
        planeMat.DisableKeyword("_ALPHABLEND_ON");
        planeMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        planeMat.renderQueue = 3000;
        planeMat.color = new Color(1.0f, 0.50f, 0.08f, 0.14f);
        planeMat.SetFloat("_Glossiness", 0.0f);
        EditorUtility.SetDirty(planeMat);

        string lineMatPath = "Assets/Materials/ARPlaneLineMat.mat";
        Shader unlitColor = Shader.Find("Unlit/Color") ?? stdShader;
        Material lineMat = AssetDatabase.LoadAssetAtPath<Material>(lineMatPath);
        if (lineMat == null)
        {
            lineMat = new Material(unlitColor);
            AssetDatabase.CreateAsset(lineMat, lineMatPath);
        }
        lineMat.color = new Color(1.0f, 0.48f, 0.05f, 1.0f);
        EditorUtility.SetDirty(lineMat);

        AssetDatabase.SaveAssets();
        return result;
    }

    private static GameObject CreateARDefaultPlanePrefab()
    {
        string prefabPath = "Assets/Prefabs/AR Default Plane.prefab";
        Material planeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlaneMat.mat");
        Material lineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ARPlaneLineMat.mat");

        GameObject go = new GameObject("AR Default Plane");
        go.AddComponent<ARPlane>();
        go.AddComponent<ARPlaneMeshVisualizer>();
        go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        if (planeMat != null) mr.sharedMaterial = planeMat;

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 0;
        lr.startWidth = 0.015f;
        lr.endWidth = 0.015f;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        if (lineMat != null) lr.sharedMaterial = lineMat;
        lr.startColor = new Color(1.0f, 0.48f, 0.05f, 1f);
        lr.endColor = new Color(1.0f, 0.65f, 0.15f, 1f);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        GameObject.DestroyImmediate(go);
        return savedPrefab;
    }

    private static GameObject[] CreateFurniturePrefabs(Dictionary<string, Material> matMap)
    {
        List<GameObject> prefabs = new List<GameObject>();

        foreach (string key in ModelKeys)
        {
            string objPath = $"Assets/Models/Furnitures/{key}.obj";
            AssetDatabase.ImportAsset(objPath, ImportAssetOptions.ForceUpdate);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);

            if (modelAsset == null)
            {
                Debug.LogError("[FurnishAR] Model bulunamadı: " + objPath);
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = key;

            Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer r in renderers)
            {
                Material[] shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string mName = shared[i] != null ? shared[i].name : "";
                    foreach (var kv in matMap)
                    {
                        if (mName.Contains(kv.Key) || r.gameObject.name.EndsWith(kv.Key))
                        {
                            shared[i] = kv.Value;
                            break;
                        }
                    }
                }
                r.sharedMaterials = shared;

                if (!hasBounds)
                {
                    combinedBounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(r.bounds);
                }
            }

            BoxCollider col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            if (hasBounds)
            {
                col.center = instance.transform.InverseTransformPoint(combinedBounds.center);
                col.size = combinedBounds.size;
            }

            string prefabPath = $"Assets/Prefabs/{key}.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            GameObject.DestroyImmediate(instance);
            prefabs.Add(savedPrefab);
        }

        return prefabs.ToArray();
    }

    private static string CreateFurnishARScene(GameObject planePrefab, GameObject[] furniturePrefabs)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject lightGO = new GameObject("Directional Light");
        Light dirLight = lightGO.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(1.0f, 0.96f, 0.90f, 1.0f);
        dirLight.intensity = 1.15f;
        dirLight.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        GameObject originGO = new GameObject("AR Session Origin");
        ARSessionOrigin sessionOrigin = originGO.AddComponent<ARSessionOrigin>();

        GameObject arCamGO = new GameObject("AR Camera");
        arCamGO.tag = "MainCamera";
        arCamGO.transform.SetParent(originGO.transform, false);

        Camera arCam = arCamGO.AddComponent<Camera>();
        arCam.clearFlags = CameraClearFlags.SolidColor;
        arCam.backgroundColor = Color.black;
        arCam.nearClipPlane = 0.05f;
        arCam.farClipPlane = 30f;

        arCamGO.AddComponent<ARCameraManager>();
        arCamGO.AddComponent<ARCameraBackground>();
        TrackedPoseDriver poseDriver = arCamGO.AddComponent<TrackedPoseDriver>();
        poseDriver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.ColorCamera);

        sessionOrigin.camera = arCam;

        ARRaycastManager raycastManager = originGO.AddComponent<ARRaycastManager>();
        ARPlaneManager planeManager = originGO.AddComponent<ARPlaneManager>();
        planeManager.planePrefab = planePrefab;
        planeManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;

        FurniturePlacementManager placementManager = originGO.AddComponent<FurniturePlacementManager>();
        placementManager.sessionOrigin = sessionOrigin;
        placementManager.raycastManager = raycastManager;
        placementManager.planeManager = planeManager;
        placementManager.furnitureCatalog = furniturePrefabs;
        placementManager.furnitureNames = DisplayNames;
        if (furniturePrefabs != null && furniturePrefabs.Length > 0)
        {
            placementManager.SpawnableFurniture = furniturePrefabs[0];
        }

        GameObject arSessionGO = new GameObject("AR Session");
        arSessionGO.AddComponent<ARSession>();
        arSessionGO.AddComponent<ARInputManager>();

        GameObject eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<StandaloneInputModule>();

        BuildOrangeThemeUI(placementManager, furniturePrefabs);

        string scenePath = "Assets/Scenes/FurnishARScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        return scenePath;
    }

    /// <summary>
    /// Üstte hiçbir panel olmadan, alttaki Hamburger Butonu ile alttan yukarı açılan
    /// Birleşik Turuncu Menü Çekmecesi (BottomMenuDrawer) arayüzünü kurar.
    /// </summary>
    private static void BuildOrangeThemeUI(FurniturePlacementManager manager, GameObject[] furniturePrefabs)
    {
        Sprite roundedCard = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedCard.png");
        Sprite roundedBtn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/RoundedButton.png");
        Sprite borderGlow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/CardBorderGlow.png");
        Sprite hamburgerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/HamburgerIcon.png");
        Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        Color darkPanelBg     = new Color(0.12f, 0.06f, 0.03f, 0.95f);
        Color primaryOrange   = new Color(1.00f, 0.45f, 0.06f, 1.00f);
        Color warmAmber       = new Color(1.00f, 0.62f, 0.18f, 1.00f);
        Color buttonOrangeBg  = new Color(0.86f, 0.36f, 0.06f, 0.96f);
        Color buttonSecondary = new Color(0.26f, 0.14f, 0.08f, 0.96f);
        Color creamText       = new Color(1.00f, 0.96f, 0.90f, 1.00f);
        Color softOrangeText  = new Color(1.00f, 0.78f, 0.55f, 1.00f);

        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // =====================================================================
        // 1) ALTTAN AÇILAN BİRLEŞİK MENÜ ÇEKMECESİ (BottomMenuDrawer)
        // (Tüm döndürme, boyutlandırma, yüzey, geri al, temizle ve mobilya seçimi burada!)
        // =====================================================================
        GameObject drawerGO = CreateUIObject("BottomMenuDrawer", canvasGO.transform);
        RectTransform drawerRT = drawerGO.GetComponent<RectTransform>();
        drawerRT.anchorMin = new Vector2(0f, 0f);
        drawerRT.anchorMax = new Vector2(1f, 0f);
        drawerRT.pivot = new Vector2(0.5f, 0f);
        drawerRT.anchoredPosition = new Vector2(0f, 128f);
        drawerRT.sizeDelta = new Vector2(-32f, 610f);

        Image drawerBg = drawerGO.AddComponent<Image>();
        drawerBg.sprite = roundedCard;
        drawerBg.color = darkPanelBg;
        manager.bottomMenuDrawer = drawerRT;

        // Çekmece Üst Turuncu Tutamaç Çizgisi
        GameObject handleLine = CreateUIObject("DrawerHandleBar", drawerGO.transform);
        RectTransform handleRT = handleLine.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0.38f, 1f);
        handleRT.anchorMax = new Vector2(0.62f, 1f);
        handleRT.pivot = new Vector2(0.5f, 1f);
        handleRT.anchoredPosition = new Vector2(0f, -10f);
        handleRT.sizeDelta = new Vector2(0f, 8f);
        Image handleImg = handleLine.AddComponent<Image>();
        handleImg.color = primaryOrange;

        // --- A) Çekmece İçi Bilgi & Durum Başlığı (En Üstte Değil, Alttaki Çekmecenin İçinde!) ---
        Text selectedTxt = CreateText("SelectedItemText", drawerGO.transform, defaultFont,
            "Seçili Eşya: Modern Kanepe", 25, FontStyle.Bold, warmAmber, TextAnchor.MiddleLeft);
        RectTransform selRT = selectedTxt.GetComponent<RectTransform>();
        selRT.anchorMin = new Vector2(0f, 1f);
        selRT.anchorMax = new Vector2(0.75f, 1f);
        selRT.pivot = new Vector2(0f, 1f);
        selRT.anchoredPosition = new Vector2(28f, -24f);
        selRT.sizeDelta = new Vector2(0f, 36f);
        manager.selectedItemText = selectedTxt;

        // Çekmeceyi İndir / Kapat Butonu (Sağ Üst Köşe - Çekmece İçinde)
        CreateActionButton("Btn_CollapseDrawer", drawerGO.transform, defaultFont, roundedBtn,
            "Kapat v", new Vector2(0.78f, 0.89f), new Vector2(0.97f, 0.96f),
            buttonSecondary, softOrangeText, manager.CloseBottomMenuDrawer);

        // Durum Mesajı Şeridi (Çekmece İçinde)
        GameObject statusBox = CreateUIObject("StatusBanner", drawerGO.transform);
        RectTransform statusBoxRT = statusBox.GetComponent<RectTransform>();
        statusBoxRT.anchorMin = new Vector2(0.025f, 0.78f);
        statusBoxRT.anchorMax = new Vector2(0.975f, 0.875f);
        statusBoxRT.offsetMin = Vector2.zero;
        statusBoxRT.offsetMax = Vector2.zero;
        Image statusBoxBg = statusBox.AddComponent<Image>();
        statusBoxBg.sprite = roundedBtn;
        statusBoxBg.color = new Color(0.20f, 0.10f, 0.05f, 0.95f);

        Text statusTxt = CreateText("StatusText", statusBox.transform, defaultFont,
            "Eşya seçip zemine dokunun. Parmağınızla taşıyabilir veya döndürebilirsiniz.",
            21, FontStyle.Normal, creamText, TextAnchor.MiddleCenter);
        StretchFull(statusTxt.GetComponent<RectTransform>(), 14f, 4f);
        manager.statusText = statusTxt;

        // --- B) Özellik ve Araç Butonları (Eskiden Üstte Olan Özellikler Artık Alt Çekmecede!) ---
        GameObject controlsContainer = CreateUIObject("DrawerControlsSection", drawerGO.transform);
        RectTransform ctrlRT = controlsContainer.GetComponent<RectTransform>();
        ctrlRT.anchorMin = new Vector2(0.025f, 0.51f);
        ctrlRT.anchorMax = new Vector2(0.975f, 0.76f);
        ctrlRT.offsetMin = Vector2.zero;
        ctrlRT.offsetMax = Vector2.zero;

        // 1. Satır: Sol Döndür, Sağ Döndür, Küçült, Büyüt
        CreateActionButton("Btn_RotateLeft", controlsContainer.transform, defaultFont, roundedBtn,
            "< Sol Döndür", new Vector2(0.00f, 0.53f), new Vector2(0.24f, 1.00f), buttonOrangeBg, creamText,
            manager.RotateSelectedLeft);

        CreateActionButton("Btn_RotateRight", controlsContainer.transform, defaultFont, roundedBtn,
            "Sağ Döndür >", new Vector2(0.253f, 0.53f), new Vector2(0.493f, 1.00f), buttonOrangeBg, creamText,
            manager.RotateSelectedRight);

        CreateActionButton("Btn_ScaleDown", controlsContainer.transform, defaultFont, roundedBtn,
            "- Küçült", new Vector2(0.506f, 0.53f), new Vector2(0.746f, 1.00f), buttonSecondary, softOrangeText,
            manager.ScaleSelectedDown);

        CreateActionButton("Btn_ScaleUp", controlsContainer.transform, defaultFont, roundedBtn,
            "+ Büyüt", new Vector2(0.76f, 0.53f), new Vector2(1.00f, 1.00f), buttonSecondary, softOrangeText,
            manager.ScaleSelectedUp);

        // 2. Satır: Yüzey Aç/Gizle, Geri Al, Temizle
        Button planeBtn = CreateActionButton("Btn_TogglePlanes", controlsContainer.transform, defaultFont, roundedBtn,
            "Yüzey: Açık", new Vector2(0.00f, 0.00f), new Vector2(0.32f, 0.46f), buttonSecondary, warmAmber,
            manager.TogglePlanesVisibility);
        manager.planeToggleButtonText = planeBtn.GetComponentInChildren<Text>();

        CreateActionButton("Btn_Undo", controlsContainer.transform, defaultFont, roundedBtn,
            "Geri Al", new Vector2(0.34f, 0.00f), new Vector2(0.66f, 0.46f), buttonSecondary, creamText,
            manager.UndoLastFurniture);

        CreateActionButton("Btn_ClearAll", controlsContainer.transform, defaultFont, roundedBtn,
            "Temizle", new Vector2(0.68f, 0.00f), new Vector2(1.00f, 0.46f), new Color(0.68f, 0.20f, 0.08f, 0.95f), creamText,
            manager.ClearAllFurniture);

        // --- C) Mobilya Seçim Kataloğu (Alt Çekmecenin Alt Yarısı) ---
        Text dockTitle = CreateText("CatalogTitle", drawerGO.transform, defaultFont,
            "MOBİLYA SEÇİMİ", 21, FontStyle.Bold, softOrangeText, TextAnchor.MiddleLeft);
        RectTransform dockTitleRT = dockTitle.GetComponent<RectTransform>();
        dockTitleRT.anchorMin = new Vector2(0.03f, 0.43f);
        dockTitleRT.anchorMax = new Vector2(0.97f, 0.49f);
        dockTitleRT.offsetMin = Vector2.zero;
        dockTitleRT.offsetMax = Vector2.zero;

        int count = Mathf.Min(ModelKeys.Length, furniturePrefabs.Length);
        Image[] selectionBorders = new Image[count];

        float marginX = 0.025f;
        float spacingX = 0.015f;
        float cardWidth = (1.0f - 2f * marginX - (count - 1) * spacingX) / count;

        for (int i = 0; i < count; i++)
        {
            string key = ModelKeys[i];
            string label = DisplayNames[i];
            GameObject prefab = furniturePrefabs[i];
            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/UI/Sprites/{key}_Icon.png");

            float xMin = marginX + i * (cardWidth + spacingX);
            float xMax = xMin + cardWidth;

            GameObject cardGO = CreateUIObject($"Card_{key}", drawerGO.transform);
            RectTransform cardRT = cardGO.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(xMin, 0.03f);
            cardRT.anchorMax = new Vector2(xMax, 0.42f);
            cardRT.offsetMin = Vector2.zero;
            cardRT.offsetMax = Vector2.zero;

            Image borderImg = cardGO.AddComponent<Image>();
            borderImg.sprite = borderGlow != null ? borderGlow : roundedCard;
            borderImg.color = (i == 0) ? primaryOrange : new Color(0.32f, 0.20f, 0.14f, 0.55f);
            selectionBorders[i] = borderImg;

            Button btn = cardGO.AddComponent<Button>();
            btn.targetGraphic = borderImg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.92f, 0.82f, 1f);
            cb.pressedColor = new Color(1f, 0.70f, 0.35f, 1f);
            cb.selectedColor = Color.white;
            btn.colors = cb;

            UnityEventTools.AddObjectPersistentListener<GameObject>(
                btn.onClick,
                manager.SwitchFurniture,
                prefab
            );

            GameObject innerCard = CreateUIObject("InnerBg", cardGO.transform);
            RectTransform innerRT = innerCard.GetComponent<RectTransform>();
            StretchFull(innerRT, 5f, 5f);
            Image innerImg = innerCard.AddComponent<Image>();
            innerImg.sprite = roundedCard;
            innerImg.color = new Color(0.18f, 0.10f, 0.06f, 0.96f);
            innerImg.raycastTarget = false;

            GameObject iconGO = CreateUIObject("FurnitureIcon", innerCard.transform);
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.08f, 0.26f);
            iconRT.anchorMax = new Vector2(0.92f, 0.95f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            GameObject labelStrip = CreateUIObject("LabelStrip", innerCard.transform);
            RectTransform stripRT = labelStrip.GetComponent<RectTransform>();
            stripRT.anchorMin = new Vector2(0.04f, 0.03f);
            stripRT.anchorMax = new Vector2(0.96f, 0.25f);
            stripRT.offsetMin = Vector2.zero;
            stripRT.offsetMax = Vector2.zero;
            Image stripImg = labelStrip.AddComponent<Image>();
            stripImg.sprite = roundedBtn;
            stripImg.color = new Color(0.85f, 0.36f, 0.06f, 0.92f);
            stripImg.raycastTarget = false;

            Text itemTxt = CreateText("LabelText", labelStrip.transform, defaultFont,
                label, 19, FontStyle.Bold, creamText, TextAnchor.MiddleCenter);
            itemTxt.raycastTarget = false;
            StretchFull(itemTxt.GetComponent<RectTransform>(), 4f, 2f);
        }

        manager.buttonSelectionBorders = selectionBorders;

        // =====================================================================
        // 2) EN ALTTAKİ HAMBURGER BUTON ÇUBUĞU (BottomHamburgerBar)
        // (Tıklandığında alttan BottomMenuDrawer'ı açar / kapatır)
        // =====================================================================
        GameObject bottomBar = CreateUIObject("BottomHamburgerBar", canvasGO.transform);
        RectTransform barRT = bottomBar.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(1f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, 20f);
        barRT.sizeDelta = new Vector2(-32f, 92f);

        // Sol/Orta: Hamburger Menü Butonu (☰ İkonu + Başlık)
        GameObject hamBtnGO = CreateUIObject("Btn_HamburgerMenu", bottomBar.transform);
        RectTransform hamBtnRT = hamBtnGO.GetComponent<RectTransform>();
        hamBtnRT.anchorMin = new Vector2(0f, 0f);
        hamBtnRT.anchorMax = new Vector2(0.74f, 1f);
        hamBtnRT.offsetMin = Vector2.zero;
        hamBtnRT.offsetMax = Vector2.zero;

        Image hamBtnBg = hamBtnGO.AddComponent<Image>();
        hamBtnBg.sprite = roundedBtn;
        hamBtnBg.color = primaryOrange;

        Button hamBtn = hamBtnGO.AddComponent<Button>();
        hamBtn.targetGraphic = hamBtnBg;
        UnityEventTools.AddPersistentListener(hamBtn.onClick, manager.ToggleBottomMenuDrawer);

        // 3 Çizgili Hamburger İkonu (Butonun Solunda)
        GameObject hamIconGO = CreateUIObject("HamburgerIcon", hamBtnGO.transform);
        RectTransform hamIconRT = hamIconGO.GetComponent<RectTransform>();
        hamIconRT.anchorMin = new Vector2(0f, 0.5f);
        hamIconRT.anchorMax = new Vector2(0f, 0.5f);
        hamIconRT.pivot = new Vector2(0f, 0.5f);
        hamIconRT.anchoredPosition = new Vector2(18f, 0f);
        hamIconRT.sizeDelta = new Vector2(68f, 68f);
        Image hamIconImg = hamIconGO.AddComponent<Image>();
        hamIconImg.sprite = hamburgerSprite != null ? hamburgerSprite : roundedCard;
        hamIconImg.preserveAspect = true;
        hamIconImg.raycastTarget = false;

        Text hamTxt = CreateText("HamburgerLabel", hamBtnGO.transform, defaultFont,
            "MENÜ & EŞYALAR", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        hamTxt.raycastTarget = false;
        RectTransform hamTxtRT = hamTxt.GetComponent<RectTransform>();
        hamTxtRT.anchorMin = new Vector2(0.14f, 0f);
        hamTxtRT.anchorMax = new Vector2(0.98f, 1f);
        hamTxtRT.offsetMin = Vector2.zero;
        hamTxtRT.offsetMax = Vector2.zero;
        manager.hamburgerButtonText = hamTxt;

        // Sağ Alt: Eşya Sayacı Rozeti (Üstte değil, altta Hamburger butonunun yanında!)
        GameObject countBadge = CreateUIObject("CountBadge", bottomBar.transform);
        RectTransform countBadgeRT = countBadge.GetComponent<RectTransform>();
        countBadgeRT.anchorMin = new Vector2(0.76f, 0f);
        countBadgeRT.anchorMax = new Vector2(1f, 1f);
        countBadgeRT.offsetMin = Vector2.zero;
        countBadgeRT.offsetMax = Vector2.zero;
        Image countBg = countBadge.AddComponent<Image>();
        countBg.sprite = roundedBtn;
        countBg.color = darkPanelBg;

        Text countTxt = CreateText("CountBadgeText", countBadge.transform, defaultFont,
            "Eşya: 0", 25, FontStyle.Bold, warmAmber, TextAnchor.MiddleCenter);
        StretchFull(countTxt.GetComponent<RectTransform>());
        manager.countBadgeText = countTxt;

        // =====================================================================
        // 3) TAM EKRAN TURUNCU HOME SCREEN PANELİ
        // =====================================================================
        CreateHomeScreenPanel(canvasGO.transform, manager, defaultFont, roundedCard, roundedBtn);

        EditorUtility.SetDirty(manager);
    }

    private static void CreateHomeScreenPanel(
        Transform canvasTransform,
        FurniturePlacementManager manager,
        Font font,
        Sprite roundedCard,
        Sprite roundedBtn)
    {
        Color primaryOrange = new Color(1.00f, 0.45f, 0.06f, 1.00f);
        Color warmAmber     = new Color(1.00f, 0.65f, 0.20f, 1.00f);
        Color creamText     = new Color(1.00f, 0.96f, 0.90f, 1.00f);

        GameObject homePanel = CreateUIObject("HomeScreenPanel", canvasTransform);
        StretchFull(homePanel.GetComponent<RectTransform>());
        Image bgImg = homePanel.AddComponent<Image>();
        bgImg.color = new Color(0.10f, 0.05f, 0.02f, 0.96f);

        GameObject centerCard = CreateUIObject("HomeCenterCard", homePanel.transform);
        RectTransform cardRT = centerCard.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.06f, 0.18f);
        cardRT.anchorMax = new Vector2(0.94f, 0.82f);
        cardRT.offsetMin = Vector2.zero;
        cardRT.offsetMax = Vector2.zero;
        Image cardImg = centerCard.AddComponent<Image>();
        cardImg.sprite = roundedCard;
        cardImg.color = new Color(0.17f, 0.08f, 0.04f, 0.98f);

        GameObject topBar = CreateUIObject("TopOrangeBar", centerCard.transform);
        RectTransform topBarRT = topBar.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0.06f, 1f);
        topBarRT.anchorMax = new Vector2(0.94f, 1f);
        topBarRT.pivot = new Vector2(0.5f, 1f);
        topBarRT.anchoredPosition = new Vector2(0f, -10f);
        topBarRT.sizeDelta = new Vector2(0f, 10f);
        Image topBarImg = topBar.AddComponent<Image>();
        topBarImg.color = primaryOrange;

        Text titleTxt = CreateText("HomeTitle", centerCard.transform, font,
            "FURNISH AR", 72, FontStyle.Bold, warmAmber, TextAnchor.MiddleCenter);
        RectTransform titleRT = titleTxt.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.05f, 0.78f);
        titleRT.anchorMax = new Vector2(0.95f, 0.93f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        Text subTitleTxt = CreateText("HomeSubtitle", centerCard.transform, font,
            "Artırılmış Gerçeklik (AR) Oda & Mobilya Tasarımı", 28, FontStyle.Bold, creamText, TextAnchor.MiddleCenter);
        RectTransform subTitleRT = subTitleTxt.GetComponent<RectTransform>();
        subTitleRT.anchorMin = new Vector2(0.05f, 0.70f);
        subTitleRT.anchorMax = new Vector2(0.95f, 0.78f);
        subTitleRT.offsetMin = Vector2.zero;
        subTitleRT.offsetMax = Vector2.zero;

        GameObject previewRow = CreateUIObject("FurniturePreviewRow", centerCard.transform);
        RectTransform rowRT = previewRow.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0.06f, 0.48f);
        rowRT.anchorMax = new Vector2(0.94f, 0.67f);
        rowRT.offsetMin = Vector2.zero;
        rowRT.offsetMax = Vector2.zero;

        for (int i = 0; i < ModelKeys.Length; i++)
        {
            float w = 1f / ModelKeys.Length;
            GameObject icGO = CreateUIObject($"Preview_{ModelKeys[i]}", previewRow.transform);
            RectTransform icRT = icGO.GetComponent<RectTransform>();
            icRT.anchorMin = new Vector2(i * w + 0.01f, 0.05f);
            icRT.anchorMax = new Vector2((i + 1) * w - 0.01f, 0.95f);
            icRT.offsetMin = Vector2.zero;
            icRT.offsetMax = Vector2.zero;
            Image icImg = icGO.AddComponent<Image>();
            icImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/UI/Sprites/{ModelKeys[i]}_Icon.png");
            icImg.preserveAspect = true;
        }

        Text descTxt = CreateText("HomeGuideText", centerCard.transform, font,
            "• Zemini taratıp dokunarak eşyaları odaya yerleştirin\n" +
            "• Eşyayı parmağınızla tutup sürükleyerek taşıyın veya döndürün\n" +
            "• Alttaki Hamburger Menü butonundan tüm eşya ve araçları seçin",
            25, FontStyle.Normal, new Color(1f, 0.88f, 0.75f, 0.95f), TextAnchor.MiddleCenter);
        RectTransform descRT = descTxt.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0.07f, 0.27f);
        descRT.anchorMax = new Vector2(0.93f, 0.46f);
        descRT.offsetMin = Vector2.zero;
        descRT.offsetMax = Vector2.zero;

        CreateActionButton("Btn_StartAR", centerCard.transform, font, roundedBtn,
            "BAŞLA  -  AR EKRANINI AÇ",
            new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.22f),
            primaryOrange, Color.white,
            manager.CloseHomeScreen);

        manager.homeScreenPanel = homePanel;
    }

    private static Button CreateActionButton(
        string name,
        Transform parent,
        Font font,
        Sprite bgSprite,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color bgColor,
        Color textColor,
        UnityEngine.Events.UnityAction action)
    {
        GameObject btnGO = CreateUIObject(name, parent);
        RectTransform rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = btnGO.AddComponent<Image>();
        img.sprite = bgSprite;
        img.color = bgColor;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        UnityEventTools.AddPersistentListener(btn.onClick, action);

        Text txt = CreateText("Text", btnGO.transform, font, label, 23, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        txt.raycastTarget = false;
        StretchFull(txt.GetComponent<RectTransform>(), 6f, 4f);

        return btn;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Font font,
        string content,
        int fontSize,
        FontStyle style,
        Color color,
        TextAnchor alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        Text t = go.AddComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = alignment;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static void StretchFull(RectTransform rt, float padX = 0f, float padY = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    public static void EnsureAppNameAndIconConfigured()
    {
        string iconPath = "Assets/UI/Sprites/AppIcon.png";
        if (!File.Exists(iconPath)) return;

        Texture2D currentIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        if (currentIcon == null)
        {
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
            currentIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        }

        Texture2D[] existingIcons = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
        bool iconAlreadyAssigned = existingIcons != null && existingIcons.Length > 0 && existingIcons[0] != null;

        if (!iconAlreadyAssigned || PlayerSettings.productName == "FurnishAR_Orange" || PlayerSettings.productName == "ARApp")
        {
            ApplyCustomBranding("Turuncu AR Mobilya", "TuruncuAR Studio", currentIcon);
        }
    }

    public static void ApplyCustomBranding(string newProductName, string newCompanyName, Texture2D iconTexture)
    {
        if (!string.IsNullOrWhiteSpace(newCompanyName))
            PlayerSettings.companyName = newCompanyName.Trim();

        if (!string.IsNullOrWhiteSpace(newProductName))
            PlayerSettings.productName = newProductName.Trim();

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.turuncuar.mobilya");

        if (iconTexture != null)
        {
            string iconPath = AssetDatabase.GetAssetPath(iconTexture);
            TextureImporter ti = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.isReadable = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 512;
                ti.SaveAndReimport();
            }

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new Texture2D[] { iconTexture });
        }

        AssetDatabase.SaveAssets();
    }

    private static void ConfigurePlayerAndBuildSettings(string scenePath)
    {
        Texture2D appIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Sprites/AppIcon.png");
        ApplyCustomBranding("Turuncu AR Mobilya", "TuruncuAR Studio", appIcon);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new UnityEngine.Rendering.GraphicsDeviceType[]
        {
            UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
        });
        PlayerSettings.MTRendering = false;
        PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, false);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
    }
}
#endif
