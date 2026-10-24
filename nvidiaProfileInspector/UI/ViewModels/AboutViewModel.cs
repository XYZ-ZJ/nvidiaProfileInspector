namespace nvidiaProfileInspector.UI.ViewModels
{
    using nvidiaProfileInspector.Common.Helper;
    using nvidiaProfileInspector.Common.Updates;
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;

    public class Contributor
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public string AvatarUrl { get; set; }
        public string ProfileUrl { get; set; }
    }

    public class AboutViewModel : ViewModelBase
    {
        private bool _isAutomaticUpdateCheckEnabled;
        private bool _isSplashScreenDisabled;
        private bool _isUpdateAvailable;
        private bool _isUpdating;
        private string _latestVersionText = "未检查";
        private string _selectedUpdateChannel;
        private string _updateStatusText = "请选择更新通道并检查更新。";
        private UpdateCheckResult _lastUpdateCheckResult;
        private readonly AppUpdateService _updateService = new AppUpdateService();
        private int _updateCheckGeneration;

        public string Version { get; }
        public ObservableCollection<Contributor> Contributors { get; } = new ObservableCollection<Contributor>();
        public ObservableCollection<string> UpdateChannels { get; } = new ObservableCollection<string>
        {
            UpdateChannelFormatter.ToDisplayName(UpdateChannel.Release),
            UpdateChannelFormatter.ToDisplayName(UpdateChannel.Prerelease)
        };
        public string GitHubUrl => "https://github.com/Orbmu2k/nvidiaProfileInspector/releases";
        public string GitHubRepositoryUrl => "https://github.com/Orbmu2k/nvidiaProfileInspector";

        public bool IsAutomaticUpdateCheckEnabled
        {
            get => _isAutomaticUpdateCheckEnabled;
            set
            {
                if (SetProperty(ref _isAutomaticUpdateCheckEnabled, value, nameof(IsAutomaticUpdateCheckEnabled)))
                {
                    var settings = UserSettings.LoadSettings();
                    settings.DisableUpdateCheck = !value;
                    settings.SaveSettings();
                }
            }
        }

        public bool IsSplashScreenDisabled
        {
            get => _isSplashScreenDisabled;
            set
            {
                if (SetProperty(ref _isSplashScreenDisabled, value, nameof(IsSplashScreenDisabled)))
                {
                    var settings = UserSettings.LoadSettings();
                    settings.DisableSplashScreen = value;
                    settings.SaveSettings();
                }
            }
        }

        public string SelectedUpdateChannel
        {
            get => _selectedUpdateChannel;
            set
            {
                if (SetProperty(ref _selectedUpdateChannel, value, nameof(SelectedUpdateChannel)))
                {
                    var settings = UserSettings.LoadSettings();
                    settings.UpdateChannel = UpdateChannelFormatter.ToSettingsValue(UpdateChannelFormatter.Parse(value));
                    settings.SaveSettings();
                    _lastUpdateCheckResult = null;
                    IsUpdateAvailable = false;
                    LatestVersionText = "正在检查...";
                    UpdateStatusText = "正在检查 GitHub 发布...";
                    OnPropertyChanged(nameof(CanInstallUpdate));
                    _ = RefreshUpdateStatusAsync();
                }
            }
        }

        public string LatestVersionText
        {
            get => _latestVersionText;
            set => SetProperty(ref _latestVersionText, value, nameof(LatestVersionText));
        }

        public string UpdateStatusText
        {
            get => _updateStatusText;
            set => SetProperty(ref _updateStatusText, value, nameof(UpdateStatusText));
        }

        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                if (SetProperty(ref _isUpdating, value, nameof(IsUpdating)))
                    OnPropertyChanged(nameof(CanInstallUpdate));
            }
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set
            {
                if (SetProperty(ref _isUpdateAvailable, value, nameof(IsUpdateAvailable)))
                    OnPropertyChanged(nameof(CanInstallUpdate));
            }
        }

        public bool CanInstallUpdate => _lastUpdateCheckResult?.CanInstall == true && !IsUpdating;

        public ICommand OpenGitHubCommand { get; }
        public ICommand OpenGitHubRepositoryCommand { get; }
        public ICommand OpenContributorProfileCommand { get; }
        public ICommand CheckForUpdateCommand { get; }
        public ICommand InstallUpdateCommand { get; }

        public AboutViewModel(UpdateRelease latestAvailableRelease = null)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            Version = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

            var settings = UserSettings.LoadSettings();
            _selectedUpdateChannel = UpdateChannelFormatter.ToDisplayName(UpdateChannelFormatter.Parse(settings.UpdateChannel));
            _isAutomaticUpdateCheckEnabled = !settings.DisableUpdateCheck;
            _isSplashScreenDisabled = settings.DisableSplashScreen;

            if (latestAvailableRelease?.Version != null)
            {
                _lastUpdateCheckResult = UpdateCheckResult.Available(latestAvailableRelease);
                ApplyUpdateCheckResult(_lastUpdateCheckResult);
            }
            else
            {
                LatestVersionText = "正在检查...";
                UpdateStatusText = "正在检查 GitHub 发布...";
                _ = RefreshUpdateStatusAsync();
            }

            OpenGitHubCommand = new RelayCommand(_ => OpenUrl(GitHubUrl));
            OpenGitHubRepositoryCommand = new RelayCommand(_ => OpenUrl(GitHubRepositoryUrl));
            OpenContributorProfileCommand = new RelayCommand(param => OpenUrl(param as string));
            CheckForUpdateCommand = new AsyncRelayCommand(CheckForUpdateAsync);
            InstallUpdateCommand = new AsyncRelayCommand(InstallUpdateAsync, () => CanInstallUpdate);

            LoadContributors();
        }

        private async Task CheckForUpdateAsync()
        {
            await RefreshUpdateStatusAsync();
        }

        private async Task RefreshUpdateStatusAsync()
        {
            IsUpdating = true;
            var generation = ++_updateCheckGeneration;
            var channel = GetSelectedUpdateChannel();
            try
            {
                UpdateStatusText = "正在检查 GitHub 发布...";
                var result = await _updateService.CheckAsync(channel);
                if (generation != _updateCheckGeneration)
                    return;

                _lastUpdateCheckResult = result;
                ApplyUpdateCheckResult(_lastUpdateCheckResult);
            }
            finally
            {
                if (generation == _updateCheckGeneration)
                    IsUpdating = false;
            }
        }

        private async Task InstallUpdateAsync()
        {
            IsUpdating = true;
            try
            {
                UpdateStatusText = "正在准备更新包...";
                if (_lastUpdateCheckResult == null || !_lastUpdateCheckResult.CanInstall)
                {
                    _lastUpdateCheckResult = await _updateService.CheckAsync(GetSelectedUpdateChannel());
                    ApplyUpdateCheckResult(_lastUpdateCheckResult);

                    if (!_lastUpdateCheckResult.CanInstall)
                        return;
                }

                await _updateService.PrepareInstallAsync(_lastUpdateCheckResult.LatestRelease);
                UpdateStatusText = "更新已下载，正在重启应用程序...";
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                UpdateStatusText = "更新失败。";
                MessageBoxViewModel.Show(
                    $"无法安装更新。\r\n\r\n{ex.Message}",
                    "更新失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsUpdating = false;
            }
        }

        private UpdateChannel GetSelectedUpdateChannel()
        {
            return UpdateChannelFormatter.Parse(SelectedUpdateChannel);
        }

        private void ApplyUpdateCheckResult(UpdateCheckResult result)
        {
            IsUpdateAvailable = result?.IsUpdateAvailable == true;
            LatestVersionText = result?.LatestRelease?.DisplayText ?? "不可用";
            UpdateStatusText = result?.StatusMessage ?? "Could not read release information.";
            OnPropertyChanged(nameof(CanInstallUpdate));
        }

        private void LoadContributors()
        {
            Contributors.Add(new Contributor
            {
                Name = "Orbmu2k",
                Role = "首席架构师（荣誉）",
                AvatarUrl = "https://github.com/Orbmu2k.png",
                ProfileUrl = "https://github.com/Orbmu2k"
            });
            Contributors.Add(new Contributor
            {
                Name = "emoose",
                Role = "主要开发者",
                AvatarUrl = "https://github.com/emoose.png",
                ProfileUrl = "https://github.com/emoose"
            });
            Contributors.Add(new Contributor
            {
                Name = "Warkratos",
                Role = "开发者兼驱动设置研究员",
                AvatarUrl = "https://github.com/warkratos.png",
                ProfileUrl = "https://github.com/warkratos"
            });
            Contributors.Add(new Contributor
            {
                Name = "DarkStarSword",
                Role = "贡献者",
                AvatarUrl = "https://github.com/DarkStarSword.png",
                ProfileUrl = "https://github.com/DarkStarSword"
            });
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
