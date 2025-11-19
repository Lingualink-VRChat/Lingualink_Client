using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace lingualink_client.Services
{
    /// <summary>
    /// 使用 Win32 API 的剪贴板辅助类，用于在某些情况下绕过
    /// WPF/WinForms Clipboard 在第三方软件占用剪贴板时抛出的 ExternalException。
    /// </summary>
    public static class ClipboardHelper
    {
        private const uint CfUnicodeText = 13;
        private const uint GmemMoveable = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        /// <summary>
        /// 尝试将文本写入系统剪贴板，内部使用 Win32 API 并带有限次数重试，
        /// 以减小被其他进程（如远程桌面工具）短暂占用时的失败概率。
        /// </summary>
        public static bool TrySetText(string text, int maxRetries = 5, int retryDelayMilliseconds = 20)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    Thread.Sleep(retryDelayMilliseconds);
                    continue;
                }

                try
                {
                    if (!EmptyClipboard())
                    {
                        // 如果清空剪贴板失败，直接放弃当前尝试
                        return false;
                    }

                    var bytes = (text.Length + 1) * 2; // Unicode 每个字符 2 字节，包含结尾的 '\0'
                    var hGlobal = GlobalAlloc(GmemMoveable, (UIntPtr)bytes);
                    if (hGlobal == IntPtr.Zero)
                    {
                        return false;
                    }

                    var target = GlobalLock(hGlobal);
                    if (target == IntPtr.Zero)
                    {
                        GlobalFree(hGlobal);
                        return false;
                    }

                    try
                    {
                        var chars = text.ToCharArray();
                        Marshal.Copy(chars, 0, target, chars.Length);
                        // 写入终止符
                        Marshal.WriteInt16(target, chars.Length * 2, 0);
                    }
                    finally
                    {
                        GlobalUnlock(hGlobal);
                    }

                    if (SetClipboardData(CfUnicodeText, hGlobal) == IntPtr.Zero)
                    {
                        // 如果设置失败，由我们负责释放内存
                        GlobalFree(hGlobal);
                        return false;
                    }

                    // 成功后由系统接管 hGlobal 的生命周期
                    return true;
                }
                finally
                {
                    CloseClipboard();
                }
            }

            return false;
        }
    }
}

