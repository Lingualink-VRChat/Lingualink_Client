using System.Collections.ObjectModel;
using System.Collections.Generic; // For List<string>
using lingualink_client.Services;
using CommunityToolkit.Mvvm.ComponentModel; // 添加
using CommunityToolkit.Mvvm.Input;       // 添加

namespace lingualink_client.ViewModels
{
    public partial class SelectableTargetLanguageViewModel : ViewModelBase // 声明为 partial
    {
        public IndexWindowViewModel ParentViewModel { get; }

        [ObservableProperty]
        private string _selectedLanguage;

        [ObservableProperty] // 这里 collection 实例本身可以作为 ObservableProperty (尽管通常是其内容变化)
        private ObservableCollection<string> _availableLanguages;

        [ObservableProperty] private string _label; // Backing field for Label

        public string LabelText => LanguageManager.GetString("TargetLanguageLabel"); // 这是一个计算属性，不是 [ObservableProperty]

        [ObservableProperty]
        private bool _canRemove;

        // RemoveCommand 将被 [RelayCommand] 生成
        // public DelegateCommand RemoveCommand { get; } // 移除此行

        public SelectableTargetLanguageViewModel(IndexWindowViewModel parent, string initialSelection, List<string> allLangsSeed)
        {
            ParentViewModel = parent;
            _availableLanguages = new ObservableCollection<string>(allLangsSeed);
            _selectedLanguage = initialSelection; // 初始赋值给 backing field

            // RemoveCommand 不再需要手动初始化，由 [RelayCommand] 生成
            // No need to initialize RemoveCommand here

            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(LabelText));
        }

        // SelectedLanguage 属性的 OnChanged 回调
        partial void OnSelectedLanguageChanged(string oldValue, string newValue)
        {
            ParentViewModel?.OnLanguageSelectionChanged(this);
        }

        // CanRemove 属性的 OnChanged 回调
        partial void OnCanRemoveChanged(bool oldValue, bool newValue)
        {
            RemoveItemCommand.NotifyCanExecuteChanged(); // 更新命令的 CanExecute 状态
        }

        // RelayCommand 方法
        [RelayCommand(CanExecute = nameof(CanRemove))] // 绑定 CanExecute 方法
        private void RemoveItem() // 方法名与命令名对应
        {
            ParentViewModel.RemoveLanguageItem(this);
        }
    }
}