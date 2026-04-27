using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    public partial class PeerAudioLanguageItemViewModel : ViewModelBase
    {
        private readonly PeerAudioTranslationWindowViewModel _owner;

        [ObservableProperty]
        private ObservableCollection<LanguageDisplayItem> _availableLanguages = new ObservableCollection<LanguageDisplayItem>();

        [ObservableProperty]
        private LanguageDisplayItem? _selectedDisplayLanguage;

        [ObservableProperty]
        private string _label = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
        private bool _canRemove;

        public string BackendName => SelectedDisplayLanguage?.BackendName ?? string.Empty;
        public string RemoveLabel => LanguageManager.GetString("Remove");

        public PeerAudioLanguageItemViewModel(
            PeerAudioTranslationWindowViewModel owner,
            string backendName,
            ObservableCollection<LanguageDisplayItem> availableLanguages)
        {
            _owner = owner;
            AvailableLanguages = availableLanguages;
            SelectedDisplayLanguage = AvailableLanguages.FirstOrDefault(item => item.BackendName == backendName)
                ?? AvailableLanguages.FirstOrDefault();
        }

        partial void OnSelectedDisplayLanguageChanged(LanguageDisplayItem? oldValue, LanguageDisplayItem? newValue)
        {
            _owner.OnPeerAudioTargetLanguageChanged();
            OnPropertyChanged(nameof(BackendName));
        }

        [RelayCommand(CanExecute = nameof(CanRemove))]
        private void Remove()
        {
            _owner.RemovePeerAudioTargetLanguage(this);
        }
    }

    public sealed class PeerAudioChatMessage
    {
        public string Text { get; }
        public bool IsError { get; }

        public PeerAudioChatMessage(string text, bool isError)
        {
            Text = text;
            IsError = isError;
        }
    }
}
