using System.Collections.ObjectModel;
using System.Collections.Generic; // For List<string>
using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    public class SelectableTargetLanguageViewModel : ViewModelBase
    {
        // Changed ParentViewModel type
        public IndexWindowViewModel ParentViewModel { get; }

        private string _selectedLanguage;
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    ParentViewModel?.OnLanguageSelectionChanged(this);
                }
            }
        }

        private ObservableCollection<string> _availableLanguages;
        public ObservableCollection<string> AvailableLanguages
        {
            get => _availableLanguages;
            set => SetProperty(ref _availableLanguages, value);
        }

        public string LabelText => LanguageManager.GetString("TargetLanguageLabel");

        private string _label;
        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        private bool _canRemove;
        public bool CanRemove
        {
            get => _canRemove;
            set
            {
                if (SetProperty(ref _canRemove, value))
                {
                    RemoveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DelegateCommand RemoveCommand { get; }

        // Changed constructor parameter type
        public SelectableTargetLanguageViewModel(IndexWindowViewModel parent, string initialSelection, List<string> allLangsSeed)
        {
            ParentViewModel = parent;
            _availableLanguages = new ObservableCollection<string>(allLangsSeed);
            _selectedLanguage = initialSelection;
            
            RemoveCommand = new DelegateCommand(
                _ => ParentViewModel.RemoveLanguageItem(this),
                _ => CanRemove
            );

            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(LabelText));
        }
    }
}