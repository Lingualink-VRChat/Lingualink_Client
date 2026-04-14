using lingualink_client.Services;
using lingualink_client.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using lingualink_client.Models;

namespace lingualink_client.Views
{
    /// <summary>
    /// MessageTemplatePage.xaml 的交互逻辑
    /// </summary>
    public partial class MessageTemplatePage : Page
    {
        private MessageTemplatePageViewModel? _viewModel;

        public MessageTemplatePage()
        {
            InitializeComponent();

            this.Loaded += MessageTemplatePage_Loaded;
        }

        private void MessageTemplatePage_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel ??= new MessageTemplatePageViewModel();
            DataContext = _viewModel;
            LanguageManager.LanguageChanged -= UpdateTemplateHint;
            LanguageManager.LanguageChanged += UpdateTemplateHint;
            UpdateTemplateHint();
            _viewModel.RefreshSettings();
        }

        private void UpdateTemplateHint()
        {
            if (TemplateHintText != null)
            {
                TemplateHintText.Text = LanguageManager.GetString("TemplateHint");
            }
        }
    }
} 
