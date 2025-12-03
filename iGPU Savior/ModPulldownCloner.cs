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
        // === 核心修复 1：通用类型获取方法 (防止 Type cannot be null) ===
        private static Type GetPulldownUIType()
        {
            return Type.GetType("Bulbul.PulldownListUI, Assembly-CSharp")
                ?? Type.GetType("PulldownListUI, Assembly-CSharp")
                ?? Type.GetType("PulldownListUI");
        }

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
        public static void AddOption(GameObject pulldownClone, GameObject buttonTemplate, string optionText, Action onClick)
        {
            try
            {
                // Find Content container
                Transform content = pulldownClone.transform.Find("PulldownList/Pulldown/CurrentSelectText (TMP)/Content");
                if (content == null)
                {
                    // Check if we already moved content to Viewport (scrolling enabled)
                    // If content was moved, the path above won't work, so we search recursively
                    content = pulldownClone.transform.Find("PulldownList/Pulldown/CurrentSelectText (TMP)/ScrollView/Viewport/Content");
                    
                    // Fallback search
                    if (content == null) {
                         var allContent = pulldownClone.GetComponentsInChildren<RectTransform>(true);
                         foreach(var rt in allContent) {
                             if (rt.name == "Content") { content = rt; break; }
                         }
                    }
                    
                    if (content == null) {
                        PotatoPlugin.Log.LogError("Content container not found");
                        return;
                    }
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
                        
                        // === 修复点：使用通用方法获取类型，不再写死字符串 ===
                        try
                        {
                            Type pulldownType = GetPulldownUIType(); // <--- 使用新方法
                            if (pulldownType != null)
                            {
                                // 尝试在自身或子物体查找组件
                                var pulldownUI = pulldownClone.GetComponent(pulldownType);
                                if (pulldownUI == null) 
                                    pulldownUI = pulldownClone.GetComponentInChildren(pulldownType);

                                if (pulldownUI != null)
                                {
                                    // Update selected text
                                    var changeTextMethod = pulldownType.GetMethod("ChangeSelectContentText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (changeTextMethod != null)
                                    {
                                        changeTextMethod.Invoke(pulldownUI, new object[] { optionText });
                                        PotatoPlugin.Log.LogInfo($"Updated selected text to: {optionText}");
                                    }
                                    
                                    // Close the pulldown
                                    var closePullDownMethod = pulldownType.GetMethod("ClosePullDown", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (closePullDownMethod != null)
                                    {
                                        closePullDownMethod.Invoke(pulldownUI, new object[] { false }); 
                                        PotatoPlugin.Log.LogInfo("Dropdown closed via ClosePullDown()");
                                    }
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

                    if (!button.interactable) button.interactable = true;
                    
                    if (button.targetGraphic == null)
                    {
                        var graphic = newButton.GetComponent<UnityEngine.UI.Image>();
                        if (graphic != null) button.targetGraphic = graphic;
                    }
                }
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to add option: {e}");
            }
        }

        public static void MountPulldown(GameObject pulldownClone, string parentPath)
        {
            try
            {
                GameObject settingRoot = GameObject.Find("UI_FacilitySetting");
                if (settingRoot == null) return;

                Transform parent = settingRoot.transform.Find(parentPath);
                if (parent == null) return;

                pulldownClone.transform.SetParent(parent, false);
                pulldownClone.SetActive(true);
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to mount pulldown: {e}");
            }
        }

        /// <summary>
        /// Ensure the PulldownListUI component is properly configured on the cloned pulldown
        /// </summary>
        public static void EnsurePulldownListUI(GameObject clone, Transform originalPath, Transform content, float manualContentHeight = -1f)
        {
            try
            {
                // 1. 获取类型 (使用通用方法)
                Type pulldownUIType = GetPulldownUIType();
                if (pulldownUIType == null)
                {
                    PotatoPlugin.Log.LogError("PulldownListUI type not found");
                    return;
                }

                // 2. 找到关键节点
                Transform pulldownList = clone.transform.Find("PulldownList");
                Transform pulldown = clone.transform.Find("PulldownList/Pulldown");
                Transform pulldownButton = clone.transform.Find("PulldownList/PulldownButton");
                Transform currentSelectText = clone.transform.Find("PulldownList/Pulldown/CurrentSelectText (TMP)");

                // 3. 挂载 PulldownListUI 脚本
                GameObject uiHost = (pulldownList != null) ? pulldownList.gameObject : clone;
                Component pulldownUI = uiHost.GetComponent(pulldownUIType);
                if (pulldownUI == null) pulldownUI = uiHost.AddComponent(pulldownUIType);

                // 4. 获取必要的组件引用
                Button pulldownButtonComp = pulldownButton?.GetComponent<Button>();
                TMP_Text currentSelectTextComp = currentSelectText?.GetComponent<TMP_Text>();
                RectTransform pulldownParentRect = pulldown?.GetComponent<RectTransform>();
                RectTransform pulldownButtonRect = pulldownButton?.GetComponent<RectTransform>();
                RectTransform contentRect = content?.GetComponent<RectTransform>();

                if (pulldownButtonComp == null || currentSelectTextComp == null || pulldownParentRect == null) return;

                // 5. 反射辅助方法
                void SetField(string fieldName, object value) {
                    if (value == null) return;
                    pulldownUIType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(pulldownUI, value);
                }

                // =========================================================================
                // 🔥 核心修复：高度计算与滚动条构建 🔥
                // =========================================================================

                // A. 精确计算所需高度 (不依赖 Unity 自动布局，避免时序问题)
                int childCount = content.childCount;
                float itemHeight = 40f; // 标准按钮高度
                
                // 尝试从第一个子物体获取真实高度 (如果有)
                if (childCount > 0)
                {
                    var firstChild = content.GetChild(0).GetComponent<RectTransform>();
                    if (firstChild != null && firstChild.rect.height > 10) itemHeight = firstChild.rect.height;
                }
                
                float realContentHeight = childCount * itemHeight;

                // B. 滚动逻辑：如果高度超过阈值 (比如 6 个选项)，则限制高度并启用滚动
                float maxVisibleItems = 6f; // 最多显示 6 个
                float maxViewHeight = maxVisibleItems * itemHeight;
                
                bool needsScroll = realContentHeight > maxViewHeight;
                float finalViewHeight = needsScroll ? maxViewHeight : realContentHeight;
                
                // C. 计算展开动画的目标高度 (OpenSize)
                // 头部高度(通常40) + 显示内容高度 + 缓冲(10)
                float headerHeight = pulldownParentRect.rect.height; 
                float openSize = headerHeight + finalViewHeight + 10f;

                // D. 动态构建 ScrollView 结构 (如果需要且尚未构建)
                if (needsScroll)
                {
                    // 检查是否已经在 Viewport 里了
                    if (content.parent.name != "Viewport")
                    {
                        // 1. 创建 ScrollView (作为容器)
                        GameObject scrollView = new GameObject("ScrollView", typeof(RectTransform));
                        scrollView.transform.SetParent(content.parent, false); // 挂在原父节点下 (CurrentSelectText)
                        
                        var scrollRectRT = scrollView.GetComponent<RectTransform>();
                        scrollRectRT.anchorMin = Vector2.zero;
                        scrollRectRT.anchorMax = new Vector2(1f, 0f); // 底部对齐
                        scrollRectRT.pivot = new Vector2(0.5f, 1f);   // 顶部锚点
                        scrollRectRT.sizeDelta = new Vector2(0, finalViewHeight); // 宽度自适应，高度受限
                        scrollRectRT.anchoredPosition = Vector2.zero; // 贴紧头部下方

                        // 2. 添加 ScrollRect 组件
                        var scrollRect = scrollView.AddComponent<ScrollRect>();
                        scrollRect.horizontal = false;
                        scrollRect.vertical = true;
                        scrollRect.scrollSensitivity = 20f;
                        scrollRect.movementType = ScrollRect.MovementType.Clamped;

                        // 3. 创建 Viewport (遮罩层)
                        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                        viewport.transform.SetParent(scrollView.transform, false);
                        var viewRect = viewport.GetComponent<RectTransform>();
                        viewRect.anchorMin = Vector2.zero;
                        viewRect.anchorMax = Vector2.one;
                        viewRect.sizeDelta = Vector2.zero; // 填满 ScrollView

                        // 4. 将 Content 移入 Viewport
                        content.SetParent(viewport.transform, true);

                        // 5. 绑定引用
                        scrollRect.viewport = viewRect;
                        scrollRect.content = contentRect;

                        // 6. 修正 Content 参数
                        // 在 ScrollRect 中，Content 高度必须是真实总高度
                        contentRect.anchorMin = new Vector2(0, 1); // Top Left
                        contentRect.anchorMax = new Vector2(1, 1); // Top Right
                        contentRect.pivot = new Vector2(0.5f, 1f); 
                        contentRect.anchoredPosition = Vector2.zero;
                        contentRect.sizeDelta = new Vector2(0, realContentHeight);

                        // 添加 ContentSizeFitter 确保 Content 自动根据按钮撑大
                        var fitter = content.GetComponent<ContentSizeFitter>();
                        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                        PotatoPlugin.Log.LogInfo($"[ModPulldown] Created ScrollView for {childCount} items.");
                    }
                    else
                    {
                        // 如果已经有结构，更新 Viewport 高度
                        if (content.parent.parent != null) // ScrollView
                        {
                            var scrollViewRT = content.parent.parent.GetComponent<RectTransform>();
                            if (scrollViewRT != null)
                                scrollViewRT.sizeDelta = new Vector2(scrollViewRT.sizeDelta.x, finalViewHeight);
                        }
                    }
                }
                else
                {
                    // 不需要滚动时，确保 Content 高度正确，防止留白
                    if (contentRect != null)
                    {
                        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, realContentHeight);
                        contentRect.anchoredPosition = Vector2.zero;
                    }
                }

                // =========================================================================
                // 🔥 关键修复：把 Canvas 加在【clone 根节点】上 🔥 (之前已验证成功)
                // =========================================================================
                
                Canvas rootCanvas = clone.GetComponent<Canvas>();
                if (rootCanvas == null)
                {
                    rootCanvas = clone.AddComponent<Canvas>();
                    // 默认关闭 overrideSorting，避免关闭状态下层级异常
                    rootCanvas.overrideSorting = false; 
                    rootCanvas.sortingOrder = 0; 
                    
                    if (clone.GetComponent<GraphicRaycaster>() == null)
                        clone.AddComponent<GraphicRaycaster>();
                        
                    PotatoPlugin.Log.LogInfo("✅ Canvas added to ROOT object (ModPulldownList)");
                }
                
                // 🧹 清理子物体 Canvas
                if (pulldown != null) {
                    var childCanvas = pulldown.GetComponent<Canvas>();
                    if (childCanvas != null) UnityEngine.Object.Destroy(childCanvas);
                }
                if (pulldownList != null) {
                    var childCanvas = pulldownList.GetComponent<Canvas>();
                    if (childCanvas != null) UnityEngine.Object.Destroy(childCanvas);
                }

                // 7. 初始化层级控制器
                var layerController = clone.GetComponent<PulldownLayerController>();
                if (layerController == null) layerController = clone.AddComponent<PulldownLayerController>();
                
                layerController.Initialize(pulldownUI, rootCanvas);

                // 8. 继续反射赋值
                SetField("_currentSelectContentText", currentSelectTextComp);
                SetField("_pullDownParentRect", pulldownParentRect);
                SetField("_openPullDownSizeDeltaY", openSize); // 使用精确计算后的 openSize
                SetField("_pullDownOpenCloseSeconds", 0.3f);
                SetField("_pullDownOpenButton", pulldownButtonComp);
                SetField("_pullDownButtonRect", pulldownButtonRect);
                SetField("_isOpen", false);

                // 9. 调用原版 Setup 方法
                pulldownUIType.GetMethod("Setup")?.Invoke(pulldownUI, null);
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
        private bool isInitialized = false;

        public void Initialize(Component pulldownUIComponent, Canvas canvas)
        {
            pulldownUI = pulldownUIComponent;
            targetCanvas = canvas;
            
            if (pulldownUI != null)
            {
                isOpenField = pulldownUI.GetType().GetField("_isOpen", BindingFlags.NonPublic | BindingFlags.Instance);
                isInitialized = true;
                
                // 强制刷新一次状态
                UpdateSortingOrder(false);
                PotatoPlugin.Log.LogInfo("PulldownLayerController initialized successfully");
            }
        }

        private void Update()
        {
            if (!isInitialized || pulldownUI == null || targetCanvas == null || isOpenField == null) return;

            try
            {
                bool isOpen = (bool)isOpenField.GetValue(pulldownUI);
                
                // Only update when state changes to reduce overhead
                if (isOpen != lastIsOpen)
                {
                    UpdateSortingOrder(isOpen);
                    lastIsOpen = isOpen;
                }
            }
            catch
            {
                // Ignore errors silently
            }
        }

        private void UpdateSortingOrder(bool isOpen)
        {
            if (targetCanvas == null) return;

            // ========== 优化修复：开关 overrideSorting ==========
            // 展开时：开启 overrideSorting 并设置为 30000，确保盖住所有东西
            // 收起时：关闭 overrideSorting，让它回归父级 Layout 的自然层级
            
            if (isOpen)
            {
                targetCanvas.overrideSorting = true;
                targetCanvas.sortingOrder = 30000;
            }
            else
            {
                targetCanvas.overrideSorting = false;
                targetCanvas.sortingOrder = 0;
            }
        }
    }
}