namespace nvidiaProfileInspector.UI.ViewModels
{
    using nvidiaProfileInspector.Common;

    public class ProfileListItem
    {
        public ProfileListItem(string profileName, bool isUserDefined)
        {
            ProfileName = profileName;
            IsGlobal = profileName == DrsSettingsService.GlobalProfileName;
            IsUserDefined = !IsGlobal && isUserDefined;
        }

        public string ProfileName { get; }

        public bool IsGlobal { get; }

        public bool IsUserDefined { get; }

        public string ProfileTypeLabel =>
            IsGlobal ? "全局驱动配置文件" :
            IsUserDefined ? "用户创建的配置文件" :
            "NVIDIA 预定义配置文件";

        public override string ToString()
        {
            return ProfileName;
        }
    }
}
