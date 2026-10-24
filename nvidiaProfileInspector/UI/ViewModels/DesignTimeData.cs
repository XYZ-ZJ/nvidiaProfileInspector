namespace nvidiaProfileInspector.UI.ViewModels
{
    using nvidiaProfileInspector.Common;
    using System.Collections.ObjectModel;

    public static class DesignTimeData
    {
        public static MainViewModel MainViewModel => new MainViewModel(null, null, null, null);
        public static BitEditorViewModel BitEditorViewModel => new BitEditorViewModel(
            0x10F9DC81,
            0x0000100D,
            "抗锯齿兼容性标志",
            null,
            null,
            null);

        public static ObservableCollection<SettingItemViewModel> SampleSettings { get; } = new ObservableCollection<SettingItemViewModel>
        {
            new SettingItemViewModel(new SettingItem
            {
                SettingId = 0x00000001,
                SettingText = "抗锯齿 - 模式",
                ValueText = "增强应用程序设置",
                ValueRaw = "0x00000002",
                GroupName = "抗锯齿",
                State = SettingState.NvidiaSetting
            }),
            new SettingItemViewModel(new SettingItem
            {
                SettingId = 0x00000002,
                SettingText = "抗锯齿 - 设置",
                ValueText = "4x MSAA",
                ValueRaw = "0x00000004",
                GroupName = "抗锯齿",
                State = SettingState.UserdefinedSetting
            }),
            new SettingItemViewModel(new SettingItem
            {
                SettingId = 0x00000003,
                SettingText = "纹理过滤 - 质量",
                ValueText = "高性能",
                ValueRaw = "0x00000001",
                GroupName = "纹理过滤",
                State = SettingState.GlobalSetting
            }),
            new SettingItemViewModel(new SettingItem
            {
                SettingId = 0x00000004,
                SettingText = "垂直同步",
                ValueText = "On",
                ValueRaw = "0x00000001",
                GroupName = "Sync",
                State = SettingState.UserdefinedSetting,
                IsSettingHidden = true
            }),
            new SettingItemViewModel(new SettingItem
            {
                SettingId = 0x00000005,
                SettingText = "电源管理模式",
                ValueText = "优先最高性能",
                ValueRaw = "0x00000002",
                GroupName = "电源管理",
                State = SettingState.NvidiaSetting
            })
        };
    }
}
