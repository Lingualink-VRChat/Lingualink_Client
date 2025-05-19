using lingualink_client.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lingualink_client.ViewModels
{
    public class SettingPageViewModel : ViewModelBase
    {
        public string InterfaceLanguage => LanguageManager.GetString("InterfaceLanguage");

        public SettingPageViewModel()
        {
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(InterfaceLanguage));
        }
    }
}
