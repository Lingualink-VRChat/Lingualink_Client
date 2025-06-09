using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using lingualink_client.Models;

namespace lingualink_client.ViewModels.Managers
{
    /// <summary>
    /// 麦克风管理器接口 - 负责麦克风设备的管理和选择
    /// </summary>
    public interface IMicrophoneManager : INotifyPropertyChanged
    {
        /// <summary>
        /// 可用麦克风设备集合
        /// </summary>
        ObservableCollection<MMDeviceWrapper> Microphones { get; }

        /// <summary>
        /// 当前选中的麦克风
        /// </summary>
        MMDeviceWrapper? SelectedMicrophone { get; set; }

        /// <summary>
        /// 是否正在刷新麦克风列表
        /// </summary>
        bool IsRefreshing { get; }

        /// <summary>
        /// 麦克风选择是否启用
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 是否有可用的麦克风
        /// </summary>
        bool HasMicrophones { get; }

        /// <summary>
        /// 当前选中的麦克风是否有效
        /// </summary>
        bool IsSelectedMicrophoneValid { get; }

        /// <summary>
        /// 刷新麦克风列表
        /// </summary>
        /// <returns>刷新任务</returns>
        Task RefreshAsync();
    }
} 