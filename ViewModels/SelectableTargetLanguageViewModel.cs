// File: ViewModels/SelectableTargetLanguageViewModel.cs
using System.Collections.ObjectModel;
using System.Collections.Generic; // For List<string>
using lingualink_client.ViewModels; // For ViewModelBase and DelegateCommand

namespace lingualink_client.ViewModels
{
    public class SelectableTargetLanguageViewModel : ViewModelBase
    {
        public SettingsWindowViewModel ParentViewModel { get; }

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

        private string _label;
        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        private bool _canRemove;
        public bool CanRemove // Property for XAML Binding and Command's CanExecute
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

        public SelectableTargetLanguageViewModel(SettingsWindowViewModel parent, string initialSelection, List<string> allLangsSeed)
        {
            ParentViewModel = parent;
            _availableLanguages = new ObservableCollection<string>(allLangsSeed); // Initial full list, parent will refine
            _selectedLanguage = initialSelection; // Parent ensures this is valid or a default
            
            RemoveCommand = new DelegateCommand(
                _ => ParentViewModel.RemoveLanguageItem(this),
                _ => CanRemove // Predicate for the command
            );
        }
    }
}