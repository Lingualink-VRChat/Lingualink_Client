// File: Services/SettingsChangedNotifier.cs
using System;

namespace lingualink_client.Services
{
    public static class SettingsChangedNotifier
    {
        public static event Action? SettingsChanged;

        public static void RaiseSettingsChanged()
        {
            SettingsChanged?.Invoke();
        }
    }
}