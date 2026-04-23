using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Interop;

namespace lingualink_client.Services.Ui
{
    public sealed class HotkeyGesture
    {
        public HotkeyGesture(ModifierKeys modifiers, Key key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        public ModifierKeys Modifiers { get; }

        public Key Key { get; }

        public static bool TryCreate(ModifierKeys modifiers, Key key, out HotkeyGesture? gesture)
        {
            gesture = null;

            if (IsModifierKey(key))
            {
                return false;
            }

            gesture = new HotkeyGesture(NormalizeModifiers(modifiers), key);
            return true;
        }

        public static bool TryParse(string? value, out HotkeyGesture? gesture)
        {
            gesture = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var tokens = value
                .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (tokens.Count == 0)
            {
                return false;
            }

            var modifiers = ModifierKeys.None;
            Key? parsedKey = null;

            foreach (var token in tokens)
            {
                switch (token.ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= ModifierKeys.Control;
                        continue;
                    case "ALT":
                        modifiers |= ModifierKeys.Alt;
                        continue;
                    case "SHIFT":
                        modifiers |= ModifierKeys.Shift;
                        continue;
                    case "WIN":
                    case "WINDOWS":
                        modifiers |= ModifierKeys.Windows;
                        continue;
                }

                if (!TryParseKeyToken(token, out var key))
                {
                    return false;
                }

                parsedKey = key;
            }

            if (!parsedKey.HasValue)
            {
                return false;
            }

            return TryCreate(modifiers, parsedKey.Value, out gesture);
        }

        public string ToConfigString()
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(ModifierKeys.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(ModifierKeys.Shift))
            {
                parts.Add("Shift");
            }

            if (Modifiers.HasFlag(ModifierKeys.Windows))
            {
                parts.Add("Win");
            }

            parts.Add(GetKeyDisplayString(Key));
            return string.Join("+", parts);
        }

        public string ToDisplayString() => ToConfigString();

        public uint GetNativeModifiers()
        {
            uint modifiers = 0;
            if (Modifiers.HasFlag(ModifierKeys.Alt))
            {
                modifiers |= 0x0001;
            }

            if (Modifiers.HasFlag(ModifierKeys.Control))
            {
                modifiers |= 0x0002;
            }

            if (Modifiers.HasFlag(ModifierKeys.Shift))
            {
                modifiers |= 0x0004;
            }

            if (Modifiers.HasFlag(ModifierKeys.Windows))
            {
                modifiers |= 0x0008;
            }

            return modifiers;
        }

        public uint GetVirtualKey() => (uint)KeyInterop.VirtualKeyFromKey(Key);

        public static bool IsModifierKey(Key key)
        {
            return key is Key.LeftCtrl or Key.RightCtrl
                or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin;
        }

        private static ModifierKeys NormalizeModifiers(ModifierKeys modifiers)
        {
            return modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows);
        }

        private static bool TryParseKeyToken(string token, out Key key)
        {
            key = Key.None;
            var keyConverter = new KeyConverter();
            try
            {
                var converted = keyConverter.ConvertFromInvariantString(token);
                if (converted is Key parsed)
                {
                    key = parsed;
                    return true;
                }
            }
            catch
            {
            }

            if (token.Length == 1 && char.IsDigit(token[0]))
            {
                return Enum.TryParse($"D{token}", out key);
            }

            return Enum.TryParse(token, ignoreCase: true, out key);
        }

        private static string GetKeyDisplayString(Key key)
        {
            if (key is >= Key.D0 and <= Key.D9)
            {
                return ((char)('0' + (key - Key.D0))).ToString();
            }

            if (key is >= Key.A and <= Key.Z)
            {
                return key.ToString().ToUpperInvariant();
            }

            var keyConverter = new KeyConverter();
            return keyConverter.ConvertToInvariantString(key) ?? key.ToString();
        }
    }
}
