using System;
using System.Text;

namespace CheatEngineAdditions.Utils
{
    public static class CheatEngineUtil
    {
        public static bool IsWindowFocused(string name)
        {
            IntPtr foreground = Imports.GetForegroundWindow();
            bool isFocused = false;
            Imports.EnumWindows((hWnd, lParam) =>
            {
                if (!Imports.IsWindowVisible(hWnd)) return true;

                StringBuilder stringBuilder = new StringBuilder(256);
                Imports.GetWindowText(hWnd, stringBuilder, stringBuilder.Capacity);
                if (stringBuilder.ToString().Contains(name) && hWnd == foreground)
                {
                    isFocused = true;
                    return false; 
                }
                return true; 
            }, IntPtr.Zero);
            return isFocused;
        }
    }
}
