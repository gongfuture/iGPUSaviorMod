using System;
using System.Runtime.InteropServices;
using PotatoOptimization.Core;

namespace PotatoOptimization.Utilities
{
    /// <summary>
    /// Windows 窗口管理工具类 - 封装所有 Win32 API 调用
    /// </summary>
    public static class WindowManager
    {
        // ==================== Win32 API 声明 ====================
        [DllImport("user32.dll")] 
        private static extern IntPtr GetActiveWindow();
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] 
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] 
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] 
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] 
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")] 
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")] 
        private static extern bool ReleaseCapture();
        
        [DllImport("user32.dll")] 
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        
        [DllImport("user32.dll")] 
        [return: MarshalAs(UnmanagedType.Bool)] 
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("user32.dll")] 
        [return: MarshalAs(UnmanagedType.Bool)] 
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // ==================== 结构体定义 ====================
        [StructLayout(LayoutKind.Sequential)] 
        public struct POINT 
        { 
            public int X; 
            public int Y; 
        }

        [StructLayout(LayoutKind.Sequential)] 
        public struct RECT 
        { 
            public int Left; 
            public int Top; 
            public int Right; 
            public int Bottom; 
        }

        // ==================== 常量 ====================
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        // ==================== 公共方法 ====================
        
        /// <summary>
        /// 获取当前活动窗口句柄
        /// </summary>
        public static IntPtr GetCurrentWindowHandle()
        {
            return GetActiveWindow();
        }

        /// <summary>
        /// 设置窗口样式 (兼容 32/64 位)
        /// </summary>
        public static IntPtr SetWindowStyle(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) 
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else 
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        /// <summary>
        /// 获取窗口样式 (兼容 32/64 位)
        /// </summary>
        public static IntPtr GetWindowStyle(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8) 
                return GetWindowLongPtr64(hWnd, nIndex);
            else 
                return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        /// <summary>
        /// 移除窗口边框和标题栏
        /// </summary>
        public static void RemoveWindowBorder(IntPtr hWnd)
        {
            IntPtr stylePtr = GetWindowStyle(hWnd, Constants.GWL_STYLE);
            long style = stylePtr.ToInt64();
            style &= ~((long)Constants.WS_CAPTION | Constants.WS_THICKFRAME | Constants.WS_SYSMENU);
            
            SetWindowStyle(hWnd, Constants.GWL_STYLE, new IntPtr(style));
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, 
                Constants.SWP_NOMOVE | Constants.SWP_NOSIZE | Constants.SWP_FRAMECHANGED | Constants.SWP_SHOWWINDOW);
        }

        /// <summary>
        /// 恢复窗口样式
        /// </summary>
        public static void RestoreWindowStyle(IntPtr hWnd, IntPtr originalStyle)
        {
            SetWindowStyle(hWnd, Constants.GWL_STYLE, originalStyle);
            SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, 
                Constants.SWP_NOMOVE | Constants.SWP_NOSIZE | Constants.SWP_FRAMECHANGED | Constants.SWP_SHOWWINDOW);
        }

        /// <summary>
        /// 移动窗口
        /// </summary>
        public static bool MoveWindow(IntPtr hWnd, int x, int y)
        {
            return SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, Constants.SWP_NOSIZE | Constants.SWP_NOZORDER);
        }

        /// <summary>
        /// 开始拖动窗口 (系统级)
        /// </summary>
        public static void StartSystemDrag()
        {
            try
            {
                ReleaseCapture();
                SendMessage(GetActiveWindow(), Constants.WM_NCLBUTTONDOWN, Constants.HT_CAPTION, 0);
            }
            catch (Exception e)
            {
                PotatoPlugin.Log.LogError($"拖动窗口失败: {e.Message}");
            }
        }

        /// <summary>
        /// 获取鼠标屏幕坐标
        /// </summary>
        public static bool GetCursorScreenPosition(out POINT point)
        {
            return GetCursorPos(out point);
        }

        /// <summary>
        /// 获取窗口矩形区域
        /// </summary>
        public static bool GetWindowBounds(IntPtr hWnd, out RECT rect)
        {
            return GetWindowRect(hWnd, out rect);
        }
    }
}
