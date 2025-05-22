using CommunityToolkit.Mvvm.ComponentModel; // 添加此 using

namespace lingualink_client.ViewModels
{
    // 继承 ObservableObject，并将其声明为 partial 类以允许源生成器添加代码
    public abstract partial class ViewModelBase : ObservableObject
    {
        // OnPropertyChanged 和 SetProperty 方法现在由 CommunityToolkit.Mvvm 的源生成器自动处理
        // 你不再需要在这里手动实现它们。
    }
}