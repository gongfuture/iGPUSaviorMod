using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using PotatoOptimization.Core;

namespace PotatoOptimization.UI
{
  public class ModInputFieldCloner
  {
    public static GameObject CreateInputField(Transform settingRoot, string labelText, string initialValue, Action<string> onValueChanged)
{
    try
    {
        if (settingRoot == null) return null;

        // 1. 寻找模板 - 现在找的是父物体 FrameRate
        Transform graphicsContent = settingRoot.Find("Graphics/ScrollView/Viewport/Content");
        if (graphicsContent == null) return null;

        // 🆕 改找父物体
        Transform templateObj = graphicsContent.Find("FrameRate");
        if (templateObj == null)
        {
            // 如果找不到，尝试找包含 FrameRate 的
            foreach(Transform child in graphicsContent) {
                if(child.name.Contains("FrameRate") && !child.name.Contains("Deactive") && !child.name.Contains("Active")) 
                { 
                    templateObj = child; 
                    break; 
                }
            }
        }

        if (templateObj == null)
        {
            PotatoPlugin.Log.LogError("[Input] Template 'FrameRate' not found!");
            return null;
        }

        PotatoPlugin.Log.LogInfo($"[Input] Found template: {templateObj.name} (Cloning...)");

        // 2. 克隆父物体
        GameObject clone = UnityEngine.Object.Instantiate(templateObj.gameObject);
        clone.name = $"ModInput_{labelText}";
        clone.SetActive(false);

        // === 🆕 删除 DeactiveFrameRate 子物体 ===
        Transform deactiveInput = clone.transform.Find("DeactiveFrameRate");
        if (deactiveInput != null)
        {
            PotatoPlugin.Log.LogInfo("[Input] 🔪 Removing DeactiveFrameRate input");
            UnityEngine.Object.DestroyImmediate(deactiveInput.gameObject);
        }
        else
        {
            PotatoPlugin.Log.LogWarning("[Input] ⚠️ DeactiveFrameRate not found in clone!");
        }

        // === 3. 核弹级清理 + 删除多余的 TitleText ===
        var allComponents = clone.GetComponentsInChildren<MonoBehaviour>(true).ToList();

        int removedCount = 0;
        foreach (var comp in allComponents)
        {
            if (comp == null) continue;

            Type type = comp.GetType();
            string ns = type.Namespace ?? "";
            
            bool isSafe = 
                ns.StartsWith("UnityEngine.UI") ||
                ns.Contains("TMPro") ||
                type == typeof(LayoutElement) ||
                type == typeof(CanvasGroup) ||
                type == typeof(CanvasRenderer);

            if (!isSafe)
            {
                PotatoPlugin.Log.LogWarning($"[Input] 🔪 Killing logic script: {type.Name} on {comp.gameObject.name}");
                UnityEngine.Object.DestroyImmediate(comp);
                removedCount++;
            }
        }

        // 🆕 删除父物体的 TitleText（那个显示"帧率"的）
        Transform parentTitleText = clone.transform.Find("TitleText");
        if (parentTitleText != null)
        {
            PotatoPlugin.Log.LogInfo("[Input] 🔪 Removing parent TitleText (帧率)");
            UnityEngine.Object.DestroyImmediate(parentTitleText.gameObject);
        }

        PotatoPlugin.Log.LogInfo($"[Input] Cleanup complete. Removed {removedCount} logic scripts.");

        // 4. 修改标题 - 优先找 ActiveFrameRate 下的标题
        Transform activeFrame = clone.transform.Find("ActiveFrameRate");
        TMP_Text titleText = null;

        if (activeFrame != null)
        {
            // 在 ActiveFrameRate 下找标题
            titleText = activeFrame.Find("TitleText")?.GetComponent<TMP_Text>();
            if (titleText == null)
            {
                // 如果没找到，就在 ActiveFrameRate 下找第一个 TMP_Text
                titleText = activeFrame.GetComponentInChildren<TMP_Text>();
            }
        }

        // 如果还是没找到，再找父物体的
        if (titleText == null)
        {
            titleText = clone.transform.Find("TitleText")?.GetComponent<TMP_Text>();
        }
        if (titleText == null)
        {
            titleText = clone.GetComponentInChildren<TMP_Text>();
        }

        if (titleText != null)
        {
            titleText.text = labelText;
            PotatoPlugin.Log.LogInfo($"[Input] ✅ Set title to: {labelText}");
        }
        else
        {
            PotatoPlugin.Log.LogWarning("[Input] ⚠️ Title text not found!");
        }

        // 5. 改造输入框（现在应该只剩 ActiveFrameRate 里的那个了）
        var inputField = clone.GetComponentInChildren<TMP_InputField>();
        if (inputField != null)
        {
            inputField.contentType = TMP_InputField.ContentType.Standard;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.characterValidation = TMP_InputField.CharacterValidation.None;
            inputField.characterLimit = 0;
            inputField.text = initialValue;

            inputField.onValueChanged.RemoveAllListeners();
            inputField.onEndEdit.RemoveAllListeners();
            inputField.onSubmit.RemoveAllListeners();
            inputField.onSelect.RemoveAllListeners();
            inputField.onDeselect.RemoveAllListeners();

            inputField.onEndEdit.AddListener((val) => 
            {
                PotatoPlugin.Log.LogInfo($"[Input] '{labelText}' saved: {val}");
                onValueChanged?.Invoke(val);
            });
        }
        else
        {
            PotatoPlugin.Log.LogError("[Input] TMP_InputField not found in clone!");
        }

        return clone;
    }
    catch (Exception e)
    {
        PotatoPlugin.Log.LogError($"CreateInputField failed: {e}");
        return null;
    }
}
  }
}