using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using PotatoOptimization.Features;

namespace PotatoOptimization.Patches
{
    /// <summary>
    /// Harmony 补丁：拦截鼠标输入并在镜像模式下翻转坐标
    /// </summary>
    [HarmonyPatch(typeof(Input), "mousePosition", MethodType.Getter)]
    public class InputMousePositionPatch
    {
        /// <summary>
        /// 镜像模式是否启用 (由 CameraMirrorManager 控制)
        /// </summary>
        public static bool IsInputMirrored { get; set; } = false;

        static void Postfix(ref Vector3 __result)
        {
            // 仅在镜像模式启用时处理
            if (!IsInputMirrored)
                return;

            // 检测鼠标下是否有 UI 元素
            if (IsPointerOverUI(__result))
            {
                // 鼠标在 UI 上，不翻转（UI 本身没有镜像）
                return;
            }

            // 鼠标在 3D 场景上，翻转坐标
            __result = new Vector3(
                Screen.width - __result.x,  // 水平翻转
                __result.y,                   // Y 保持不变
                __result.z                    // Z 保持不变
            );
        }

        /// <summary>
        /// 检测鼠标位置是否在 UI 元素上
        /// </summary>
        private static bool IsPointerOverUI(Vector3 mousePosition)
        {
            // 使用 EventSystem 检测 UI
            if (EventSystem.current != null)
            {
                PointerEventData eventData = new PointerEventData(EventSystem.current)
                {
                    position = new Vector2(mousePosition.x, mousePosition.y)
                };

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);

                // 过滤掉镜像 Canvas（MirrorCanvas）
                foreach (var result in results)
                {
                    if (result.gameObject != null && 
                        result.gameObject.name != "MirrorDisplay" && 
                        result.gameObject.GetComponentInParent<Canvas>() != null &&
                        result.gameObject.GetComponentInParent<Canvas>().name != "MirrorCanvas")
                    {
                        return true; // 检测到游戏 UI
                    }
                }
            }

            return false; // 没有 UI，是 3D 场景
        }
    }
}
