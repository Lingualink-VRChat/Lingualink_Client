using System.Collections.ObjectModel;
using System.Collections.Generic; // For List<string>
using System.Linq;
using lingualink_client.Services;
using CommunityToolkit.Mvvm.ComponentModel; // 添加
using CommunityToolkit.Mvvm.Input;       // 添加

namespace lingualink_client.ViewModels
{
    public partial class SelectableTargetLanguageViewModel : ViewModelBase // 声明为 partial
    {
        public IndexWindowViewModel ParentViewModel { get; }

        /// <summary>
        /// 后端使用的语言名称（中文，用于存储和传参）
        /// </summary>
        [ObservableProperty]
        private string _selectedBackendLanguage;

        /// <summary>
        /// 界面显示的语言项目集合
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<LanguageDisplayItem> _availableLanguages;

        /// <summary>
        /// 当前选中的显示语言项目
        /// </summary>
        [ObservableProperty]
        private LanguageDisplayItem? _selectedDisplayLanguage;

        [ObservableProperty] private string _label; // Backing field for Label

        public string LabelText => LanguageManager.GetString("TargetLanguageLabel"); // 这是一个计算属性，不是 [ObservableProperty]
        public string RemoveLabel => LanguageManager.GetString("Remove"); // 移除按钮的标签

        [ObservableProperty]
        private bool _canRemove;

        // RemoveCommand 将被 [RelayCommand] 生成
        // public DelegateCommand RemoveCommand { get; } // 移除此行

        /// <summary>
        /// 用于外部访问的选中语言（后端名称）
        /// </summary>
        public string SelectedLanguage 
        { 
            get => SelectedBackendLanguage; 
            set => SelectedBackendLanguage = value; 
        }

        public SelectableTargetLanguageViewModel(IndexWindowViewModel parent, string initialBackendLanguage, List<string> allBackendLangsSeed)
        {
            ParentViewModel = parent;
            SelectedBackendLanguage = initialBackendLanguage;
            
            // 初始化空的可用语言列表
            AvailableLanguages = new ObservableCollection<LanguageDisplayItem>();
            
            // 初始化可用语言列表
            UpdateAvailableLanguages(allBackendLangsSeed);

            // 设置初始选中的显示语言
            SelectedDisplayLanguage = AvailableLanguages.FirstOrDefault(item => item.BackendName == initialBackendLanguage);

            // RemoveCommand 不再需要手动初始化，由 [RelayCommand] 生成
            // No need to initialize RemoveCommand here

            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>
        /// 更新可用语言列表（根据当前界面语言重新本地化）
        /// </summary>
        public void UpdateAvailableLanguages(List<string> backendLanguages)
        {
            var currentSelectedBackend = SelectedBackendLanguage;
            
            AvailableLanguages = new ObservableCollection<LanguageDisplayItem>();
            foreach (var backendLang in backendLanguages)
            {
                AvailableLanguages.Add(new LanguageDisplayItem
                {
                    BackendName = backendLang,
                    DisplayName = LanguageDisplayHelper.GetDisplayName(backendLang)
                });
            }
            
            // 恢复选中状态
            SelectedDisplayLanguage = AvailableLanguages.FirstOrDefault(item => item.BackendName == currentSelectedBackend);
        }

        /// <summary>
        /// 语言界面切换时的回调
        /// </summary>
        private void OnLanguageChanged()
        {
            OnPropertyChanged(nameof(LabelText));
            OnPropertyChanged(nameof(RemoveLabel));
            
            // 重新本地化所有语言显示名称
            foreach (var item in AvailableLanguages)
            {
                item.DisplayName = LanguageDisplayHelper.GetDisplayName(item.BackendName);
            }
            
            // 通知界面更新
            OnPropertyChanged(nameof(AvailableLanguages));
        }

        // SelectedBackendLanguage 属性的 OnChanged 回调
        partial void OnSelectedBackendLanguageChanged(string oldValue, string newValue)
        {
            ParentViewModel?.OnLanguageSelectionChanged(this);
        }

        // SelectedDisplayLanguage 属性的 OnChanged 回调
        partial void OnSelectedDisplayLanguageChanged(LanguageDisplayItem? oldValue, LanguageDisplayItem? newValue)
        {
            if (newValue != null && newValue.BackendName != SelectedBackendLanguage)
            {
                SelectedBackendLanguage = newValue.BackendName;
                OnPropertyChanged(nameof(SelectedLanguage));
                ParentViewModel?.OnLanguageSelectionChanged(this);
            }
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