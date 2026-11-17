using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Furnish AR - Yüzey Tarama, Elle Eşya Yerleştirme / Taşıma / Döndürme,
/// Home Screen ve Alttan Açılan Hamburger Menü Yöneticisi
/// </summary>
public class FurniturePlacementManager : MonoBehaviour
{
    [Header("=== VİDEODAKİ TEMEL AR BİLEŞENLERİ ===")]
    [Tooltip("Şu an seçili olan ve ekrana dokunulduğunda yerleştirilecek eşya Prefab'ı")]
    public GameObject SpawnableFurniture;

    [Tooltip("Sahnedeki AR Session Origin bileşeni")]
    public ARSessionOrigin sessionOrigin;

    [Tooltip("Yüzeylere ışın (Raycast) göndermek için kullanılan AR Raycast Manager")]
    public ARRaycastManager raycastManager;

    [Tooltip("Gerçek dünyadaki yatay yüzeyleri (zemin/masa) algılayan AR Plane Manager")]
    public ARPlaneManager planeManager;

    [Header("=== ÖZELLEŞTİRİLMİŞ MOBİLYA KATALOĞU ===")]
    [Tooltip("Kullanıcının alt menüden seçebileceği tüm mobilya Prefab listesi")]
    public GameObject[] furnitureCatalog;

    [Tooltip("Mobilyaların arayüzde görünecek Türkçe isimleri")]
    public string[] furnitureNames = new string[]
    {
        "Modern Kanepe",
        "Tekli Berjer",
        "Orta Sehpa",
        "Tripod Lambader",
        "Dekoratif Kitaplık"
    };

    [Header("=== TURUNCU TEMALI ARAYÜZ & ALT HAMBURGER MENÜ BAĞLANTILARI ===")]
    public GameObject homeScreenPanel;
    public RectTransform bottomMenuDrawer;
    public Text hamburgerButtonText;
    public Text statusText;
    public Text selectedItemText;
    public Text countBadgeText;
    public Text planeToggleButtonText;
    public Image[] buttonSelectionBorders;

    [Header("=== YERLEŞTİRME VE ELLE KONTROL AYARLARI ===")]
    [Tooltip("Videodaki gibi eşya koyduktan sonra yüzey çizgilerini otomatik gizle")]
    public bool hidePlanesAfterPlacement = true;

    [Tooltip("Parmağı sürükleyerek eşyayı zeminde elle taşıma özelliği")]
    public bool enableHandDragPlacement = true;

    [Tooltip("İki parmakla (veya bilgisayarda Sağ Tık sürükleyerek) eşyayı elle döndürme özelliği")]
    public bool enableHandRotation = true;

    [Tooltip("Elle döndürme hassasiyeti")]
    public float handRotationSensitivity = 0.45f;

    [Tooltip("Unity Editor içinde bilgisayarda Play tuşuna basıldığında fare ile test zemini oluştur")]
    public bool enableEditorSimulationFloor = true;

    // Videodaki AR Raycast çarpmalarını tutan liste
    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();

    // Sahneye yerleştirilen eşyaların geçmişi
    private List<GameObject> placedFurnitures = new List<GameObject>();
    private GameObject currentSelectedInstance = null;
    private bool planesVisible = true;
    private GameObject editorSimFloor = null;

    // Alttan Açılan Hamburger Menü Durumu
    private bool isBottomMenuOpen = false;
    private Coroutine drawerAnimCoroutine = null;
    private const float DrawerOpenY = 128f;
    private const float DrawerClosedY = -680f;

    // Elle Taşıma (Drag) ve Elle Döndürme (Rotate) Durum Değişkenleri
    private bool isDraggingFurniture = false;
    private Vector3 dragOffset = Vector3.zero;
    private Vector3 lastMousePos = Vector3.zero;

    // Turuncu Tema Renk Paleti
    private readonly Color orangeActiveBorder = new Color(1.00f, 0.52f, 0.08f, 1.00f);   // #FF8514 Parlak Turuncu
    private readonly Color orangeInactiveBorder = new Color(0.32f, 0.20f, 0.14f, 0.55f); // Koyu Sıcak Kahve/Turuncu

    private void Awake()
    {
        if (sessionOrigin == null)
            sessionOrigin = GetComponent<ARSessionOrigin>() ?? FindObjectOfType<ARSessionOrigin>();

        if (raycastManager == null)
            raycastManager = GetComponent<ARRaycastManager>() ?? FindObjectOfType<ARRaycastManager>();

        if (planeManager == null)
            planeManager = GetComponent<ARPlaneManager>() ?? FindObjectOfType<ARPlaneManager>();

        if (SpawnableFurniture == null && furnitureCatalog != null && furnitureCatalog.Length > 0)
            SpawnableFurniture = furnitureCatalog[0];
    }

    private void Start()
    {
#if UNITY_EDITOR
        if (enableEditorSimulationFloor)
        {
            CreateEditorSimulationEnvironment();
        }
#endif
        if (bottomMenuDrawer == null)
        {
            GameObject drawerGO = GameObject.Find("BottomMenuDrawer");
            if (drawerGO != null)
                bottomMenuDrawer = drawerGO.GetComponent<RectTransform>();
        }

        // Başlangıçta alt çekmece kapalı olsun, alttaki Hamburger butonuna basınca açılsın
        SetBottomMenuImmediate(false);

        EnsureHomeScreenExistsAtRuntime();
        if (homeScreenPanel != null)
        {
            homeScreenPanel.SetActive(true);
            homeScreenPanel.transform.SetAsLastSibling();
        }

        HighlightSelectedButton(0);
        UpdateUIStatus("Yüzeye dokunup eşya koyun; alttaki ☰ Menü butonundan tüm özellikleri seçin.");
        UpdateCounters();
    }

    private void Update()
    {
        // Home Screen açıkken arka plandaki AR sahnesine dokunulmasını engelle
        if (homeScreenPanel != null && homeScreenPanel.activeSelf)
            return;

        UpdatePlaneScanStatusMessage();

        // 1) MOBİL DOKUNMATİK KONTROLLER (Elle Yerleştirme, Sürükleyerek Taşıma ve İki Parmakla Döndürme)
        if (Input.touchCount > 0)
        {
            HandleMobileTouchInput();
        }
#if UNITY_EDITOR
        // 2) BİLGİSAYARDA (UNITY EDITOR) FARE İLE ELLE YERLEŞTİRME, SÜRÜKLEME VE DÖNDÜRME
        else
        {
            HandleEditorMouseInput();
        }
#endif
    }

    // =========================================================================
    // ALTTAN AÇILAN HAMBURGER MENÜ (BOTTOM DRAWER) FONKSİYONLARI
    // =========================================================================

    /// <summary>
    /// Alttaki Hamburger Butonuna basıldığında menüyü alttan yukarı doğru açar veya kapatır.
    /// </summary>
    public void ToggleBottomMenuDrawer()
    {
        SetBottomMenuOpen(!isBottomMenuOpen);
    }

    public void OpenBottomMenuDrawer()
    {
        SetBottomMenuOpen(true);
    }

    public void CloseBottomMenuDrawer()
    {
        SetBottomMenuOpen(false);
    }

    private void SetBottomMenuOpen(bool open)
    {
        isBottomMenuOpen = open;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        UpdateHamburgerButtonLabel();

        if (bottomMenuDrawer == null) return;

        if (open)
            bottomMenuDrawer.gameObject.SetActive(true);

        if (drawerAnimCoroutine != null)
            StopCoroutine(drawerAnimCoroutine);

        drawerAnimCoroutine = StartCoroutine(AnimateDrawerSlide(open));
    }

    private void SetBottomMenuImmediate(bool open)
    {
        isBottomMenuOpen = open;
        UpdateHamburgerButtonLabel();

        if (bottomMenuDrawer != null)
        {
            bottomMenuDrawer.anchoredPosition = new Vector2(0f, open ? DrawerOpenY : DrawerClosedY);
            bottomMenuDrawer.gameObject.SetActive(open);
        }
    }

    private void UpdateHamburgerButtonLabel()
    {
        if (hamburgerButtonText != null)
        {
            hamburgerButtonText.text = isBottomMenuOpen
                ? "MENÜYÜ KAPAT"
                : "MENÜ & EŞYALAR";
        }
    }

    private IEnumerator AnimateDrawerSlide(bool opening)
    {
        if (bottomMenuDrawer == null) yield break;

        float startY = bottomMenuDrawer.anchoredPosition.y;
        float targetY = opening ? DrawerOpenY : DrawerClosedY;
        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration && bottomMenuDrawer != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Yumuşak Ease-Out geçişi
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);
            float y = Mathf.Lerp(startY, targetY, smoothT);
            bottomMenuDrawer.anchoredPosition = new Vector2(0f, y);
            yield return null;
        }

        if (bottomMenuDrawer != null)
        {
            bottomMenuDrawer.anchoredPosition = new Vector2(0f, targetY);
            if (!opening)
                bottomMenuDrawer.gameObject.SetActive(false);
        }
    }

    // =========================================================================
    // HOME SCREEN (GİRİŞ EKRANI) FONKSİYONLARI
    // =========================================================================

    public void CloseHomeScreen()
    {
        if (homeScreenPanel != null)
        {
            homeScreenPanel.SetActive(false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        UpdateUIStatus("Yüzeye dokunarak eşya yerleştirin. Alttaki Menü butonundan eşya ve araçları seçebilirsiniz.");
    }

    public void OpenHomeScreen()
    {
        if (homeScreenPanel != null)
        {
            homeScreenPanel.SetActive(true);
            homeScreenPanel.transform.SetAsLastSibling();
        }
    }

    // =========================================================================
    // MOBİL ELLE YERLEŞTİRME, TAŞIMA VE DÖNDÜRME SİSTEMİ
    // =========================================================================

    private void HandleMobileTouchInput()
    {
        // İKİ PARMAK KONTROLÜ: Seçili Eşyayı Elle Döndürme (Twist / Kaydırma) ve Boyutlandırma (Pinch)
        if (Input.touchCount >= 2 && enableHandRotation)
        {
            isDraggingFurniture = false;
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            if (IsTouchOverUI(t0.fingerId) || IsTouchOverUI(t1.fingerId))
                return;

            GameObject target = GetActivePlacedFurniture();
            if (target == null) return;

            Vector2 prevPos0 = t0.position - t0.deltaPosition;
            Vector2 prevPos1 = t1.position - t1.deltaPosition;

            Vector2 prevDir = prevPos1 - prevPos0;
            Vector2 currDir = t1.position - t0.position;

            if (prevDir.sqrMagnitude > 1f && currDir.sqrMagnitude > 1f)
            {
                float twistAngle = Vector2.SignedAngle(prevDir, currDir);
                float avgSlideX = (t0.deltaPosition.x + t1.deltaPosition.x) * 0.5f;
                float totalYaw = (-twistAngle * 1.35f) - (avgSlideX * handRotationSensitivity);

                target.transform.Rotate(Vector3.up, totalYaw, Space.World);
            }

            float prevDist = prevDir.magnitude;
            float currDist = currDir.magnitude;
            if (prevDist > 15f && currDist > 15f)
            {
                float scaleFactor = currDist / prevDist;
                float newScale = Mathf.Clamp(target.transform.localScale.x * scaleFactor, 0.45f, 2.20f);
                target.transform.localScale = new Vector3(newScale, newScale, newScale);
            }

            UpdateUIStatus($"'{CleanName(target.name)}' elle döndürülüyor...");
            return;
        }

        // TEK PARMAK KONTROLÜ: Elle Yerleştirme ve Parmağı Sürükleyerek Eşya Taşıma
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            if (isButtonPressed() || IsTouchOverUI(touch.fingerId))
            {
                isDraggingFurniture = false;
                return;
            }

            if (TrySelectExistingFurniture(touch.position, out _))
            {
                if (enableHandDragPlacement && currentSelectedInstance != null)
                {
                    isDraggingFurniture = true;
                    if (TryGetFloorPosition(touch.position, currentSelectedInstance.transform.position.y, out Vector3 floorPt))
                        dragOffset = currentSelectedInstance.transform.position - floorPt;
                    else
                        dragOffset = Vector3.zero;
                }
                return;
            }

            if (raycastManager != null && raycastManager.Raycast(touch.position, raycastHits, TrackableType.PlaneWithinPolygon))
            {
                if (isButtonPressed() == false)
                {
                    Pose hitPose = raycastHits[0].pose;
                    PlaceFurnitureAtPose(hitPose.position, hitPose.rotation);
                    isDraggingFurniture = enableHandDragPlacement;
                    dragOffset = Vector3.zero;
                }
            }
        }
        else if (touch.phase == TouchPhase.Moved)
        {
            if (isDraggingFurniture && enableHandDragPlacement && currentSelectedInstance != null)
            {
                if (TryGetFloorPosition(touch.position, currentSelectedInstance.transform.position.y, out Vector3 floorPos))
                {
                    currentSelectedInstance.transform.position = floorPos + dragOffset;
                    UpdateUIStatus($"'{CleanName(currentSelectedInstance.name)}' elle taşınıyor...");
                }
            }
        }
        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            if (isDraggingFurniture && currentSelectedInstance != null)
            {
                UpdateUIStatus($"'{CleanName(currentSelectedInstance.name)}' konumlandırıldı.");
            }
            isDraggingFurniture = false;
        }
    }

#if UNITY_EDITOR
    private void HandleEditorMouseInput()
    {
        bool overUI = isButtonPressed() || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());

        if (Input.GetMouseButtonDown(0) && !overUI)
        {
            if (TrySelectExistingFurniture(Input.mousePosition, out _))
            {
                if (enableHandDragPlacement && currentSelectedInstance != null)
                {
                    isDraggingFurniture = true;
                    if (TryGetFloorPosition(Input.mousePosition, currentSelectedInstance.transform.position.y, out Vector3 floorPt))
                        dragOffset = currentSelectedInstance.transform.position - floorPt;
                    else
                        dragOffset = Vector3.zero;
                }
                return;
            }

            if (TryGetFloorPosition(Input.mousePosition, 0f, out Vector3 spawnPos))
            {
                PlaceFurnitureAtPose(spawnPos, Quaternion.identity);
                isDraggingFurniture = enableHandDragPlacement;
                dragOffset = Vector3.zero;
            }
        }
        else if (Input.GetMouseButton(0) && isDraggingFurniture && enableHandDragPlacement && currentSelectedInstance != null)
        {
            if (TryGetFloorPosition(Input.mousePosition, currentSelectedInstance.transform.position.y, out Vector3 newFloorPos))
            {
                currentSelectedInstance.transform.position = newFloorPos + dragOffset;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDraggingFurniture = false;
        }

        if (enableHandRotation)
        {
            GameObject target = GetActivePlacedFurniture();
            if (target != null)
            {
                if (Input.GetMouseButtonDown(1) && !overUI)
                {
                    lastMousePos = Input.mousePosition;
                }
                else if (Input.GetMouseButton(1) && !overUI)
                {
                    float deltaX = Input.mousePosition.x - lastMousePos.x;
                    lastMousePos = Input.mousePosition;
                    if (Mathf.Abs(deltaX) > 0.01f)
                    {
                        target.transform.Rotate(Vector3.up, -deltaX * handRotationSensitivity, Space.World);
                        UpdateUIStatus($"'{CleanName(target.name)}' elle döndürülüyor...");
                    }
                }

                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (!overUI && Mathf.Abs(scroll) > 0.001f)
                {
                    target.transform.Rotate(Vector3.up, scroll * 120f, Space.World);
                    UpdateUIStatus($"'{CleanName(target.name)}' döndürüldü.");
                }
            }
        }
    }
#endif

    private bool TryGetFloorPosition(Vector2 screenPosition, float fallbackFloorY, out Vector3 floorPosition)
    {
        if (raycastManager != null && raycastManager.Raycast(screenPosition, raycastHits, TrackableType.PlaneWithinPolygon))
        {
            floorPosition = raycastHits[0].pose.position;
            return true;
        }

        Camera cam = GetActiveCamera();
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(screenPosition);
            Plane horizontalFloor = new Plane(Vector3.up, new Vector3(0f, fallbackFloorY, 0f));
            if (horizontalFloor.Raycast(ray, out float enter) && enter > 0.1f && enter < 25f)
            {
                floorPosition = ray.GetPoint(enter);
                return true;
            }
        }

        floorPosition = Vector3.zero;
        return false;
    }

    private void PlaceFurnitureAtPose(Vector3 position, Quaternion surfaceRotation)
    {
        if (SpawnableFurniture == null)
        {
            UpdateUIStatus("Lütfen önce alt menüden bir eşya seçin!");
            return;
        }

        GameObject _object = Instantiate(SpawnableFurniture);
        _object.transform.position = position;

        Camera cam = GetActiveCamera();
        if (cam != null)
        {
            Vector3 lookDir = cam.transform.position - position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                _object.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            }
            else
            {
                _object.transform.rotation = surfaceRotation;
            }
        }
        else
        {
            _object.transform.rotation = surfaceRotation;
        }

        StartCoroutine(AnimatePopIn(_object.transform));

        placedFurnitures.Add(_object);
        currentSelectedInstance = _object;

        if (hidePlanesAfterPlacement && planeManager != null)
        {
            SetPlaneVisualsVisible(false);
            planesVisible = false;
            UpdatePlaneToggleText();
        }

        UpdateCounters();
        UpdateUIStatus($"{CleanName(SpawnableFurniture.name)} yerleştirildi! Parmağınızla sürükleyip taşıyabilir veya döndürebilirsiniz.");
    }

    public bool isButtonPressed()
    {
        if (EventSystem.current == null)
            return false;

        if (EventSystem.current.currentSelectedGameObject?.GetComponent<Button>() == null)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    private bool IsTouchOverUI(int fingerId)
    {
        if (EventSystem.current == null)
            return false;
        return EventSystem.current.IsPointerOverGameObject(fingerId) || EventSystem.current.IsPointerOverGameObject();
    }

    public void SwitchFurniture(GameObject furniture)
    {
        if (furniture == null) return;
        SpawnableFurniture = furniture;

        int foundIndex = -1;
        if (furnitureCatalog != null)
        {
            for (int i = 0; i < furnitureCatalog.Length; i++)
            {
                if (furnitureCatalog[i] == furniture)
                {
                    foundIndex = i;
                    break;
                }
            }
        }

        HighlightSelectedButton(foundIndex);

        string displayName = (foundIndex >= 0 && furnitureNames != null && foundIndex < furnitureNames.Length)
            ? furnitureNames[foundIndex]
            : CleanName(furniture.name);

        if (selectedItemText != null)
            selectedItemText.text = "Seçili Eşya: " + displayName;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        UpdateUIStatus($"{displayName} seçildi. Zemine dokunarak yerleştirebilirsiniz.");
    }

    public void SwitchFurnitureByIndex(int index)
    {
        if (furnitureCatalog != null && index >= 0 && index < furnitureCatalog.Length)
        {
            SwitchFurniture(furnitureCatalog[index]);
        }
    }

    // =========================================================================
    // ALT MENÜDEKİ ÖZELLİK BUTONLARI (DÖNDÜRME, BOYUTLANDIRMA, GERİ AL, TEMİZLE)
    // =========================================================================

    public void RotateSelectedLeft()
    {
        RotateActiveFurniture(-30f);
    }

    public void RotateSelectedRight()
    {
        RotateActiveFurniture(30f);
    }

    private void RotateActiveFurniture(float angleDegrees)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        GameObject target = GetActivePlacedFurniture();
        if (target == null)
        {
            UpdateUIStatus("Döndürmek için önce sahneye bir eşya yerleştirin!");
            return;
        }
        target.transform.Rotate(Vector3.up, angleDegrees, Space.World);
        UpdateUIStatus("Eşya döndürüldü.");
    }

    public void ScaleSelectedUp()
    {
        ScaleActiveFurniture(1.15f);
    }

    public void ScaleSelectedDown()
    {
        ScaleActiveFurniture(0.87f);
    }

    private void ScaleActiveFurniture(float factor)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        GameObject target = GetActivePlacedFurniture();
        if (target == null)
        {
            UpdateUIStatus("Boyutlandırmak için önce sahneye bir eşya yerleştirin!");
            return;
        }
        Vector3 newScale = target.transform.localScale * factor;
        float clamped = Mathf.Clamp(newScale.x, 0.45f, 2.20f);
        target.transform.localScale = new Vector3(clamped, clamped, clamped);
        UpdateUIStatus($"Eşya boyutu güncellendi (%{Mathf.RoundToInt(clamped * 100f)}).");
    }

    public void UndoLastFurniture()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (placedFurnitures.Count == 0)
        {
            UpdateUIStatus("Geri alınacak eşya yok.");
            return;
        }

        int lastIdx = placedFurnitures.Count - 1;
        GameObject lastObj = placedFurnitures[lastIdx];
        placedFurnitures.RemoveAt(lastIdx);

        if (lastObj != null)
            Destroy(lastObj);

        currentSelectedInstance = placedFurnitures.Count > 0 ? placedFurnitures[placedFurnitures.Count - 1] : null;
        UpdateCounters();

        if (placedFurnitures.Count == 0)
        {
            SetPlanesActive(true);
            UpdateUIStatus("Son eşya kaldırıldı. Yüzey tarama yeniden aktif.");
        }
        else
        {
            UpdateUIStatus("Son yerleştirilen eşya geri alındı.");
        }
    }

    public void ClearAllFurniture()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        foreach (GameObject obj in placedFurnitures)
        {
            if (obj != null)
                Destroy(obj);
        }
        placedFurnitures.Clear();
        currentSelectedInstance = null;

        SetPlanesActive(true);
        UpdateCounters();
        UpdateUIStatus("Tüm eşyalar temizlendi! Yeni eşya koymak için zemine dokunun.");
    }

    public void TogglePlanesVisibility()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        SetPlanesActive(!planesVisible);
        UpdateUIStatus(planesVisible
            ? "AR Yüzey tarama çizgileri açıldı."
            : "AR Yüzey tarama çizgileri gizlendi.");
    }

    private void SetPlanesActive(bool active)
    {
        planesVisible = active;
        hidePlanesAfterPlacement = !active;
        SetPlaneVisualsVisible(active);

        if (editorSimFloor != null)
        {
            MeshRenderer mr = editorSimFloor.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = active;
        }
        UpdatePlaneToggleText();
    }

    private void SetPlaneVisualsVisible(bool visible)
    {
        if (planeManager == null) return;
        foreach (var plane in planeManager.trackables)
        {
            MeshRenderer mr = plane.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = visible;
            LineRenderer lr = plane.GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = visible;
        }
    }

    private void UpdatePlaneToggleText()
    {
        if (planeToggleButtonText != null)
        {
            planeToggleButtonText.text = planesVisible ? "Yüzey: Açık" : "Yüzey: Gizli";
        }
    }

    private GameObject GetActivePlacedFurniture()
    {
        if (currentSelectedInstance != null)
            return currentSelectedInstance;

        if (placedFurnitures.Count > 0)
            return placedFurnitures[placedFurnitures.Count - 1];

        return null;
    }

    private bool TrySelectExistingFurniture(Vector2 screenPos, out Vector3 hitWorldPos)
    {
        hitWorldPos = Vector3.zero;
        Camera cam = GetActiveCamera();
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 25f))
        {
            for (int i = 0; i < placedFurnitures.Count; i++)
            {
                GameObject placed = placedFurnitures[i];
                if (placed != null && (hit.transform == placed.transform || hit.transform.IsChildOf(placed.transform)))
                {
                    currentSelectedInstance = placed;
                    hitWorldPos = hit.point;
                    StartCoroutine(AnimateSelectionPulse(placed.transform));
                    UpdateUIStatus($"'{CleanName(placed.name)}' seçildi. Sürükleyerek taşıyabilir veya döndürebilirsiniz.");
                    return true;
                }
            }
        }
        return false;
    }

    private void HighlightSelectedButton(int selectedIndex)
    {
        if (buttonSelectionBorders == null) return;
        for (int i = 0; i < buttonSelectionBorders.Length; i++)
        {
            if (buttonSelectionBorders[i] != null)
            {
                buttonSelectionBorders[i].color = (i == selectedIndex) ? orangeActiveBorder : orangeInactiveBorder;
            }
        }
    }

    private void UpdatePlaneScanStatusMessage()
    {
        if (planeManager != null && placedFurnitures.Count == 0 && planeManager.trackables.count > 0)
        {
            if (statusText != null && statusText.text.Contains("Yüzey taranıyor"))
            {
                UpdateUIStatus("Yüzey algılandı! Eşyayı yerleştirmek için zemine dokunun.");
            }
        }
    }

    private void UpdateUIStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void UpdateCounters()
    {
        if (countBadgeText != null)
            countBadgeText.text = $"Eşya: {placedFurnitures.Count}";
    }

    private Camera GetActiveCamera()
    {
        if (sessionOrigin != null && sessionOrigin.camera != null)
            return sessionOrigin.camera;
        return Camera.main;
    }

    private string CleanName(string rawName)
    {
        return rawName.Replace("(Clone)", "").Trim();
    }

    private void EnsureHomeScreenExistsAtRuntime()
    {
        if (homeScreenPanel != null) return;

        GameObject found = GameObject.Find("HomeScreenPanel");
        if (found != null)
        {
            homeScreenPanel = found;
        }
    }

    private IEnumerator AnimatePopIn(Transform target)
    {
        if (target == null) yield break;
        Vector3 finalScale = Vector3.one;
        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = Mathf.Sin(t * Mathf.PI * 0.5f) * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
            target.localScale = finalScale * s;
            yield return null;
        }

        if (target != null)
            target.localScale = finalScale;
    }

    private IEnumerator AnimateSelectionPulse(Transform target)
    {
        if (target == null) yield break;
        Vector3 baseScale = target.localScale;
        float duration = 0.16f;
        float elapsed = 0f;

        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float pulse = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
            target.localScale = baseScale * pulse;
            yield return null;
        }

        if (target != null)
            target.localScale = baseScale;
    }

#if UNITY_EDITOR
    private void CreateEditorSimulationEnvironment()
    {
        Camera cam = GetActiveCamera();
        if (cam != null && cam.transform.position == Vector3.zero)
        {
            cam.transform.position = new Vector3(0f, 1.45f, -2.35f);
            cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.14f, 0.11f, 0.09f, 1f);
        }

        editorSimFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        editorSimFloor.name = "Editor_AR_Simulation_Floor";
        editorSimFloor.transform.position = Vector3.zero;
        editorSimFloor.transform.localScale = new Vector3(0.6f, 1f, 0.6f);

        Collider col = editorSimFloor.GetComponent<Collider>();
        if (col != null) Destroy(col);

        MeshRenderer mr = editorSimFloor.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Material floorMat = new Material(Shader.Find("Standard"));
            floorMat.color = new Color(0.24f, 0.16f, 0.11f, 1f);
            floorMat.SetFloat("_Glossiness", 0.25f);
            mr.material = floorMat;
        }
    }
#endif
}
