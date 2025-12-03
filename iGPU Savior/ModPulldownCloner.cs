using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Reflection;

namespace PotatoOptimization
{
    /// <summary>
    /// Clone game's native dropdown component and customize it for MOD settings
    /// </summary>
    public class ModPulldownCloner
    {
        /// <summary>
        /// Clone the game's GraphicQualityPulldownList and clear its options
        /// Returns a ready-to-use empty pulldown GameObject
        /// </summary>
        public static GameObject CloneAndClearPulldown(Transform settingUITransform)
        {
            try
            {
                if (settingUITransform == null)
                {
                    PotatoPlugin.Log.LogError("settingUITransform is null");
                    return null;
                }

                // Find the original pulldown in Graphics settings
                Transform originalPath = settingUITransform.Find("Graphics/ScrollView/Viewport/Content/GraphicQualityPulldownList");
                if (originalPath == null)
                {
                    PotatoPlugin.Log.LogError("GraphicQualityPulldownList not found");
                    return null;
                }

                // Clone it
                GameObject clone = UnityEngine.Object.Instantiate(originalPath.gameObject);
                clone.name = "ModPulldownList";
                clone.SetActive(false);

                // Find the Content container (where option buttons are stored)
                Transform content = clone.transform.Find("PulldownList/Pulldown/CurrentSelectText (TMP)/Content");
                if (content == null)
                {
                    PotatoPlugin.Log.LogError("Cloned pulldown's Content container not found");
                    UnityEngine.Object.Destroy(clone);
                    return null;
                }

                // Clear all existing option buttons
                int childCount = content.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    Transform child = content.GetChild(i);
                    UnityEngine.Object.Destroy(child.gameObject);
                }

                // Keep Content always active, but ensure it's initially not visible (will be clipped by RectMask2D)
                content.gameObject.SetActive(true);
                PotatoPlugin.Log.LogInfo("Content initialized (always active, clipped by parent)");

                // Verify PulldownButton exists
                Transform pulldownButtonTransform = clone.transform.Find("PulldownList/PulldownButton");
                if (pulldownButtonTransform != null)
                {
                    Button pulldownButton = pulldownButtonTransform.GetComponent<Button>();
                    if (pulldownButton == null)
                    {
                        PotatoPlugin.Log.LogError("PulldownButton has no Button component");
                    }
                }
                else
                {
                    PotatoPlugin.Log.LogError("PulldownButton not found");
                }

                PotatoPlugin.Log.LogInfo($"Successfully cloned pulldown: {clone.name}");
                // Note: EnsurePulldownListUI will be called after parenting in CreateNativeDropdown
                return clone;
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to clone pulldown: {e}");
                return null;
            }
        }

        /// <summary>
        /// Get a template button from the original pulldown (to clone for new options)
        /// </summary>
        public static GameObject GetSelectButtonTemplate(Transform settingUITransform)
        {
            try
            {
                if (settingUITransform == null)
                {
                    PotatoPlugin.Log.LogError("settingUITransform is null");
                    return null;
                }

                // Get the first option button from GraphicQualityPulldownList as template
                Transform firstButton = settingUITransform.Find(
                    "Graphics/ScrollView/Viewport/Content/GraphicQualityPulldownList/PulldownList/Pulldown/CurrentSelectText (TMP)/Content"
                );

                if (firstButton != null && firstButton.childCount > 0)
                {
                    firstButton = firstButton.GetChild(0);
                }
                else
                {
                    firstButton = null;
                }

                if (firstButton == null)
                {
                    PotatoPlugin.Log.LogError("Original SelectButton template not found");
                    return null;
                }

                // Clone it as template
                GameObject template = UnityEngine.Object.Instantiate(firstButton.gameObject);
                template.name = "SelectButtonTemplate";
                template.SetActive(false);
                return template;
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to get SelectButton template: {e}");
                return null;
            }
        }

        /// <summary>
        /// Add an option to the pulldown
        /// </summary>
        /// <param name="pulldownClone">The cloned pulldown GameObject</param>
        /// <param name="buttonTemplate">Button template from GetSelectButtonTemplate()</param>
        /// <param name="optionText">Display text for this option</param>
        /// <param name="onClick">Callback when option is clicked</param>
        public static void AddOption(GameObject pulldownClone, GameObject buttonTemplate, string optionText, Action onClick)
        {
            try
            {
                // Find Content container
                Transform content = pulldownClone.transform.Find("PulldownList/Pulldown/CurrentSelectText (TMP)/Content");
                if (content == null)
                {
                    PotatoPlugin.Log.LogError("Content container not found");
                    return;
                }

                // Create new button from template
                GameObject newButton = UnityEngine.Object.Instantiate(buttonTemplate, content);
                newButton.name = $"SelectButton_{optionText}";
                newButton.SetActive(true);

                // Set button text
                TMP_Text buttonText = newButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    buttonText.text = optionText;
                }

                // Ensure all Image components have raycastTarget enabled
                var images = newButton.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in images)
                {
                    img.raycastTarget = true;
                }

                // Setup button click event
                Button button = newButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() =>
                    {
                        PotatoPlugin.Log.LogInfo($"Option clicked: {optionText}");
                        
                        // Update pulldown's displayed text and close dropdown via reflection
                        try
                        {
                            var pulldownUI = pulldownClone.GetComponent(Type.GetType("Bulbul.PulldownListUI, Assembly-CSharp"));
                            if (pulldownUI != null)
                            {
                                var pulldownType = pulldownUI.GetType();
                                
                                // Update selected text
                                var changeTextMethod = pulldownType.GetMethod("ChangeSelectContentText");
                                if (changeTextMethod != null)
                                {
                                    changeTextMethod.Invoke(pulldownUI, new object[] { optionText });
                                    PotatoPlugin.Log.LogInfo($"Updated selected text to: {optionText}");
                                }
                                
                                // Close the pulldown (same as native behavior)
                                var closePullDownMethod = pulldownType.GetMethod("ClosePullDown");
                                if (closePullDownMethod != null)
                                {
                                    closePullDownMethod.Invoke(pulldownUI, new object[] { false }); // false = with animation
                                    PotatoPlugin.Log.LogInfo("Dropdown closed via ClosePullDown()");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            PotatoPlugin.Log.LogWarning($"Failed to update dropdown: {ex.Message}");
                        }
                        
                        // Trigger user callback AFTER updating UI
                        onClick?.Invoke();
                    });

                    // Ensure button is interactable
                    if (!button.interactable)
                    {
                        button.interactable = true;
                    }
                    
                    // Ensure button's target graphic is set
                    if (button.targetGraphic == null)
                    {
                        var graphic = newButton.GetComponent<UnityEngine.UI.Image>();
                        if (graphic != null) button.targetGraphic = graphic;
                    }
                    
                    PotatoPlugin.Log.LogInfo($"Option button '{optionText}' configured successfully, interactable={button.interactable}, hasTargetGraphic={button.targetGraphic != null}");
                }
                else
                {
                    PotatoPlugin.Log.LogError($"Button '{optionText}' has no Button component");
                }
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to add option: {e}");
            }
        }

        /// <summary>
        /// Mount the pulldown to a parent in the settings UI
        /// </summary>
        public static void MountPulldown(GameObject pulldownClone, string parentPath)
        {
            try
            {
                // Find the settings root
                GameObject settingRoot = GameObject.Find("UI_FacilitySetting");
                if (settingRoot == null)
                {
                    PotatoPlugin.Log.LogError("UI_FacilitySetting not found");
                    return;
                }

                // Find the target parent
                Transform parent = settingRoot.transform.Find(parentPath);
                if (parent == null)
                {
                    PotatoPlugin.Log.LogError($"Parent path not found: {parentPath}");
                    return;
                }

                // Mount and activate
                pulldownClone.transform.SetParent(parent, false);
                pulldownClone.SetActive(true);
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to mount pulldown: {e}");
            }
        }

        /// <summary>
        /// Play the game's native click sound
        /// </summary>
        private static void PlayClickSound()
        {
            try
            {
                var settingUI = UnityEngine.Object.FindObjectOfType(Type.GetType("Bulbul.SettingUI, Assembly-CSharp"));
                if (settingUI != null)
                {
                    var systemSeServiceField = settingUI.GetType().GetField("_systemSeService",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (systemSeServiceField != null)
                    {
                        var systemSeService = systemSeServiceField.GetValue(settingUI);
                        if (systemSeService != null)
                        {
                            var playClickMethod = systemSeService.GetType().GetMethod("PlayClick");
                            playClickMethod?.Invoke(systemSeService, null);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogWarning($"Failed to play click sound: {e.Message}");
            }
        }

        /// <summary>
        /// Ensure the PulldownListUI component is properly configured on the cloned pulldown
        /// This recreates the setup that the game does natively for its pulldowns
        /// </summary>
        public static void EnsurePulldownListUI(GameObject clone, Transform originalPath, Transform content)
        {
            try
            {
                // Get PulldownListUI type via reflection (try multiple names)
                Type pulldownUIType = Type.GetType("Bulbul.PulldownListUI, Assembly-CSharp")
                    ?? Type.GetType("PulldownListUI, Assembly-CSharp")
                    ?? Type.GetType("PulldownListUI");
                if (pulldownUIType == null)
                {
                    PotatoPlugin.Log.LogError("PulldownListUI type not found (Assembly-CSharp)");
                    return;
                }

                // Find key transforms
                Transform pulldownList = clone.transform.Find("PulldownList");
                Transform pulldown = clone.transform.Find("PulldownList/Pulldown");
                Transform pulldownButton = clone.transform.Find("PulldownList/PulldownButton");
                Transform currentSelectText = clone.transform.Find("PulldownList/Pulldown/CurrentSelectText (TMP)");

                // Get or add PulldownListUI component
                Component pulldownUI = (pulldownList != null) 
                    ? pulldownList.GetComponent(pulldownUIType) 
                    : clone.GetComponent(pulldownUIType);
                    
                if (pulldownUI == null)
                {
                    GameObject attachTarget = (pulldownList != null) ? pulldownList.gameObject : clone;
                    pulldownUI = attachTarget.AddComponent(pulldownUIType);
                }

                // Get required components
                Button pulldownButtonComp = pulldownButton != null ? pulldownButton.GetComponent<Button>() : null;
                TMP_Text currentSelectTextComp = currentSelectText != null ? currentSelectText.GetComponent<TMP_Text>() : null;
                RectTransform pulldownParentRect = pulldown != null ? pulldown.GetComponent<RectTransform>() : null;
                RectTransform pulldownButtonRect = pulldownButton != null ? pulldownButton.GetComponent<RectTransform>() : null;
                RectTransform contentRect = content != null ? content.GetComponent<RectTransform>() : null;
                Canvas canvas = clone.GetComponentInParent<Canvas>();

                // Check if all required components are present
                bool ready = pulldownButtonComp != null 
                    && currentSelectTextComp != null 
                    && pulldownParentRect != null 
                    && pulldownButtonRect != null 
                    && canvas != null;
                    
                if (!ready)
                {
                    PotatoPlugin.Log.LogWarning($"PulldownListUI init skipped: pulldownButton={pulldownButtonComp != null}, currentSelectText={currentSelectTextComp != null}, pulldownRect={pulldownParentRect != null}, buttonRect={pulldownButtonRect != null}, canvas={canvas != null}");
                    return;
                }

                // Helper to set private fields via reflection
                void SetField(string fieldName, object value)
                {
                    if (value == null) return;
                    FieldInfo field = pulldownUIType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                    field?.SetValue(pulldownUI, value);
                }

                // Calculate open size (same logic as game's native pulldowns)
                float closeHeight = pulldownParentRect.rect.height;
                float contentHeight = contentRect != null ? contentRect.rect.height : 0f;
                
                // Limit content height and add scrolling if needed (max 6 options visible)
                float maxContentHeight = 240f; // About 6 options at 40px each
                bool needsScrolling = contentHeight > maxContentHeight;
                float visibleContentHeight = needsScrolling ? maxContentHeight : contentHeight;
                float openSize = closeHeight + visibleContentHeight + 20f; // +20 for padding
                
                // Add ScrollRect to Content if it has many options
                if (needsScrolling && contentRect != null)
                {
                    var scrollRect = content.GetComponent<ScrollRect>();
                    if (scrollRect == null)
                    {
                        scrollRect = content.gameObject.AddComponent<ScrollRect>();
                        scrollRect.horizontal = false;
                        scrollRect.vertical = true;
                        scrollRect.scrollSensitivity = 20f;
                        scrollRect.movementType = ScrollRect.MovementType.Clamped;
                        
                        // Create viewport for scrolling
                        var viewport = new GameObject("Viewport");
                        viewport.transform.SetParent(content, false);
                        var viewportRect = viewport.AddComponent<RectTransform>();
                        viewportRect.anchorMin = Vector2.zero;
                        viewportRect.anchorMax = Vector2.one;
                        viewportRect.sizeDelta = Vector2.zero;
                        viewport.AddComponent<RectMask2D>();
                        
                        scrollRect.viewport = viewportRect;
                        scrollRect.content = contentRect;
                        
                        // Resize content to enable scrolling
                        contentRect.sizeDelta = new UnityEngine.Vector2(contentRect.sizeDelta.x, contentHeight);
                        
                        PotatoPlugin.Log.LogInfo($"Added ScrollRect to Content: {contentHeight}px content in {maxContentHeight}px viewport");
                    }
                }
                
                // Position Content correctly - it should overflow parent bounds when Pulldown expands
                // Content is child of CurrentSelectText (TMP), not Pulldown
                if (contentRect != null)
                {
                    // Check parent hierarchy
                    Transform contentParent = content.parent;
                    string parentName = contentParent != null ? contentParent.name : "null";
                    PotatoPlugin.Log.LogInfo($"Content parent: {parentName}");
                    
                    // Set Content to anchor at bottom of its parent (CurrentSelectText)
                    // with Overflow enabled so it appears below when Pulldown expands
                    contentRect.anchorMin = new UnityEngine.Vector2(0f, 0f);
                    contentRect.anchorMax = new UnityEngine.Vector2(1f, 0f);
                    contentRect.pivot = new UnityEngine.Vector2(0.5f, 1f); // Pivot at top
                    contentRect.anchoredPosition = new UnityEngine.Vector2(0f, 0f);
                    
                    // Ensure Overflow is enabled on parent (not clipped)
                    var parentImage = contentParent?.GetComponent<UnityEngine.UI.Image>();
                    if (parentImage != null)
                    {
                        // Check if parent has RectMask2D (which would clip content)
                        var parentMask = contentParent.GetComponent<RectMask2D>();
                        if (parentMask != null)
                        {
                            PotatoPlugin.Log.LogWarning($"Parent {parentName} has RectMask2D which may clip Content");
                        }
                    }
                    
                    PotatoPlugin.Log.LogInfo($"Content positioned: anchor=({contentRect.anchorMin}, {contentRect.anchorMax}), pivot={contentRect.pivot}, pos={contentRect.anchoredPosition}");
                }
                else
                {
                    PotatoPlugin.Log.LogWarning("contentRect is null, cannot position Content");
                }
                
                PotatoPlugin.Log.LogInfo($"Pulldown sizes - Close: {closeHeight}, Content: {contentHeight}, Open: {openSize}, Scrolling: {needsScrolling}");

                // Add Canvas to Pulldown for layer control (brings dropdown above scroll bar)
                Canvas pulldownCanvas = pulldown.GetComponent<Canvas>();
                if (pulldownCanvas == null)
                {
                    pulldownCanvas = pulldown.gameObject.AddComponent<Canvas>();
                    pulldownCanvas.overrideSorting = true;
                    pulldownCanvas.sortingOrder = -1; // Below other UI when closed (will be 1000 when open)
                    
                    // Add GraphicRaycaster for proper click detection
                    pulldown.gameObject.AddComponent<GraphicRaycaster>();
                    PotatoPlugin.Log.LogInfo("Added Canvas to Pulldown (initial sortingOrder=-1)");
                }
                
                // Add helper component to control Canvas sorting order based on _isOpen
                // Attach to clone root so it's always active (not affected by pulldown open/close)
                var layerController = clone.AddComponent<PulldownLayerController>();
                layerController.Initialize(pulldownUI, pulldownCanvas);

                // Set private fields (mimicking game's setup)
                SetField("_currentSelectContentText", currentSelectTextComp);
                SetField("_pullDownParentRect", pulldownParentRect);
                SetField("_openPullDownSizeDeltaY", openSize);
                SetField("_pullDownOpenCloseSeconds", 0.3f);
                // Set Ease.OutCubic (enum value = 6) via reflection
                Type easeType = Type.GetType("DG.Tweening.Ease, DOTween");
                if (easeType != null) SetField("_pullDownOpenCloseEase", Enum.ToObject(easeType, 6)); // 6 = OutCubic
                SetField("_pullDownOpenButton", pulldownButtonComp);
                SetField("_pullDownButtonRect", pulldownButtonRect);
                // Initialize _isOpen to false (closed state)
                SetField("_isOpen", false);
                SetField("_closePullDownSizeDeltaY", 0f); // Will be set by Setup()
                
                PotatoPlugin.Log.LogInfo("All PulldownListUI fields configured via reflection");

                // Call Setup() method (same as game does in SettingUI.Setup())
                // This binds the button onClick to TogglePullDown() which handles DOTween animations
                MethodInfo setupMethod = pulldownUIType.GetMethod("Setup", BindingFlags.Public | BindingFlags.Instance);
                if (setupMethod != null)
                {
                    setupMethod.Invoke(pulldownUI, null);
                    
                    // Verify setup results
                    FieldInfo isOpenField = pulldownUIType.GetField("_isOpen", BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo closeHeightField = pulldownUIType.GetField("_closePullDownSizeDeltaY", BindingFlags.NonPublic | BindingFlags.Instance);
                    bool isOpen = isOpenField != null ? (bool)isOpenField.GetValue(pulldownUI) : false;
                    float actualCloseHeight = closeHeightField != null ? (float)closeHeightField.GetValue(pulldownUI) : 0f;
                    
                    PotatoPlugin.Log.LogInfo($"PulldownListUI.Setup() invoked - _isOpen={isOpen}, _closePullDownSizeDeltaY={actualCloseHeight}");
                    
                    // Check if MonoBehaviour Update will run
                    MonoBehaviour mb = pulldownUI as MonoBehaviour;
                    if (mb != null)
                    {
                        PotatoPlugin.Log.LogInfo($"PulldownListUI MonoBehaviour - enabled={mb.enabled}, active={mb.gameObject.activeInHierarchy}");
                    }
                }
                else
                {
                    PotatoPlugin.Log.LogWarning("Setup() method not found on PulldownListUI");
                }
                
                PotatoPlugin.Log.LogInfo("PulldownListUI component configured successfully");
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to configure PulldownListUI: {e}");
            }
        }
    }

    /// <summary>
    /// Helper component to control Canvas sorting order based on PulldownListUI._isOpen state
    /// Attached to root GameObject to ensure Update() always runs
    /// </summary>
    public class PulldownLayerController : MonoBehaviour
    {
        private Component pulldownUI;
        private Canvas targetCanvas;
        private FieldInfo isOpenField;
        private bool lastIsOpen = false;

        public void Initialize(Component pulldownUIComponent, Canvas canvas)
        {
            pulldownUI = pulldownUIComponent;
            targetCanvas = canvas;
            
            if (pulldownUI != null)
            {
                isOpenField = pulldownUI.GetType().GetField("_isOpen", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        private void Update()
        {
            if (pulldownUI == null || targetCanvas == null || isOpenField == null) return;

            try
            {
                bool isOpen = (bool)isOpenField.GetValue(pulldownUI);
                
                // Only update when state changes to reduce overhead
                if (isOpen != lastIsOpen)
                {
                    // Bring to front when open, hide below when closed
                    targetCanvas.sortingOrder = isOpen ? 1000 : -1;
                    lastIsOpen = isOpen;
                    PotatoPlugin.Log.LogInfo($"Dropdown layer changed: isOpen={isOpen}, sortingOrder={targetCanvas.sortingOrder}");
                }
            }
            catch
            {
                // Ignore errors silently
            }
        }
    }
}
