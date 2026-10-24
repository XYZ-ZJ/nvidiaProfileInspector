namespace nvidiaProfileInspector.UI.ViewModels
{
    public class ModifiedProfileItem
    {
        public ModifiedProfileItem(string profileName, bool isUserDefined)
        {
            ProfileName = profileName;
            IsUserDefined = isUserDefined;
        }

        public string ProfileName { get; }

        public bool IsUserDefined { get; }

        public string ProfileTypeLabel => IsUserDefined ? "用户创建的配置文件" : "NVIDIA 预定义配置文件";

        public override string ToString()
        {
            return ProfileName;
        }
    }
}
