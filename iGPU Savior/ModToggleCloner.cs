using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Bulbul;

namespace PotatoOptimization
{
    /// <summary>
    /// 克隆游戏原生的开关按钮 (InteractableUI)
    /// </summary>
    public class ModToggleCloner
    {
        /// <summary>
        /// 克隆游戏的开关按钮组（激活 + 未激活）
        /// </summary>
        /// <param name="settingUITransform">SettingUI 的 Transform</param>
        /// <param name="label">设置项的标签文本</param>
        /// <param name="isOn">默认是否开启</param>
        /// <param name="onValueChanged">值改变时的回调</param>
        /// <returns>包含完整开关组的 GameObject</returns>
        public static GameObject CreateToggleGroup(Transform settingUITransform, string label, bool isOn, Action<bool> onValueChanged)
        {
            try
            {
                // 1. 找到游戏原版的垂直同步开关作为模板
                Transform verticalSyncRow = settingUITransform.Find("Graphics/ScrollView/Viewport/Content/VerticalSync");
                
                if (verticalSyncRow == null)
                {
                    PotatoPlugin.Log.LogError("未找到 VerticalSync 模板行");
                    return null;
                }

                // 2. 克隆整行
                GameObject toggleRow = UnityEngine.Object.Instantiate(verticalSyncRow.gameObject);
                toggleRow.name = $"ModToggle_{label}";
                toggleRow.SetActive(false);

                // 3. 修改标题文本
                TMP_Text titleText = toggleRow.transform.Find("Title")?.GetComponent<TMP_Text>();
                if (titleText == null)
                {
                    titleText = toggleRow.GetComponentInChildren<TMP_Text>();
                }
                
                if (titleText != null)
                {
                    titleText.text = label;
                    PotatoPlugin.Log.LogInfo($"设置开关标题: {label}");
                }

                // 4. 找到"打开"和"关闭"按钮
                Transform activateBtn = FindButtonByPattern(toggleRow.transform, "Activate", "On", "打开");
                Transform deactivateBtn = FindButtonByPattern(toggleRow.transform, "Deactivate", "Off", "关闭");

                if (activateBtn == null || deactivateBtn == null)
                {
                    PotatoPlugin.Log.LogError($"未找到开关按钮：Activate={activateBtn != null}, Deactivate={deactivateBtn != null}");
                    UnityEngine.Object.Destroy(toggleRow);
                    return null;
                }

                // 5. 获取 InteractableUI 组件
                InteractableUI activateUI = activateBtn.GetComponent<InteractableUI>();
                InteractableUI deactivateUI = deactivateBtn.GetComponent<InteractableUI>();

                if (activateUI == null || deactivateUI == null)
                {
                    PotatoPlugin.Log.LogError("InteractableUI 组件缺失");
                    UnityEngine.Object.Destroy(toggleRow);
                    return null;
                }

                // 6. 清除原有的点击事件
                Button activateButton = activateBtn.GetComponent<Button>();
                Button deactivateButton = deactivateBtn.GetComponent<Button>();
                
                if (activateButton != null) activateButton.onClick.RemoveAllListeners();
                if (deactivateButton != null) deactivateButton.onClick.RemoveAllListeners();

                // 7. 绑定新的点击事件
                activateButton?.onClick.AddListener(() => {
                    onValueChanged?.Invoke(true);
                    activateUI.ActivateUseUI(false);
                    deactivateUI.DeactivateUseUI(false);
                    PlayClickSound();
                    PotatoPlugin.Log.LogInfo($"{label}: ON");
                });

                deactivateButton?.onClick.AddListener(() => {
                    onValueChanged?.Invoke(false);
                    activateUI.DeactivateUseUI(false);
                    deactivateUI.ActivateUseUI(false);
                    PlayClickSound();
                    PotatoPlugin.Log.LogInfo($"{label}: OFF");
                });

                // 8. 设置初始状态
                if (isOn)
                {
                    activateUI.ActivateUseUI(false);
                    deactivateUI.DeactivateUseUI(false);
                }
                else
                {
                    activateUI.DeactivateUseUI(false);
                    deactivateUI.ActivateUseUI(false);
                }

                PotatoPlugin.Log.LogInfo($"成功创建开关组: {label}");
                return toggleRow;
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"创建开关组失败: {e}");
                return null;
            }
        }

        /// <summary>
        /// 根据名称模糊匹配查找按钮
        /// </summary>
        private static Transform FindButtonByPattern(Transform parent, params string[] keywords)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                string nameLower = child.name.ToLower();
                foreach (string keyword in keywords)
                {
                    if (nameLower.Contains(keyword.ToLower()))
                    {
                        return child;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 播放点击音效（尝试调用游戏的音效服务）
        /// </summary>
        private static void PlayClickSound()
        {
            try
            {
                var settingUI = UnityEngine.Object.FindObjectOfType<Bulbul.SettingUI>();
                if (settingUI != null)
                {
                    var seServiceField = typeof(Bulbul.SettingUI).GetField("_systemSeService", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (seServiceField != null)
                    {
                        var seService = seServiceField.GetValue(settingUI);
                        var playClickMethod = seService.GetType().GetMethod("PlayClick");
                        playClickMethod?.Invoke(seService, null);
                    }
                }
            }
            catch
            {
                // 静默失败，不影响主要功能
            }
        }
    }
}
