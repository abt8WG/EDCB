using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace EpgTimer
{
    public class InfoWindowBase : RestorableWindow
    {
        #region P/Invoke用の定義

        public class IntPtrObj
        {
            public IntPtr Value = IntPtr.Zero;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // 使用する部分だけを抜粋
        enum WindowLongFlags : int
        {
            GWL_STYLE = -16,
        }

        // 使用する部分だけを抜粋
        [Flags]
        enum WindowStyles : uint
        {
            WS_POPUP = 0x80000000,
            WS_CHILD = 0x40000000,
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, UIntPtr dwNewLong)
        {
            if (UIntPtr.Size == 8)
            {
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            }
            else
            {
                return SetWindowLong32(hWnd, nIndex, dwNewLong);
            }
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, UIntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, UIntPtr dwNewLong);

        public static UIntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (UIntPtr.Size == 8)
            {
                return GetWindowLongPtr64(hWnd, nIndex);
            }
            else
            {
                return GetWindowLong32(hWnd, nIndex);
            }
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern UIntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern UIntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        #endregion

        private static string GetWindowClassName(IntPtr hWnd)
        {
            StringBuilder classNameBuilder = new StringBuilder(256);

            if (GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity) == 0)
            {
                return "";
            }

            return classNameBuilder.ToString();
        }

        private static bool IsDesktopWindow(IntPtr hWnd)
        {
            string className = GetWindowClassName(hWnd);

            if ((string.Compare(className, "Progman", true) != 0) &&
                (string.Compare(className, "WorkerW", true) != 0))
            {
                return false;
            }

            IntPtr hWndDefView = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);

            return (hWndDefView != IntPtr.Zero);
        }

        /// <summary>
        /// EnumWindowsのハンドラ。
        /// hWndがDesktopウィンドウならlParamで渡されたIntPtrObjのValueにDesktopウィンドウのハンドルを代入する。
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private static bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
        {
            if (!IsDesktopWindow(hWnd))
            {
                return true;
            }

            GCHandle hhWndDesktop = GCHandle.FromIntPtr(lParam);
            IntPtrObj hWndDesktop = (IntPtrObj)hhWndDesktop.Target;

            hWndDesktop.Value = hWnd;

            return false;
        }

        /// <summary>
        /// Desktopウィンドウのハンドルを返す。
        /// </summary>
        /// <returns>Desktopウィンドウのハンドル。見つからない場合はIntPtr.Zeroを返す。</returns>
        static private IntPtr FindDesktopWindow()
        {
            IntPtrObj hWndDesktop = new IntPtrObj();
            GCHandle hhWndDesktop = GCHandle.Alloc(hWndDesktop);

            // hWndDesktop.ValueにDesktopウィンドウのハンドルが返る
            EnumWindows(new EnumWindowsProc(EnumWindowsCallback), (IntPtr)hhWndDesktop);

            hhWndDesktop.Free();

            return hWndDesktop.Value;
        }

        private static void AdjustWindowPosition(ref WINDOWPLACEMENT wp, int offsetX, int offsetY)
        {
            wp.normalPosition.Left += offsetX;
            wp.normalPosition.Top += offsetY;
            wp.normalPosition.Right += offsetX;
            wp.normalPosition.Bottom += offsetY;
        }

        private void SetWindowBottommost(bool value)
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            IntPtr hWndDesktop = FindDesktopWindow();
            if (hWndDesktop == IntPtr.Zero) return;

            RECT desktopRect = new RECT();
            WINDOWPLACEMENT wp = new WINDOWPLACEMENT();

            if (GetWindowRect(hWndDesktop, out desktopRect) == false) return;
            if (NativeMethods.GetWindowPlacement(hWnd, out wp) == false) return;

            Debug.Write("DesktopRect=(" +
                desktopRect.Left + ", " + desktopRect.Top + ", " +
                desktopRect.Right + ", " + desktopRect.Bottom + "), ");
            Debug.Write("[Before]WindowRect=(" +
                wp.normalPosition.Left + ", " + wp.normalPosition.Top + ", " +
                wp.normalPosition.Right + ", " + wp.normalPosition.Bottom + "), ");

            if (value)
            {
                // WS_CHILDを追加
                var wl = GetWindowLongPtr(hWnd, (int)WindowLongFlags.GWL_STYLE);
                wl = (UIntPtr)(wl.ToUInt32() | (uint)WindowStyles.WS_CHILD);
                SetWindowLongPtr(hWnd, (int)WindowLongFlags.GWL_STYLE, wl);
                // Desktopウィンドウの子ウィンドウにして最背面に配置する
                SetParent(hWnd, hWndDesktop);
                // ウィンドウの位置を調整
                AdjustWindowPosition(ref wp, -desktopRect.Left, -desktopRect.Top);
            }
            else
            {
                // Desktopウィンドウから切り離す
                SetParent(hWnd, IntPtr.Zero);
                // WS_CHILDを削除
                var wl = GetWindowLongPtr(hWnd, (int)WindowLongFlags.GWL_STYLE);
                wl = (UIntPtr)(wl.ToUInt32() & ~(uint)WindowStyles.WS_CHILD);
                SetWindowLongPtr(hWnd, (int)WindowLongFlags.GWL_STYLE, wl);
                // ウィンドウの位置を調整
                AdjustWindowPosition(ref wp, desktopRect.Left, desktopRect.Top);
            }

            NativeMethods.SetWindowPlacement(hWnd, ref wp);

            Debug.Write("[After]WindowRect=(" +
                wp.normalPosition.Left + ", " + wp.normalPosition.Top + ", " +
                wp.normalPosition.Right + ", " + wp.normalPosition.Bottom + ")\r\n");
        }

        #region Bottommost プロパティ

        public static readonly DependencyProperty BottommostProperty =
            DependencyProperty.Register(
                "Bottommost",
                typeof(bool),
                typeof(InfoWindowBase),
                new FrameworkPropertyMetadata(
                    false,
                    new PropertyChangedCallback(OnBottommostChanged)));

        private static void OnBottommostChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            InfoWindowBase window = o as InfoWindowBase;
            if (window != null)
            {
                bool value = (bool)e.NewValue;

                window.SetWindowBottommost(value);
            }
        }

        /// <summary>
        /// ウィンドウが最下位zオーダーで表示されるかどうかを示す値を取得または設定します。
        /// </summary>
        /// <remarks>Topmostプロパティを変更することはないためTopmostは別途設定する必要があります。</remarks>
        public bool Bottommost
        {
            get { return (bool)GetValue(BottommostProperty); }
            set { SetValue(BottommostProperty, value); }
        }

        #endregion
    }
}
