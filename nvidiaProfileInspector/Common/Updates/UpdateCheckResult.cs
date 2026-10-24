namespace nvidiaProfileInspector.Common.Updates
{
    public sealed class UpdateCheckResult
    {
        private UpdateCheckResult(bool succeeded, bool isUpdateAvailable, UpdateRelease latestRelease, string statusMessage, string errorMessage)
        {
            Succeeded = succeeded;
            IsUpdateAvailable = isUpdateAvailable;
            LatestRelease = latestRelease;
            StatusMessage = statusMessage;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded { get; }
        public bool IsUpdateAvailable { get; }
        public UpdateRelease LatestRelease { get; }
        public string StatusMessage { get; }
        public string ErrorMessage { get; }
        public bool CanInstall => IsUpdateAvailable && LatestRelease?.IsInstallable == true;

        public static UpdateCheckResult Available(UpdateRelease release)
        {
            var message = release?.IsInstallable == true
                ? "有新版本已就绪，可以安装。"
                : "有新版本可用，但其中不包含受支持的更新包。";
            return new UpdateCheckResult(true, true, release, message, null);
        }

        public static UpdateCheckResult UpToDate(UpdateRelease release)
        {
            return new UpdateCheckResult(true, false, release, "您已在使用当前通道的最新版本。", null);
        }

        public static UpdateCheckResult Unavailable(string message)
        {
            return new UpdateCheckResult(false, false, null, message, message);
        }
    }
}
