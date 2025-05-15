Okay, let's address these points. This involves some refactoring to share the ViewModel correctly, creating a new page for logs, and adjusting the `NavigationView`.

**Summary of Changes:**

1.  **Shared `IndexWindowViewModel`**: The `IndexWindowViewModel` will be instantiated once in `App.xaml.cs` and made accessible globally. `IndexPage` (and the new `LogPage`) will use this shared instance. This will fix the logging issue.
2.  **New `LogPage`**:
    *   Create `Views/Pages/LogPage.xaml` and `Views/Pages/LogPage.xaml.cs`.
    *   Move the log display UI from `IndexPage.xaml` to `LogPage.xaml`.
    *   `LogPage` will use the shared `IndexWindowViewModel` for its `DataContext`.
    *   Add a "日志" (Log) item to the `NavigationView` in `MainWindow.xaml`.
3.  **Remove Back Button**: Configure the `NavigationView` in `MainWindow.xaml` to hide the back button.
4.  **Namespace Adjustments (Implicit)**: I'll assume the C# namespaces for pages within `Views/Pages/` are `lingualink_client.Views` as per your current `IndexPage.xaml.cs` and `ServicePage.xaml.cs`. If they were `lingualink_client.Views.Pages`, the XAML type references would need adjustment, but I'll stick to your current pattern.

Here are the modified and new files:

**1. `App.xaml.cs` (Modified)**
   *   Instantiate and hold the shared `IndexWindowViewModel`.
   *   Dispose of the ViewModel on application exit.

```csharp
// File: App.xaml.cs
using lingualink_client.ViewModels; // Add this using statement
using System.Windows;

namespace lingualink_client
{
    public partial class App : Application
    {
        public IndexWindowViewModel SharedIndexWindowViewModel { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            SharedIndexWindowViewModel = new IndexWindowViewModel();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SharedIndexWindowViewModel?.Dispose(); // Ensure ViewModel is disposed
            base.OnExit(e);
        }
    }
}
```

**2. `Views/Pages/IndexPage.xaml.cs` (Modified)**
   *   Use the shared `IndexWindowViewModel` from `App.xaml.cs`.

```csharp
// File: Views/Pages/IndexPage.xaml.cs
using System.Windows; // Required for Application.Current
using System.Windows.Controls;

namespace lingualink_client.Views // Assuming this namespace based on your existing files
{
    public partial class IndexPage : Page
    {
        // private readonly IndexWindowViewModel _viewModel; // This field is no longer needed here

        public IndexPage()
        {
            InitializeComponent();
            // Get the shared ViewModel from App.xaml.cs
            DataContext = (Application.Current as App)?.SharedIndexWindowViewModel;
        }
    }
}
```

**3. `Views/Pages/IndexPage.xaml` (Modified)**
   *   Remove the log display section (GridSplitter and the right column).

```xml
<Page
    x:Class="lingualink_client.Views.IndexPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="clr-namespace:lingualink_client.Views" 
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:lingualink_client.ViewModels"
    xmlns:converters="clr-namespace:lingualink_client.Converters"
    Title="IndexPage"
    d:DataContext="{d:DesignInstance Type=vm:IndexWindowViewModel}"
    mc:Ignorable="d">

    <Grid Margin="10">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" /> <!-- Only one column now -->
        </Grid.ColumnDefinitions>

        <!-- Main Controls (was Left Column) -->
        <Grid Grid.Column="0"> <!-- Margin="0,0,5,0" removed -->
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" /> <!-- Mic Selection -->
                <RowDefinition Height="Auto" /> <!-- Target Languages Expander -->
                <RowDefinition Height="Auto" /> <!-- Work Button -->
                <RowDefinition Height="Auto" /> <!-- Status Text -->
                <RowDefinition Height="*" />   <!-- Translation Result -->
                <RowDefinition Height="Auto" /> <!-- Hint -->
            </Grid.RowDefinitions>

            <StackPanel
                Grid.Row="0"
                Margin="0,0,0,10"
                Orientation="Horizontal">
                <Label VerticalAlignment="Center" Content="选择麦克风：" />
                <ComboBox
                    Width="200"
                    Margin="5,0,0,0"
                    VerticalAlignment="Center"
                    DisplayMemberPath="FriendlyName"
                    IsEnabled="{Binding IsMicrophoneComboBoxEnabled}"
                    ItemsSource="{Binding Microphones}"
                    SelectedItem="{Binding SelectedMicrophone}" />
                <Button
                    Margin="10,0,0,0"
                    Padding="5,2"
                    VerticalAlignment="Center"
                    Command="{Binding RefreshMicrophonesCommand}"
                    Content="刷新" />
                <StatusBar
                    Margin="10,0,0,0"
                    HorizontalAlignment="Right"
                    VerticalAlignment="Center"
                    Background="Transparent"
                    Opacity="0.7"
                    Visibility="{Binding IsRefreshingMicrophones, Converter={StaticResource BooleanToVisibilityConverter}}">
                    <StatusBarItem>
                        <TextBlock Text="正在刷新麦克风..." />
                    </StatusBarItem>
                </StatusBar>
            </StackPanel>

            <Expander Grid.Row="1" Header="目标翻译语言" Margin="0,0,0,10" IsExpanded="True">
                <StackPanel Margin="10,5,0,0">
                    <ItemsControl ItemsSource="{Binding TargetLanguageItems}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate DataType="{x:Type vm:SelectableTargetLanguageViewModel}">
                                <Grid Margin="0,3,0,3">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto" />
                                        <ColumnDefinition Width="*" />
                                        <ColumnDefinition Width="Auto" />
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="0" Margin="0,0,8,0" VerticalAlignment="Center" Text="{Binding Label}" />
                                    <ComboBox Grid.Column="1" MinWidth="150" MaxWidth="200" HorizontalAlignment="Stretch"
                                              ItemsSource="{Binding AvailableLanguages}" SelectedItem="{Binding SelectedLanguage}" />
                                    <Button Grid.Column="2" Margin="8,0,0,0" Padding="5,2" VerticalAlignment="Center"
                                            Command="{Binding RemoveCommand}" Content="移除"
                                            Visibility="{Binding CanRemove, Converter={StaticResource BooleanToVisibilityConverter}}" />
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    <Button Margin="0,5,0,0" Padding="5,2" HorizontalAlignment="Left"
                            Command="{Binding AddLanguageCommand}" Content="增加语言" />
                </StackPanel>
            </Expander>
            
            <Button
                Grid.Row="2" Margin="0,0,0,10"
                Padding="10,5"
                HorizontalAlignment="Left"
                Command="{Binding ToggleWorkCommand}"
                Content="{Binding WorkButtonContent}" />

            <TextBlock
                Grid.Row="3"
                Margin="0,0,0,10"
                FontSize="14"
                Text="{Binding StatusText}" />

            <TextBox
                Grid.Row="4"
                Margin="0,0,0,10"
                AcceptsReturn="True"
                IsReadOnly="True"
                Text="{Binding TranslationResultText, Mode=OneWay}"
                TextWrapping="Wrap"
                VerticalScrollBarVisibility="Auto" />

            <TextBlock
                Grid.Row="5"
                FontStyle="Italic"
                Foreground="Gray"
                Text="提示：点击“开始工作”后，应用将持续监听麦克风进行VAD检测。" />
        </Grid>

        <!-- GridSplitter and Right Column for Log REMOVED -->

    </Grid>
</Page>
```

**4. `Views/Pages/LogPage.xaml` (New File)**
   *   This page will now contain the log display.

```xml
<!-- File: Views/Pages/LogPage.xaml -->
<Page
    x:Class="lingualink_client.Views.LogPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:lingualink_client.ViewModels"
    xmlns:converters="clr-namespace:lingualink_client.Converters"
    mc:Ignorable="d"
    d:DataContext="{d:DesignInstance Type=vm:IndexWindowViewModel}"
    Title="LogPage">

    <Grid Margin="15"> <!-- Increased margin for a page view -->
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" /> <!-- Log Label & Clear Button -->
            <RowDefinition Height="*" />   <!-- Log TextBox -->
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,10"> <!-- Adjusted margin -->
            <Label Content="运行日志:" FontSize="16" FontWeight="SemiBold" VerticalAlignment="Center"/>
            <Button Content="清除日志" Command="{Binding ClearLogCommand}" HorizontalAlignment="Right" Margin="15,0,0,0" Padding="8,4" VerticalAlignment="Center"/>
        </StackPanel>
        
        <TextBox Grid.Row="1"
                 IsReadOnly="True"
                 AcceptsReturn="True"
                 TextWrapping="Wrap"
                 VerticalScrollBarVisibility="Auto"
                 Text="{Binding LogMessages, Converter={StaticResource StringJoinConverter}, Mode=OneWay}" 
                 FontFamily="Consolas" FontSize="12"/> <!-- Monospaced font for logs is common -->
    </Grid>
</Page>
```

**5. `Views/Pages/LogPage.xaml.cs` (New File)**
   *   Code-behind for `LogPage`, sets the shared `DataContext`.

```csharp
// File: Views/Pages/LogPage.xaml.cs
using System.Windows;
using System.Windows.Controls;

namespace lingualink_client.Views // Assuming this namespace
{
    public partial class LogPage : Page
    {
        public LogPage()
        {
            InitializeComponent();
            DataContext = (Application.Current as App)?.SharedIndexWindowViewModel;
        }
    }
}
```

**6. `MainWindow.xaml` (Modified)**
   *   Add a `NavigationViewItem` for the new `LogPage`.
   *   Hide the back button in `NavigationView`.
   *   Ensure the `pages` xmlns points to the correct namespace if it was different. Your current `xmlns:pages="clr-namespace:lingualink_client.Views"` seems to be what you're using for pages like `IndexPage` class name, so we'll stick to that.

```xml
<ui:FluentWindow
    x:Class="lingualink_client.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="clr-namespace:lingualink_client"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:pages="clr-namespace:lingualink_client.Views" <!-- This namespace should contain IndexPage, ServicePage, LogPage classes -->
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    Title="MainWindow"
    Width="1100"
    Height="650"
    d:DesignHeight="450"
    d:DesignWidth="800"
    ui:Design.Background="{DynamicResource ApplicationBackgroundBrush}"
    ui:Design.Foreground="{DynamicResource TextFillColorPrimaryBrush}"
    Closed="MainWindow_Closed"
    ExtendsContentIntoTitleBar="True"
    Foreground="{DynamicResource TextFillColorPrimaryBrush}"
    WindowBackdropType="Mica"
    WindowCornerPreference="Round"
    WindowStartupLocation="CenterScreen"
    mc:Ignorable="d">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <ui:TitleBar Title="VRChat LinguaLink - Client" Grid.Row="0">
            <ui:TitleBar.TrailingContent>
                <ToggleButton
                    VerticalAlignment="Top"
                    BorderThickness="0"
                    Checked="ThemesChanged"
                    Unchecked="ThemesChanged">
                    <ui:SymbolIcon Symbol="DarkTheme24" />
                </ToggleButton>
            </ui:TitleBar.TrailingContent>
        </ui:TitleBar>

        <ui:NavigationView
            x:Name="RootNavigation"
            Grid.Row="1"
            IsBackButtonVisible="Collapsed" <!-- ADD THIS LINE to hide the back button -->
            OpenPaneLength="150">
            <ui:NavigationView.Header>
                <ui:BreadcrumbBar Margin="42,32,0,0" FontSize="28" FontWeight="DemiBold" />
            </ui:NavigationView.Header>
            <ui:NavigationView.MenuItems>
                <ui:NavigationViewItem
                    Content="启动"
                    NavigationCacheMode="Enabled"
                    TargetPageType="{x:Type pages:IndexPage}">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="Home24" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
                <ui:NavigationViewItem
                    Content="服务"
                    NavigationCacheMode="Enabled"
                    TargetPageType="{x:Type pages:ServicePage}">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="ServerSurfaceMultiple16" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
                <!-- NEW LOG PAGE NAVIGATION ITEM -->
                <ui:NavigationViewItem
                    Content="日志"
                    NavigationCacheMode="Enabled" 
                    TargetPageType="{x:Type pages:LogPage}">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="TextBulletListLtr24" /> <!-- Example icon, choose one you like -->
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
            </ui:NavigationView.MenuItems>
            <ui:NavigationView.FooterMenuItems>
                <ui:NavigationViewItem
                    Content="设置"
                    NavigationCacheMode="Disabled"
                    TargetPageType="{x:Type pages:SettingPage}">
                    <ui:NavigationViewItem.Icon>
                        <ui:SymbolIcon Symbol="Settings24" />
                    </ui:NavigationViewItem.Icon>
                </ui:NavigationViewItem>
            </ui:NavigationView.FooterMenuItems>
        </ui:NavigationView>
    </Grid>
</ui:FluentWindow>
```

**Before Running:**

1.  **Create the new files**: `Views/Pages/LogPage.xaml` and `Views/Pages/LogPage.xaml.cs` with the content provided above.
2.  **Build the project**: Ensure there are no compilation errors.
3.  **Test Logging**: Start the application, perform actions that should generate log messages (e.g., try translating, change target languages, if OSC fails it should log). Navigate to the "日志" page to see if messages appear. Test the "清除日志" button.
4.  **Test Back Button**: Navigate between pages. The back button in the header area should no longer be visible.

This set of changes should resolve your issues and restructure the application as requested. The key fix for logging is ensuring that the `IndexPage` (and now `LogPage`) uses the single, correct instance of `IndexWindowViewModel` where the `LogMessages` are actually being populated.