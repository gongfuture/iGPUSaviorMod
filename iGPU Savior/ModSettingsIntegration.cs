using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Bulbul;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using ModShared;
using BepInEx.Configuration;
using R3;
using System.Reflection;

namespace PotatoOptimization
{
    /// <summary>
    /// 协程运行器(单例) - 用于运行延迟操作
    /// </summary>
    public class ModUICoroutineRunner : MonoBehaviour
    {
        private static ModUICoroutineRunner _instance;
        public static ModUICoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ModUI_CoroutineRunner");
                    Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<ModUICoroutineRunner>();
                    PotatoPlugin.Log.LogInfo("Created ModUICoroutineRunner");
                }
                return _instance;
            }
        }

        public void RunDelayed(float seconds, System.Action action)
        {
            StartCoroutine(DelayedAction(seconds, action));
        }

        private System.Collections.IEnumerator DelayedAction(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action?.Invoke();
        }
    }
    
    /// <summary>
    /// 将MOD设置集成到游戏设置UI
    /// </summary>
    [HarmonyPatch(typeof(SettingUI), "Setup")]
    public class ModSettingsIntegration
    {
        // 保存MOD相关的UI引用
        private static GameObject modContentParent;
        private static InteractableUI modInteractableUI;
        private static SettingUI cachedSettingUI;  // 缓存SettingUI实例
        
        static void Postfix(SettingUI __instance)
        {
            try
            {
                cachedSettingUI = __instance;  // 保存引用
                
                // 检查是否已经创建了MOD标签
                if (ModSettingsManager.Instance != null && ModSettingsManager.Instance.IsInitialized)
                {
                    // 标签已存在，直接注册当前MOD
                    RegisterModToExistingTab();
                }
                else
                {
                    // 标签不存在，创建新标签
                    CreateModSettingsTab(__instance);
                }
                
                // 关键：为所有原生标签按钮添加隐藏MOD标签的逻辑
                HookIntoTabButtons(__instance);
            }
            catch (System.Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to add mod settings: {e}");
            }
        }
        
        /// <summary>
        /// 为游戏原生的标签按钮添加点击监听，隐藏MOD标签
        /// </summary>
        static void HookIntoTabButtons(SettingUI settingUI)
        {
            try
            {
                // 获取所有原生标签按钮和对应的父对象
                var generalButton = AccessTools.Field(typeof(SettingUI), "_generalInteractableUI").GetValue(settingUI) as InteractableUI;
                var graphicButton = AccessTools.Field(typeof(SettingUI), "_graphicInteractableUI").GetValue(settingUI) as InteractableUI;
                var audioButton = AccessTools.Field(typeof(SettingUI), "_audioInteractableUI").GetValue(settingUI) as InteractableUI;
                var creditsButton = AccessTools.Field(typeof(SettingUI), "_creditsInteractableUI").GetValue(settingUI) as InteractableUI;
                
                var generalParent = AccessTools.Field(typeof(SettingUI), "_generalParent").GetValue(settingUI) as GameObject;
                var graphicParent = AccessTools.Field(typeof(SettingUI), "_graphicParent").GetValue(settingUI) as GameObject;
                var audioParent = AccessTools.Field(typeof(SettingUI), "_audioParent").GetValue(settingUI) as GameObject;
                var creditsParent = AccessTools.Field(typeof(SettingUI), "_creditsParent").GetValue(settingUI) as GameObject;
                
                // 为每个按钮添加点击监听
                AddHideModTabListener(generalButton, generalParent);
                AddHideModTabListener(graphicButton, graphicParent);
                AddHideModTabListener(audioButton, audioParent);
                AddHideModTabListener(creditsButton, creditsParent);
                
                PotatoPlugin.Log.LogWarning(">>> Successfully hooked into all tab buttons! <<<");
            }
            catch (System.Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to hook into tab buttons: {e}");
            }
        }
        
        /// <summary>
        /// 为按钮添加隐藏MOD标签的监听器
        /// </summary>
        static void AddHideModTabListener(InteractableUI button, GameObject targetParent)
        {
            if (button == null) return;
            
            var uiButton = button.GetComponent<Button>();
            if (uiButton != null)
            {
                uiButton.onClick.AddListener(() => {
                    // 使用协程延迟执行,确保游戏原生逻辑先执行
                    if (modContentParent != null && modContentParent.activeSelf)
                    {
                        // 延迟一帧执行隐藏逻辑
                        ModSettingsManager.Instance.StartCoroutine(HideModTabAndShowTarget(button, targetParent));
                    }
                });
            }
        }
        
        /// <summary>
        /// 延迟隐藏MOD标签并强制显示目标标签
        /// </summary>
        static System.Collections.IEnumerator HideModTabAndShowTarget(InteractableUI targetButton, GameObject targetParent)
        {
            // 等待一帧,让游戏的逻辑先执行
            yield return null;
            
            // 隐藏MOD标签
            if (modContentParent != null)
            {
                modContentParent.SetActive(false);
                modInteractableUI?.DeactivateUseUI(false);
            }
            
            // 强制显示目标标签(即使游戏认为它已经是当前标签)
            if (targetParent != null && !targetParent.activeSelf)
            {
                targetParent.SetActive(true);
                targetButton?.ActivateUseUI(false);
                PotatoPlugin.Log.LogInfo($"Force shown target tab: {targetParent.name}");
            }
        }
        
        /// <summary>
        /// 更新MOD按钮的文本
        /// </summary>
        static void UpdateModButtonText(GameObject modTabButton)
        {
            try
            {
                PotatoPlugin.Log.LogInfo($"开始更新MOD按钮文本，按钮名称: {modTabButton.name}");
                
                // 方法1: 深度递归查找所有TextMeshProUGUI
                int textCount = 0;
                RecursivelyChangeText(modTabButton.transform, ref textCount);
                
                PotatoPlugin.Log.LogInfo($"方法1: 递归修改了 {textCount} 个文本组件");
                
                // 方法2: 使用GetComponentsInChildren (包括inactive)
                var allTexts = modTabButton.GetComponentsInChildren<TextMeshProUGUI>(true);
                PotatoPlugin.Log.LogInfo($"方法2: GetComponentsInChildren 找到 {allTexts.Length} 个文本组件");
                
                foreach (var text in allTexts)
                {
                    if (text.text != "MOD")
                    {
                        PotatoPlugin.Log.LogInfo($"  修改文本: '{text.text}' -> 'MOD' (路径: {GetGameObjectPath(text.gameObject)})");
                        text.text = "MOD";
                    }
                }
                
                PotatoPlugin.Log.LogInfo("MOD按钮文本更新完成");
            }
            catch (System.Exception e)
            {
                PotatoPlugin.Log.LogError($"更新MOD按钮文本失败: {e.Message}\n{e.StackTrace}");
            }
        }
        
        /// <summary>
        /// 更新MOD内容页面的标题文本
        /// </summary>
        static void UpdateModContentText(GameObject modContentParent)
        {
            try
            {
                PotatoPlugin.Log.LogInfo($"开始更新MOD内容页面文本，页面名称: {modContentParent.name}");
                
                // 查找内容页面中直接的TextMeshProUGUI组件(通常是标题)
                var titleText = modContentParent.GetComponent<TextMeshProUGUI>();
                if (titleText != null)
                {
                    PotatoPlugin.Log.LogInfo($"找到内容页面标题文本: '{titleText.text}'");
                    titleText.text = "MOD设置";
                }
                
                // 查找ScrollRect的Content路径
                var scrollRect = modContentParent.GetComponentInChildren<ScrollRect>();
                Transform contentTransform = null;
                if (scrollRect != null && scrollRect.content != null)
                {
                    contentTransform = scrollRect.content.transform;
                    PotatoPlugin.Log.LogInfo($"找到ScrollRect Content: {GetGameObjectPath(contentTransform.gameObject)}");
                }
                
                // 查找所有子节点中的文本
                var allTexts = modContentParent.GetComponentsInChildren<TextMeshProUGUI>(true);
                PotatoPlugin.Log.LogInfo($"内容页面共找到 {allTexts.Length} 个文本组件");
                
                foreach (var text in allTexts)
                {
                    // 跳过Content内部的文本(那是我们添加的设置项)
                    if (contentTransform != null && text.transform.IsChildOf(contentTransform))
                    {
                        PotatoPlugin.Log.LogInfo($"  跳过Content内的文本: {text.text}");
                        continue;
                    }
                    
                    // 修改标题类文本
                    if (text.text.Contains("制作人员") || text.text.Contains("Credits"))
                    {
                        PotatoPlugin.Log.LogInfo($"  修改内容标题: '{text.text}' -> 'MOD设置' (路径: {GetGameObjectPath(text.gameObject)})");
                        text.text = "MOD设置";
                    }
                }
                
                PotatoPlugin.Log.LogInfo("MOD内容页面文本更新完成");
            }
            catch (System.Exception e)
            {
                PotatoPlugin.Log.LogError($"更新MOD内容页面文本失败: {e.Message}\n{e.StackTrace}");
            }
        }
        
        /// <summary>
        /// 延迟更新MOD标签UI(修改文本和调整布局)
        /// </summary>
        static System.Collections.IEnumerator UpdateModTabUI(GameObject modTabButton, Transform tabBarParent)
        {
            // 等待UI完全实例化
            yield return null;
            
            // 递归查找并修改所有TextMeshProUGUI组件
            int textCount = 0;
            RecursivelyChangeText(modTabButton.transform, ref textCount);
            
            PotatoPlugin.Log.LogInfo($"Total changed {textCount} text components to 'MOD'");
            
            // 调整标签栏的布局以居中显示所有标签
            yield return null; // 再等一帧
            AdjustTabBarLayout(tabBarParent);
        }
        
        /// <summary>
        /// 递归修改所有文本组件
        /// </summary>
        static void RecursivelyChangeText(Transform parent, ref int count)
        {
            // 检查当前对象
            var text = parent.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                string oldText = text.text;
                text.text = "MOD";
                PotatoPlugin.Log.LogInfo($"  [{count}] Changed '{oldText}' -> '{text.text}' in {GetGameObjectPath(parent.gameObject)}");
                count++;
            }
            
            // 递归检查所有子对象
            for (int i = 0; i < parent.childCount; i++)
            {
                RecursivelyChangeText(parent.GetChild(i), ref count);
            }
        }
        
        /// <summary>
        /// 获取GameObject的完整路径(用于调试)
        /// </summary>
        static string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform current = obj.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
        
        /// <summary>
        /// 延迟调整布局
        /// </summary>
        static System.Collections.IEnumerator DelayedLayoutAdjustment(Transform tabBarParent)
        {
            // 等待2帧确保UI完全构建
            yield return null;
            yield return null;
            
            PotatoPlugin.Log.LogInfo("Starting delayed layout adjustment...");
            AdjustTabBarLayout(tabBarParent);
        }
        
        /// <summary>
        /// 创建全新的 "MOD Settings" 标签页
        /// </summary>
        static void CreateModSettingsTab(SettingUI settingUI)
        {
            // 1. 获取Credits标签按钮作为参考
            var creditsButton = AccessTools.Field(typeof(SettingUI), "_creditsInteractableUI")
                .GetValue(settingUI) as InteractableUI;
            
            if (creditsButton == null)
            {
                PotatoPlugin.Log.LogError("Cannot find credits button!");
                return;
            }
            
            // 2. 克隆Credits按钮创建MOD标签按钮
            GameObject modTabButton = Object.Instantiate(creditsButton.gameObject);
            modTabButton.name = "ModSettingsTabButton";
            modTabButton.transform.SetParent(creditsButton.transform.parent, false);
            modTabButton.transform.SetSiblingIndex(creditsButton.transform.GetSiblingIndex() + 1);
            
            // 3. 克隆Credits内容页面
            var creditsParent = AccessTools.Field(typeof(SettingUI), "_creditsParent")
                .GetValue(settingUI) as GameObject;
            
            modContentParent = Object.Instantiate(creditsParent);
            modContentParent.name = "ModSettingsContent";
            modContentParent.transform.SetParent(creditsParent.transform.parent, false);
            modContentParent.SetActive(false);
            
            // 清空原有内容（保留ScrollRect）
            var scrollRect = modContentParent.GetComponentInChildren<ScrollRect>();
            var content = scrollRect.content;
            
            // 清除所有子对象
            foreach (Transform child in content)
            {
                Object.Destroy(child.gameObject);
            }
            
            // 重新配置Content
            var contentLayout = content.gameObject.GetComponent<VerticalLayoutGroup>();
            if (contentLayout == null)
            {
                contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            contentLayout.spacing = 20;
            contentLayout.padding = new RectOffset(0, 0, 20, 20);
            contentLayout.childControlHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            
            var contentFitter = content.gameObject.GetComponent<ContentSizeFitter>();
            if (contentFitter == null)
            {
                contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // 4. 创建ModSettingsManager单例
            GameObject managerObj = new GameObject("ModSettingsManager");
            Object.DontDestroyOnLoad(managerObj);
            var manager = managerObj.AddComponent<ModSettingsManager>();
            
            // 初始化
            manager.Initialize(modTabButton, content.gameObject, scrollRect);
            
            // 延迟修改按钮文本和布局(使用独立的CoroutineRunner)
            ModUICoroutineRunner.Instance.RunDelayed(0.3f, () => {
                UpdateModButtonText(modTabButton);
                UpdateModContentText(modContentParent);
                AdjustTabBarLayout(creditsButton.transform.parent);
            });
            
            // 5. 设置按钮点击事件
            modInteractableUI = modTabButton.GetComponent<InteractableUI>();
            if (modInteractableUI != null)
            {
                modInteractableUI.Setup();
                var btn = modInteractableUI.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => {
                        SwitchToModTab(settingUI);
                    });
                }
            }
            
            // 6. 注册当前MOD
            RegisterCurrentMod(manager);
            
            PotatoPlugin.Log.LogWarning(">>> MOD Settings tab created successfully! <<<");
        }
        
        /// <summary>
        /// 切换到MOD标签页
        /// </summary>
        static void SwitchToModTab(SettingUI settingUI)
        {
            // 隐藏其他标签页
            var generalParent = AccessTools.Field(typeof(SettingUI), "_generalParent").GetValue(settingUI) as GameObject;
            var graphicParent = AccessTools.Field(typeof(SettingUI), "_graphicParent").GetValue(settingUI) as GameObject;
            var audioParent = AccessTools.Field(typeof(SettingUI), "_audioParent").GetValue(settingUI) as GameObject;
            var creditsParent = AccessTools.Field(typeof(SettingUI), "_creditsParent").GetValue(settingUI) as GameObject;
            
            generalParent?.SetActive(false);
            graphicParent?.SetActive(false);
            audioParent?.SetActive(false);
            creditsParent?.SetActive(false);
            
            // 取消其他按钮的激活状态
            var generalButton = AccessTools.Field(typeof(SettingUI), "_generalInteractableUI").GetValue(settingUI) as InteractableUI;
            var graphicButton = AccessTools.Field(typeof(SettingUI), "_graphicInteractableUI").GetValue(settingUI) as InteractableUI;
            var audioButton = AccessTools.Field(typeof(SettingUI), "_audioInteractableUI").GetValue(settingUI) as InteractableUI;
            var creditsButton = AccessTools.Field(typeof(SettingUI), "_creditsInteractableUI").GetValue(settingUI) as InteractableUI;
            
            generalButton?.DeactivateUseUI(false);
            graphicButton?.DeactivateUseUI(false);
            audioButton?.DeactivateUseUI(false);
            creditsButton?.DeactivateUseUI(false);
            
            // 激活MOD标签
            modInteractableUI.ActivateUseUI(false);
            modContentParent.SetActive(true);
            
            // 强制重建布局并滚动到顶部
            LayoutRebuilder.ForceRebuildLayoutImmediate(modContentParent.GetComponent<RectTransform>());
            var scrollRect = modContentParent.GetComponentInChildren<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
            
            PotatoPlugin.Log.LogInfo("Switched to MOD Settings tab");
        }
        
        /// <summary>
        /// 注册到已存在的标签页
        /// </summary>
        static void RegisterModToExistingTab()
        {
            RegisterCurrentMod(ModSettingsManager.Instance);
        }
        
        /// <summary>
        /// 注册当前MOD的设置项
        /// </summary>
        static void RegisterCurrentMod(ModSettingsManager manager)
        {
            var modSection = manager.RegisterMod("iGPU Savior", "1.6.0");
            
            if (modSection == null)
            {
                PotatoPlugin.Log.LogWarning("Failed to register mod - already registered or manager not ready");
                return;
            }
            
            // 1. 是否开启镜像
            manager.AddToggle(
                modSection,
                "是否开启镜像",
                PotatoPlugin.CfgEnableMirror.Value,
                (value) => {
                    PotatoPlugin.CfgEnableMirror.Value = value;
                    PotatoPlugin.Log.LogInfo($"Mirror mode set to: {value}");
                }
            );
            
            // 2. 土豆模式绑定按键
            var potatoKeyOptions = GetFunctionKeyOptions();
            int potatoKeyIndex = GetKeyCodeIndex(PotatoPlugin.KeyPotatoMode.Value);
            
            manager.AddDropdown(
                modSection,
                "土豆模式绑定按键",
                potatoKeyOptions,
                potatoKeyIndex,
                (index) => {
                    var keyCode = GetKeyCodeFromIndex(index);
                    PotatoPlugin.KeyPotatoMode.Value = keyCode;
                    PotatoPlugin.Log.LogInfo($"Potato mode key set to: {keyCode}");
                }
            );
            
            // 3. 小窗模式绑定按键
            int pipKeyIndex = GetKeyCodeIndex(PotatoPlugin.KeyPiPMode.Value);
            
            manager.AddDropdown(
                modSection,
                "小窗模式绑定按键",
                potatoKeyOptions,
                pipKeyIndex,
                (index) => {
                    var keyCode = GetKeyCodeFromIndex(index);
                    PotatoPlugin.KeyPiPMode.Value = keyCode;
                    PotatoPlugin.Log.LogInfo($"PiP mode key set to: {keyCode}");
                }
            );
            
            // 4. 镜像绑定按键
            int mirrorKeyIndex = GetKeyCodeIndex(PotatoPlugin.KeyCameraMirror.Value);
            
            manager.AddDropdown(
                modSection,
                "镜像模式绑定按键",
                potatoKeyOptions,
                mirrorKeyIndex,
                (index) => {
                    var keyCode = GetKeyCodeFromIndex(index);
                    PotatoPlugin.KeyCameraMirror.Value = keyCode;
                    PotatoPlugin.Log.LogInfo($"Mirror mode key set to: {keyCode}");
                }
            );
            
            // 5. 小窗时拖动模式
            var dragModeOptions = new List<string> { "CTRL+左键", "ALT+左键", "右键" };
            int dragModeIndex = (int)PotatoPlugin.CfgDragMode.Value;
            
            manager.AddDropdown(
                modSection,
                "小窗时拖动模式",
                dragModeOptions,
                dragModeIndex,
                (index) => {
                    PotatoPlugin.CfgDragMode.Value = (DragMode)index;
                    PotatoPlugin.Log.LogInfo($"Drag mode set to: {(DragMode)index}");
                }
            );
            
            PotatoPlugin.Log.LogWarning(">>> iGPU Savior settings registered! <<<");
        }
        
        /// <summary>
        /// 获取功能键选项列表
        /// </summary>
        static List<string> GetFunctionKeyOptions()
        {
            return new List<string>
            {
                "F1", "F2", "F3", "F4", "F5", "F6",
                "F7", "F8", "F9", "F10", "F11", "F12"
            };
        }
        
        /// <summary>
        /// 从KeyCode获取下拉菜单索引
        /// </summary>
        static int GetKeyCodeIndex(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.F1: return 0;
                case KeyCode.F2: return 1;
                case KeyCode.F3: return 2;
                case KeyCode.F4: return 3;
                case KeyCode.F5: return 4;
                case KeyCode.F6: return 5;
                case KeyCode.F7: return 6;
                case KeyCode.F8: return 7;
                case KeyCode.F9: return 8;
                case KeyCode.F10: return 9;
                case KeyCode.F11: return 10;
                case KeyCode.F12: return 11;
                default: return 1; // 默认F2
            }
        }
        
        /// <summary>
        /// 从下拉菜单索引获取KeyCode
        /// </summary>
        static KeyCode GetKeyCodeFromIndex(int index)
        {
            switch (index)
            {
                case 0: return KeyCode.F1;
                case 1: return KeyCode.F2;
                case 2: return KeyCode.F3;
                case 3: return KeyCode.F4;
                case 4: return KeyCode.F5;
                case 5: return KeyCode.F6;
                case 6: return KeyCode.F7;
                case 7: return KeyCode.F8;
                case 8: return KeyCode.F9;
                case 9: return KeyCode.F10;
                case 10: return KeyCode.F11;
                case 11: return KeyCode.F12;
                default: return KeyCode.F2;
            }
        }
        
        /// <summary>
        /// 调整标签栏布局,使5个标签居中显示
        /// 通过调整容器的RectTransform位置来实现居中
        /// </summary>
        static void AdjustTabBarLayout(Transform tabBarParent)
        {
            try
            {
                var rectTransform = tabBarParent.GetComponent<RectTransform>();
                var horizontalLayout = tabBarParent.GetComponent<HorizontalLayoutGroup>();
                
                if (horizontalLayout == null || rectTransform == null)
                {
                    PotatoPlugin.Log.LogWarning("HorizontalLayoutGroup or RectTransform not found");
                    return;
                }

                // 记录当前状态
                PotatoPlugin.Log.LogInfo($"Tab bar has {tabBarParent.childCount} children");
                PotatoPlugin.Log.LogInfo($"Original position: {rectTransform.anchoredPosition}");
                
                // 强制刷新布局以获取正确的尺寸
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                
                // 获取第一个标签的宽度(所有标签应该是相同宽度)
                if (tabBarParent.childCount == 0)
                {
                    PotatoPlugin.Log.LogWarning("No children found in tab bar");
                    return;
                }
                
                var firstChild = tabBarParent.GetChild(0).GetComponent<RectTransform>();
                if (firstChild == null)
                {
                    PotatoPlugin.Log.LogWarning("First child has no RectTransform");
                    return;
                }
                
                // 计算宽度
                float tabWidth = firstChild.rect.width;
                float spacing = horizontalLayout.spacing;
                int tabCount = tabBarParent.childCount;
                
                // 原4标签总宽度
                float original4TabsWidth = (tabWidth * 4) + (spacing * 3);
                
                // 现5标签总宽度
                float current5TabsWidth = (tabWidth * 5) + (spacing * 4);
                
                // 宽度增加量
                float widthIncrease = current5TabsWidth - original4TabsWidth;
                
                // 容器需要向左移动增加宽度的一半(因为标签从左到右排列)
                float positionOffset = widthIncrease / 2f;
                
                PotatoPlugin.Log.LogInfo($"Tab width: {tabWidth}, Spacing: {spacing}");
                PotatoPlugin.Log.LogInfo($"Original 4-tabs width: {original4TabsWidth}");
                PotatoPlugin.Log.LogInfo($"Current 5-tabs width: {current5TabsWidth}");
                PotatoPlugin.Log.LogInfo($"Width increase: {widthIncrease}, Position offset: -{positionOffset}");
                
                // 调整容器X坐标 - 向左移(减去offset)
                Vector2 currentPos = rectTransform.anchoredPosition;
                rectTransform.anchoredPosition = new Vector2(
                    currentPos.x - positionOffset,
                    currentPos.y
                );
                
                PotatoPlugin.Log.LogInfo($"New position: {rectTransform.anchoredPosition}");
                
                // 确保padding为0,保持左对齐
                horizontalLayout.padding.left = 0;
                horizontalLayout.padding.right = 0;
                horizontalLayout.childAlignment = TextAnchor.UpperLeft;
                
                // 再次强制刷新布局
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                
                PotatoPlugin.Log.LogInfo("Tab bar layout adjusted successfully!");
            }
            catch (System.Exception e)
            {
                PotatoPlugin.Log.LogError($"Failed to adjust tab bar layout: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
