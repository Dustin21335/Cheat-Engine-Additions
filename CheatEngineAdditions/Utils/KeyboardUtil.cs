using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CheatEngineAdditions.Utils
{
    public static class KeyboardUtil
    {
        static KeyboardUtil()
        {
            Timer keybindUpdateTimer = new Timer();
            keybindUpdateTimer.Interval = 100;
            keybindUpdateTimer.Tick += (s, e) =>
            {
                keybinds.Values.ToList().ForEach(kb =>
                {
                    if (kb.Keys.All(IsKeyDown))
                    {
                        string? focusedTabCondition = kb.FoscusedTabCondition ?? "";
                        if (!kb.AlreadyTriggered && (string.IsNullOrEmpty(focusedTabCondition) || CheatEngineUtil.IsWindowFocused(focusedTabCondition)))
                        {
                            if (kb.Callback != null) kb.Callback();
                            kb.AlreadyTriggered = true; 
                        }
                    }
                    else kb.AlreadyTriggered = false;
                });       
            };
            keybindUpdateTimer.Start();
        }

        private static Dictionary<string, Keybind> keybinds = new Dictionary<string, Keybind>();

        public static bool IsKeyDown(int key)
        {
            return (Imports.GetAsyncKeyState(key) & 0x8000) != 0;
        }

        public static void RegisterKeybind(string name, List<int> keys, Action callback, string? foscusedTabCondition = null)
        {
            keybinds[name] = new Keybind
            {
                Keys = keys,
                FoscusedTabCondition = foscusedTabCondition,
                Callback = callback,
            };
        }

        public static void UnregisterKeybind(string name)
        {
            if (keybinds.ContainsKey(name)) keybinds.Remove(name);
        }

        private class Keybind
        {
            public List<int>? Keys;
            public string? FoscusedTabCondition;
            public Action? Callback = null;
            public bool AlreadyTriggered;
        }
    }
}
