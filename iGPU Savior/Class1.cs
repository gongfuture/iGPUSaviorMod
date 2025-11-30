using BepInEx;
using BepInEx.Configuration;
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
    // ==========================================
    // 1. 定义枚举
    // ==========================================
    public enum WindowScaleRatio
    {
        OneThird = 3,   // 1/3
        OneFourth = 4,  // 1/4
        OneFifth = 5    // 1/5
    }

    public enum DragMode
    {
        Ctrl_LeftClick, // Ctrl + 左键
        Alt_LeftClick,  // Alt + 左键
        RightClick_Hold // 右键按住
    }

    // ==========================================
    // 2. 插件入口
    // ==========================================
    [BepInPlugin("chillwithyou.potatomode", "Potato Mode Optimization", "1.5.0")]
    public class PotatoPlugin : BaseUnityPlugin
    {
        public static PotatoPlugin Instance;
        public static ManualLogSource Log;

        public static ConfigEntry<KeyCode> KeyPotatoMode;
        public static ConfigEntry<KeyCode> KeyPiPMode;
        public static ConfigEntry<WindowScaleRatio> CfgWindowScale;
        public static ConfigEntry<DragMode> CfgDragMode;

        private GameObject runnerObject;

        void Awake()
        {
            Instance = this;
            Log = Logger;

            InitConfig();
            Log.LogWarning(">>> [V1.5] 插件启动：循环逻辑已修正 (原始形态 <-> 小窗) <<<");

            runnerObject = new GameObject("PotatoRunner");
            DontDestroyOnLoad(runnerObject);
            runnerObject.hideFlags = HideFlags.HideAndDontSave;
            runnerObject.AddComponent<PotatoController>();
        }

        private void InitConfig()
        {
            KeyPotatoMode = Config.Bind("Hotkeys", "PotatoModeKey", KeyCode.F2, "切换土豆模式的按键");
            KeyPiPMode = Config.Bind("Hotkeys", "PiPModeKey", KeyCode.F3, "切换画中画小窗的按键");
            CfgWindowScale = Config.Bind("Window", "ScaleRatio", WindowScaleRatio.OneThird, "小窗缩放比例");
            CfgDragMode = Config.Bind("Window", "DragMethod", DragMode.Ctrl_LeftClick, "拖动方式");
        }
    }

    // ==========================================
    // 3. 逻辑控制器
    // ==========================================
    public class PotatoController : MonoBehaviour
    {
        private bool isPotatoMode = true;
        private bool isSmallWindow = false;

        private float targetRenderScale = 0.4f;
        private int currentTargetWidth;
        private int currentTargetHeight;

        private float lastRunTime = 0f;
        private float runInterval = 3.0f;

        // === ✨ 新增：用于记录“原始形态”的变量 ===
        private int origWidth;
        private int origHeight;
        private FullScreenMode origMode;

        // Windows API
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll")] private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        // Constants
        private const int GWL_STYLE = -16;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_THICKFRAME = 0x00040000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOZORDER = 0x0004;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // Manual drag variables
        private Vector2 dragStartMousePos;
        private Vector2 dragStartWindowPos;
        private bool isRightDragging = false;

        void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (isPotatoMode) ApplyPotatoMode(false);

            // 初始化一下原始分辨率，防止还没切小窗就报错（虽然逻辑上不会）
            origWidth = Screen.width;
            origHeight = Screen.height;
            origMode = Screen.fullScreenMode;
        }

        void Update()
        {
            if (Input.GetKeyDown(PotatoPlugin.KeyPotatoMode.Value))
            {
                isPotatoMode = !isPotatoMode;
                if (isPotatoMode) { ApplyPotatoMode(true); PotatoPlugin.Log.LogWarning(">>> 土豆模式: ON <<<"); }
                else { RestoreQuality(); PotatoPlugin.Log.LogWarning(">>> 土豆模式: OFF <<<"); }
            }

            if (Input.GetKeyDown(PotatoPlugin.KeyPiPMode.Value))
            {
                ToggleWindowMode();
            }

            if (isSmallWindow)
            {
                HandleDragLogic();
            }

            if (isPotatoMode)
            {
                if (Time.realtimeSinceStartup - lastRunTime > runInterval)
                {
                    ApplyPotatoMode(false);
                    lastRunTime = Time.realtimeSinceStartup;
                }
            }
        }

        // === ✨ 核心修改：切换逻辑 ✨ ===
        private void ToggleWindowMode()
        {
            // 如果当前不是小窗，说明准备进入小窗
            if (!isSmallWindow)
            {
                // 1. 存档：记下变身前的样子
                origWidth = Screen.width;
                origHeight = Screen.height;
                origMode = Screen.fullScreenMode;

                // 2. 变身：进入小窗
                isSmallWindow = true;
                CalculateTargetResolution();
                Screen.SetResolution(currentTargetWidth, currentTargetHeight, FullScreenMode.Windowed);
                StartCoroutine(SetPiPMode(true));

                PotatoPlugin.Log.LogWarning($">>> 开启画中画 (已保存原始分辨率: {origWidth}x{origHeight}) <<<");
            }
            else
            {
                // 1. 读档：准备恢复原始形态
                isSmallWindow = false;

                // 2. 还原：直接设回原来的分辨率和模式
                Screen.SetResolution(origWidth, origHeight, origMode);
                StartCoroutine(SetPiPMode(false));

                PotatoPlugin.Log.LogWarning(">>> 恢复原始状态 <<<");
            }
        }

        private void CalculateTargetResolution()
        {
            Resolution screenRes = Screen.currentResolution;
            int divisor = (int)PotatoPlugin.CfgWindowScale.Value;
            currentTargetWidth = screenRes.width / divisor;
            currentTargetHeight = screenRes.height / divisor;
        }

        private IEnumerator SetPiPMode(bool enable)
        {
            yield return null; yield return null;
            try
            {
                IntPtr hWnd = GetActiveWindow();
                uint style = GetWindowLong(hWnd, GWL_STYLE);
                if (enable)
                {
                    style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU);
                    SetWindowLong32(hWnd, GWL_STYLE, style);
                    SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                }
                else
                {
                    // 恢复时，只要取消 TopMost 即可
                    // 窗口样式（标题栏等）会因为 Screen.SetResolution 切换模式而由 Unity 自动重置
                    SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                }
            }
            catch (Exception e) { PotatoPlugin.Log.LogError($"窗口样式失败: {e.Message}"); }
        }

        private void HandleDragLogic()
        {
            DragMode mode = PotatoPlugin.CfgDragMode.Value;

            if (mode == DragMode.Ctrl_LeftClick || mode == DragMode.Alt_LeftClick)
            {
                bool modifierPressed = (mode == DragMode.Ctrl_LeftClick) ?
                    (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) :
                    (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));

                if (Input.GetMouseButtonDown(0) && modifierPressed) DoApiDrag();
            }
            else if (mode == DragMode.RightClick_Hold)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    isRightDragging = true;
                    dragStartMousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    IntPtr hWnd = GetActiveWindow();
                    GetWindowRect(hWnd, out RECT rect);
                    dragStartWindowPos = new Vector2(rect.Left, rect.Top);
                }
                if (Input.GetMouseButtonUp(1)) isRightDragging = false;

                if (isRightDragging)
                {
                    Vector2 currentMouse = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    Vector2 delta = currentMouse - dragStartMousePos;
                    int newX = (int)(dragStartWindowPos.x + delta.x);
                    int newY = (int)(dragStartWindowPos.y + delta.y);
                    SetWindowPos(GetActiveWindow(), IntPtr.Zero, newX, newY, 0, 0, SWP_NOSIZE | SWP_NOZORDER);
                }
            }
        }

        private void DoApiDrag()
        {
            try { ReleaseCapture(); SendMessage(GetActiveWindow(), WM_NCLBUTTONDOWN, HT_CAPTION, 0); } catch { }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (isPotatoMode) ApplyPotatoMode(false); }

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
                var allComponents = FindObjectsOfType<MonoBehaviour>();
                if (allComponents != null)
                {
                    foreach (var comp in allComponents)
                    {
                        if (comp != null && comp.enabled && comp.GetType().Name.Contains("Volume"))
                            comp.enabled = false;
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