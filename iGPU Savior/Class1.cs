using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PotatoOptimization
{
    // 1. 插件入口：只负责初始化和创建"不死"对象
    [BepInPlugin("com.yourname.potatomode", "Potato Mode Optimization", "1.3.0")]
    public class PotatoPlugin : BaseUnityPlugin
    {
        // 静态实例，方便Controller访问日志和配置
        public static PotatoPlugin Instance;
        
        // 【新增】公开的静态日志对象，让外部类可以访问
        public static ManualLogSource Log;
        
        private GameObject runnerObject;

        void Awake()
        {
            Instance = this;
            
            // 【新增】把插件自带的 Logger 赋值给公开变量
            Log = Logger;
            
            Log.LogWarning(">>> [V1.3] 插件启动：正在初始化 Runner (支持置顶和拖动) <<<");

            // --- 关键修改：模仿天气Mod的架构 ---
            // 创建一个独立的 GameObject
            runnerObject = new GameObject("PotatoRunner");

            // 标记为"切换场景不销毁"，这是活下来的关键！
            DontDestroyOnLoad(runnerObject);
            runnerObject.hideFlags = HideFlags.HideAndDontSave; // 隐藏起来，防止被游戏逻辑误删

            // 挂载逻辑组件
            runnerObject.AddComponent<PotatoController>();

            Log.LogWarning(">>> Runner 创建成功，逻辑已托管给 PotatoController <<<");
        }
    }

    // 2. 逻辑控制器：这才是真正干活的地方，挂在一个"不死"的对象上
    public class PotatoController : MonoBehaviour
    {
        // === 状态 ===
        private bool isPotatoMode = true;
        private bool isSmallWindow = false;

        // === 配置 ===
        private float targetRenderScale = 0.4f;
        private int targetWindowWidth = 640;
        private int targetWindowHeight = 360;
        private float lastRunTime = 0f;
        private float runInterval = 3.0f;

        // ==========================================
        // ✨ Windows API 魔法区域
        // ==========================================
        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        // --- 拖动窗口相关的 API ---
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        // 窗口样式常量
        private const int GWL_STYLE = -16;
        private const uint WS_CAPTION = 0x00C00000;     // 标题栏
        private const uint WS_THICKFRAME = 0x00040000;  // 可拖动边框
        private const uint WS_SYSMENU = 0x00080000;     // 系统菜单
        
        private const uint SWP_FRAMECHANGED = 0x0020;   // 通知系统框架变了
        private const uint SWP_NOSIZE = 0x0001;         // 保持大小
        private const uint SWP_NOMOVE = 0x0002;         // 保持位置
        private const uint SWP_NOZORDER = 0x0004;       // 保持层级
        private const uint SWP_SHOWWINDOW = 0x0040;     // 显示窗口

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);    // 置顶
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);  // 取消置顶

        // 拖动消息
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // ==========================================

        void Start()
        {
            // 监听场景加载
            SceneManager.sceneLoaded += OnSceneLoaded;
            // 初始应用
            if (isPotatoMode) ApplyPotatoMode(false);

            PotatoPlugin.Log.LogInfo("PotatoController [终极版] 已启动：支持置顶与拖动...");
        }

        void Update()
        {
            // --- F2: 切换土豆优化 ---
            if (Input.GetKeyDown(KeyCode.F2))
            {
                isPotatoMode = !isPotatoMode;
                if (isPotatoMode)
                {
                    ApplyPotatoMode(true);
                    PotatoPlugin.Log.LogWarning(">>> [F2] 土豆模式: ON <<<");
                }
                else
                {
                    RestoreQuality();
                    PotatoPlugin.Log.LogWarning(">>> [F2] 土豆模式: OFF <<<");
                }
            }

            // --- F3: 切换画中画模式 ---
            if (Input.GetKeyDown(KeyCode.F3))
            {
                ToggleWindowMode();
            }

            // --- ✨ 拖动逻辑 ✨ ---
            // 只有在小窗模式下，且按住 Alt 键 + 鼠标左键按下时触发
            if (isSmallWindow && Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            {
                DragWindow();
            }

            // --- 自动巡逻 ---
            if (isPotatoMode)
            {
                if (Time.realtimeSinceStartup - lastRunTime > runInterval)
                {
                    ApplyPotatoMode(false); // 静默执行
                    lastRunTime = Time.realtimeSinceStartup;
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (isPotatoMode) ApplyPotatoMode(false);
        }

        // ✨ 修改后的切换窗口逻辑
        private void ToggleWindowMode()
        {
            isSmallWindow = !isSmallWindow;
            if (isSmallWindow)
            {
                // 切到小窗：设置分辨率 -> 启动协程去边框+置顶
                Screen.SetResolution(targetWindowWidth, targetWindowHeight, FullScreenMode.Windowed);
                StartCoroutine(SetPiPMode(true)); // 开启 PiP
                PotatoPlugin.Log.LogWarning($">>> [F3] 开启画中画: 置顶 + 无边框 (按住Alt拖动) <<<");
            }
            else
            {
                // 恢复全屏：设置分辨率 -> 启动协程恢复样式
                Resolution maxRes = Screen.currentResolution;
                Screen.SetResolution(maxRes.width, maxRes.height, FullScreenMode.FullScreenWindow);
                StartCoroutine(SetPiPMode(false)); // 关闭 PiP
                PotatoPlugin.Log.LogWarning(">>> [F3] 恢复全屏模式 <<<");
            }
        }

        // 设置画中画模式 (Picture-in-Picture)
        private IEnumerator SetPiPMode(bool enable)
        {
            yield return null; 
            yield return null; 

            try
            {
                IntPtr hWnd = GetActiveWindow();
                uint style = GetWindowLong(hWnd, GWL_STYLE);

                if (enable)
                {
                    // === 开启：去边框 + 置顶 ===
                    // 1. 去掉标题栏和边框
                    style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU);
                    SetWindowLong(hWnd, GWL_STYLE, style);

                    // 2. 设置置顶 (HWND_TOPMOST)
                    SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                    
                    PotatoPlugin.Log.LogInfo("画中画模式已应用：无边框 + 置顶");
                }
                else
                {
                    // === 关闭：取消置顶 ===
                    SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                    
                    PotatoPlugin.Log.LogInfo("已取消置顶");
                }
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"窗口样式设置失败: {e.Message}");
            }
        }

        // 实现拖动窗口
        private void DragWindow()
        {
            try
            {
                // 1. 释放鼠标捕获，让 Unity 放手
                ReleaseCapture();
                
                // 2. 获取窗口句柄
                IntPtr hWnd = GetActiveWindow();

                // 3. 欺骗 Windows：告诉它用户按在了"标题栏"上 (HT_CAPTION)
                SendMessage(hWnd, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"窗口拖动失败: {e.Message}");
            }
        }

        private void ApplyPotatoMode(bool showLog)
        {
            try
            {
                Application.targetFrameRate = 15;
                QualitySettings.vSyncCount = 0;
                var pipeline = GraphicsSettings.currentRenderPipeline;
                if (pipeline != null)
                {
                    Type type = pipeline.GetType();
                    SetProp(type, pipeline, "renderScale", targetRenderScale);
                    SetProp(type, pipeline, "shadowDistance", 0f);
                    SetProp(type, pipeline, "msaaSampleCount", 1);
                }
                
                // 关闭所有 Volume (光影特效)
                var allComponents = FindObjectsOfType<MonoBehaviour>();
                int count = 0;
                if (allComponents != null)
                {
                    foreach (var comp in allComponents)
                    {
                        if (comp != null && comp.enabled && comp.GetType().Name.Contains("Volume"))
                        {
                            comp.enabled = false;
                            count++;
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void RestoreQuality()
        {
            try
            {
                Application.targetFrameRate = 60;
                var pipeline = GraphicsSettings.currentRenderPipeline;
                if (pipeline != null)
                {
                    SetProp(pipeline.GetType(), pipeline, "renderScale", 1.0f);
                    SetProp(pipeline.GetType(), pipeline, "shadowDistance", 50f);
                }
            }
            catch { }
        }

        private void SetProp(Type type, object obj, string propName, object value)
        {
            try
            {
                PropertyInfo prop = type.GetProperty(propName);
                if (prop != null && prop.CanWrite) prop.SetValue(obj, value, null);
            }
            catch { }
        }
    }
}