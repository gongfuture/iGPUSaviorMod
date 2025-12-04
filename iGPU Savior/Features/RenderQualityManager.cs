using System;
using UnityEngine;
using UnityEngine.Rendering;
using PotatoOptimization.Core;

namespace PotatoOptimization.Features
{
    /// <summary>
    /// 渲染质量管理器 - 负责土豆模式和渲染质量控制
    /// </summary>
    public class RenderQualityManager
    {
        private bool _isPotatoMode = false;
        private float _lastRunTime = 0f;
        
        public bool IsPotatoMode => _isPotatoMode;

        /// <summary>
        /// 切换土豆模式
        /// </summary>
        public void TogglePotatoMode()
        {
            _isPotatoMode = !_isPotatoMode;
            
            if (_isPotatoMode)
            {
                ApplyPotatoMode(true);
                PotatoPlugin.Log.LogWarning(">>> 土豆模式: ON <<<");
            }
            else
            {
                RestoreQuality();
                PotatoPlugin.Log.LogWarning(">>> 土豆模式: OFF <<<");
            }
        }

        /// <summary>
        /// 定期刷新土豆模式设置 (某些设置可能被游戏覆盖)
        /// </summary>
        public void UpdatePotatoMode()
        {
            if (!_isPotatoMode) return;

            if (Time.realtimeSinceStartup - _lastRunTime > Constants.DefaultRunInterval)
            {
                ApplyPotatoMode(false);
                _lastRunTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// 应用土豆模式设置
        /// </summary>
        private void ApplyPotatoMode(bool showLog)
        {
            try
            {
                // 设置目标帧率
                Application.targetFrameRate = Constants.PotatoModeTargetFPS;
                QualitySettings.vSyncCount = 0;

                // 获取渲染管线
                var pipeline = GraphicsSettings.currentRenderPipeline;
                if (pipeline != null)
                {
                    Type type = pipeline.GetType();
                    SetProperty(type, pipeline, "renderScale", Constants.PotatoModeRenderScale);
                    SetProperty(type, pipeline, "shadowDistance", Constants.PotatoModeShadowDistance);
                    SetProperty(type, pipeline, "msaaSampleCount", 1);
                }

                // 禁用所有体积效果
                var allComponents = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                if (allComponents != null)
                {
                    foreach (var comp in allComponents)
                    {
                        if (comp != null && comp.enabled && comp.GetType().Name.Contains("Volume"))
                        {
                            comp.enabled = false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (showLog) 
                    PotatoPlugin.Log.LogError($"应用土豆模式失败: {e.Message}");
            }
        }

        /// <summary>
        /// 恢复正常画质
        /// </summary>
        private void RestoreQuality()
        {
            try
            {
                Application.targetFrameRate = Constants.NormalModeTargetFPS;
                
                var pipeline = GraphicsSettings.currentRenderPipeline;
                if (pipeline != null)
                {
                    SetProperty(pipeline.GetType(), pipeline, "renderScale", Constants.NormalRenderScale);
                    SetProperty(pipeline.GetType(), pipeline, "shadowDistance", Constants.NormalShadowDistance);
                }
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"恢复画质失败: {e.Message}");
            }
        }

        /// <summary>
        /// 通过反射设置属性
        /// </summary>
        private void SetProperty(Type type, object obj, string propName, object value)
        {
            try
            {
                var prop = type.GetProperty(propName);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(obj, value, null);
                }
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogWarning($"设置属性失败 [{propName}]: {e.Message}");
            }
        }
    }
}
