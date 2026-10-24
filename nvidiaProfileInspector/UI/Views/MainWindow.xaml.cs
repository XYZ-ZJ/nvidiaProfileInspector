namespace nvidiaProfileInspector.UI.Views
{
    using nvidiaProfileInspector;
    using nvidiaProfileInspector.Native.WINAPI;
    using nvidiaProfileInspector.Services;
    using nvidiaProfileInspector.UI.Controls;
    using nvidiaProfileInspector.UI.ViewModels;
    using nvidiaProfileInspector.UI.Views.Dialogs;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;

    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ThemeManager _themeManager;
        private readonly bool _disableInitialScan;
        private readonly bool _showOnlyCustomizedSettings;
        private HwndSource _windowSource;
        private bool _mainWindowNativeDropRegistered;
        private ContentControl _currentValueEditorHost;
        private SearchableComboBox _sharedValueEditor;

        public MainWindow(bool showOnlyCustomizedSettings = false, bool disableInitialScan = false)
        {
            InitializeComponent();
            _showOnlyCustomizedSettings = showOnlyCustomizedSettings;
            _disableInitialScan = disableInitialScan;
            _viewModel = App.Bootstrapper.Resolve<MainViewModel>();
            _themeManager = App.Bootstrapper.Resolve<ThemeManager>();
            _viewModel.OnOpenBitEditor += OnOpenBitEditor;
            _viewModel.OnFocusFilter += () => FilterTextBox.Focus();
            _themeManager.ThemeChanged += OnThemeChanged;
            DataContext = _viewModel;
            _sharedValueEditor = (SearchableComboBox)Resources["SharedValueEditorResource"];

            if (_showOnlyCustomizedSettings)
                _viewModel.ApplyCommonOnlySettingSource();

            ApplyMockTitle();
            SourceInitialized += MainWindow_SourceInitialized;
            Dispatcher.BeginInvoke(new Action(PreloadAccessibilityAssembly), DispatcherPriority.ApplicationIdle);
            SettingsListView.SelectionChanged += SettingsListView_SelectionChanged;
            SettingsListView.LayoutUpdated += SettingsListView_LayoutUpdated;
        }

        private static void PreloadAccessibilityAssembly()
        {
            try
            {
                Assembly.Load(new AssemblyName("Accessibility, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
            }
            catch
            {
            }
        }

        private void ApplyMockTitle()
        {
            if (!Native.NVAPI2.NvapiDrsWrapper.Instance.IsMockMode)
                return;

            const string mockTitle = "NVIDIA PROFILE INSPECTOR - 模拟模式！";
            Title = mockTitle;
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            WindowBackdropHelper.TryApplyTo(this);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var handle = new WindowInteropHelper(this).Handle;
            RegisterMainWindowNativeDrop(handle);
            var exStyle = DragAcceptNativeHelper.GetWindowLongPtr(handle, DragAcceptNativeHelper.GWL_EXSTYLE).ToInt64();
            if ((exStyle & DragAcceptNativeHelper.WS_EX_ACCEPTFILES) == 0)
            {
                DragAcceptNativeHelper.SetWindowLongPtr(
                    handle,
                    DragAcceptNativeHelper.GWL_EXSTYLE,
                    new IntPtr(exStyle | DragAcceptNativeHelper.WS_EX_ACCEPTFILES));
            }

            DragAcceptNativeHelper.DragAcceptFiles(handle, true);
            _windowSource = HwndSource.FromHwnd(handle);
            _windowSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == DragAcceptNativeHelper.WM_DROPFILES)
            {
                try
                {
                    var files = DragAcceptNativeHelper.GetDroppedFiles(wParam);
                    if (files.Length > 0)
                    {
                        HandleDroppedFiles(files);
                        handled = true;
                    }
                }
                finally
                {
                    DragAcceptNativeHelper.DragFinish(wParam);
                }

                return IntPtr.Zero;
            }

            if (msg == MessageHelper.WM_COPYDATA)
            {
                var data = Marshal.PtrToStructure<MessageHelper.COPYDATASTRUCT>(lParam);
                var message = data.lpData ?? string.Empty;

                if (HandleCopyDataMessage(message))
                {
                    handled = true;
                    return new IntPtr(1);
                }
            }

            // Fix controls jumping/flickering during resize
            // Based on https://github.com/sourcechord/FluentWPF/issues/102#issuecomment-903709242
            if (msg == MessageHelper.WM_NCCALCSIZE && wParam != IntPtr.Zero)
            {
                var param = Marshal.PtrToStructure<MessageHelper.NCCALCSIZE_PARAMS>(lParam);
                param.rgrc[0].Bottom -= 1;
                Marshal.StructureToPtr(param, lParam, false);
            }

            // Constrain maximized window to the monitor's work area so it doesn't cover the taskbar.
            if (msg == MessageHelper.WM_GETMINMAXINFO)
            {
                var monitor = MessageHelper.MonitorFromWindow(hwnd, MessageHelper.MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var monitorInfo = new MessageHelper.MONITORINFO { cbSize = Marshal.SizeOf<MessageHelper.MONITORINFO>() };
                    if (MessageHelper.GetMonitorInfo(monitor, ref monitorInfo))
                    {
                        var work = monitorInfo.rcWork;
                        var monitor_rect = monitorInfo.rcMonitor;
                        var mmi = Marshal.PtrToStructure<MessageHelper.MINMAXINFO>(lParam);
                        var dpiScale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
                        mmi.ptMinTrackSize = new MessageHelper.POINT
                        {
                            X = (int)Math.Ceiling(MinWidth * dpiScale.M11),
                            Y = (int)Math.Ceiling(MinHeight * dpiScale.M22)
                        };
                        mmi.ptMaxPosition = new MessageHelper.POINT
                        {
                            X = work.Left - monitor_rect.Left,
                            Y = work.Top - monitor_rect.Top
                        };
                        mmi.ptMaxSize = new MessageHelper.POINT
                        {
                            X = work.Right - work.Left,
                            Y = work.Bottom - work.Top
                        };
                        Marshal.StructureToPtr(mmi, lParam, false);
                    }
                }
                handled = true;
            }


            return IntPtr.Zero;
        }

        private void HandleDroppedFiles(string[] files)
        {
            if (files == null || files.Length == 0)
                return;

            if (files.All(file => string.Equals(Path.GetExtension(file), ".nip", StringComparison.InvariantCultureIgnoreCase)))
            {
                ImportNipFiles(files, showSuccessNotification: true);
                return;
            }

            if (files.Length != 1)
                return;

            var droppedFile = files[0];
            string profileName;
            var applicationName = Common.Helper.ShortcutResolver.ResolveExecuteable(droppedFile, out profileName);
            if (string.IsNullOrEmpty(applicationName))
                return;

            var profiles = _viewModel.FindProfilesUsingApplication(applicationName);
            if (!string.IsNullOrEmpty(profiles))
            {
                var profile = profiles.Split(';')[0];
                _viewModel.SelectProfile(profile);
                _viewModel.ShowSnackbar($"已选中“{applicationName}”的配置文件！", "成功");
                return;
            }

            var result = MessageBoxViewModel.Show(
                "要为此应用程序新建配置文件吗？",
                "未找到配置文件！",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.ShowCreateProfileDialog(profileName, applicationName);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreWindowSettings();
            await _viewModel.InitializeAsync(_disableInitialScan);
            UpdateSharedValueEditorPlacement();
        }

        private void OnThemeChanged(string themeName)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                WindowBackdropHelper.TryApplyTo(this);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void RestoreWindowSettings()
        {
            var settings = Common.Helper.UserSettings.LoadSettings();

            if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
            {
                Width = Math.Max(settings.WindowWidth, MinWidth);
                Height = Math.Max(settings.WindowHeight, MinHeight);
            }

            if (settings.WindowLeft > 0 && settings.WindowTop > 0)
            {
                Left = settings.WindowLeft;
                Top = settings.WindowTop;
            }

            if (settings.WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _themeManager.ThemeChanged -= OnThemeChanged;
            SettingsListView.SelectionChanged -= SettingsListView_SelectionChanged;
            SettingsListView.LayoutUpdated -= SettingsListView_LayoutUpdated;

            var restoreBounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
            _viewModel.SaveSettings(restoreBounds.Left, restoreBounds.Top, restoreBounds.Width, restoreBounds.Height, WindowState);
        }

        private void SettingsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateSharedValueEditorPlacement), DispatcherPriority.Loaded);
        }

        private void SettingsListView_LayoutUpdated(object? sender, EventArgs e)
        {
            UpdateSharedValueEditorPlacement();
        }

        private void UpdateSharedValueEditorPlacement()
        {
            if (!IsLoaded || _viewModel.SelectedSetting == null)
            {
                HideSharedValueEditor();
                return;
            }

            if (SettingsListView.ItemContainerGenerator.ContainerFromItem(_viewModel.SelectedSetting) is not ListBoxItem selectedItem
                || !selectedItem.IsVisible)
            {
                HideSharedValueEditor();
                return;
            }

            var anchor = FindDescendantByName<FrameworkElement>(selectedItem, "ValueEditorAnchor");
            if (anchor == null || !anchor.IsVisible || anchor.ActualWidth <= 0 || anchor.ActualHeight <= 0)
            {
                HideSharedValueEditor();
                return;
            }

            var host = FindDescendantByName<ContentControl>(selectedItem, "ValueEditorHost");
            if (host == null)
            {
                HideSharedValueEditor();
                return;
            }

            if (!ReferenceEquals(_currentValueEditorHost, host))
            {
                DetachSharedValueEditor();
                host.Content = _sharedValueEditor;
                _currentValueEditorHost = host;
            }

            _sharedValueEditor.Width = double.NaN;
            _sharedValueEditor.ClearValue(FrameworkElement.HeightProperty);
            _sharedValueEditor.HorizontalAlignment = HorizontalAlignment.Stretch;
            _sharedValueEditor.VerticalAlignment = VerticalAlignment.Stretch;
            _sharedValueEditor.FontWeight = selectedItem.FontWeight;
            _sharedValueEditor.Visibility = Visibility.Visible;
            ApplySharedValueEditorTypography(selectedItem);
            _sharedValueEditor.UpdateLayout();
        }

        private void HideSharedValueEditor()
        {
            _sharedValueEditor.IsDropDownOpen = false;
            _sharedValueEditor.Visibility = Visibility.Collapsed;
            DetachSharedValueEditor();
        }

        private void DetachSharedValueEditor()
        {
            if (_currentValueEditorHost != null && ReferenceEquals(_currentValueEditorHost.Content, _sharedValueEditor))
                _currentValueEditorHost.Content = null;

            _currentValueEditorHost = null;
        }

        private void ApplySharedValueEditorTypography(ListBoxItem selectedItem)
        {
            if (FindDescendantByName<TextBlock>(selectedItem, "DisplayText") is TextBlock displayText)
                _sharedValueEditor.Foreground = displayText.Foreground;

            if (_sharedValueEditor.Template?.FindName("PART_EditableTextBox", _sharedValueEditor) is TextBox editableTextBox)
            {
                editableTextBox.FontWeight = selectedItem.FontWeight;
                editableTextBox.Foreground = _sharedValueEditor.Foreground;
            }

            if (_sharedValueEditor.Template?.FindName("ContentSite", _sharedValueEditor) is TextBlock contentSite)
            {
                contentSite.FontWeight = selectedItem.FontWeight;
                contentSite.Foreground = _sharedValueEditor.Foreground;
            }
        }

        private static T? FindDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root == null)
                return null;

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T element && string.Equals(element.Name, name, StringComparison.Ordinal))
                    return element;

                var match = FindDescendantByName<T>(child, name);
                if (match != null)
                    return match;
            }

            return null;
        }

        private void CustomSettingsOverrideChip_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxEx.Show(
                "CustomSettingNames.xml is being loaded from the application directory instead of the embedded default resource.\r\n\r\n" +
                "这表示自定义设置元数据已被本地覆盖。这可能是有意为之，但名称、分组、描述和数值可能不再与此版本随附的内嵌默认值一致。\r\n\r\n" +
                "Possible consequences:\r\n" +
                "- outdated or mismatched setting names\r\n" +
                "- stale descriptions or value labels\r\n" +
                "- missing newer embedded settings or metadata fixes\r\n" +
                "- grouping differences compared to the embedded defaults\r\n\r\n" +
                "如果并非有意如此，请删除或重命名应用程序文件夹中的外部 CustomSettingNames.xml，以恢复使用内嵌资源。",
                "Custom Settings Override",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void RegisterMainWindowNativeDrop(IntPtr handle)
        {
            if (_mainWindowNativeDropRegistered || handle == IntPtr.Zero)
                return;

            DragAcceptNativeHelper.RevokeDragDrop(handle);

            DragAcceptNativeHelper.ChangeWindowMessageFilter(DragAcceptNativeHelper.WM_DROPFILES, DragAcceptNativeHelper.MSGFLT_ADD);
            DragAcceptNativeHelper.ChangeWindowMessageFilter(DragAcceptNativeHelper.WM_COPYDATA, DragAcceptNativeHelper.MSGFLT_ADD);
            DragAcceptNativeHelper.ChangeWindowMessageFilter(DragAcceptNativeHelper.WM_COPYGLOBALDATA, DragAcceptNativeHelper.MSGFLT_ADD);
            DragAcceptNativeHelper.ChangeWindowMessageFilterEx(handle, DragAcceptNativeHelper.WM_DROPFILES, DragAcceptNativeHelper.MSGFLT_ALLOW, IntPtr.Zero);
            DragAcceptNativeHelper.ChangeWindowMessageFilterEx(handle, DragAcceptNativeHelper.WM_COPYDATA, DragAcceptNativeHelper.MSGFLT_ALLOW, IntPtr.Zero);
            DragAcceptNativeHelper.ChangeWindowMessageFilterEx(handle, DragAcceptNativeHelper.WM_COPYGLOBALDATA, DragAcceptNativeHelper.MSGFLT_ALLOW, IntPtr.Zero);
            DragAcceptNativeHelper.DragAcceptFiles(handle, true);
            _mainWindowNativeDropRegistered = true;
        }

        private bool HandleCopyDataMessage(string message)
        {
            if (string.Equals(message, App.LegacyProfilesImportedMessage, StringComparison.InvariantCulture))
            {
                _viewModel.RefreshCommand.Execute(null);
                _viewModel.ShowSnackbar("配置文件导入成功！", "成功");
                return true;
            }

            var request = App.ParseImportFilesMessage(message);
            if (request.Files.Count == 0)
                return false;

            ImportNipFiles(request.Files, showSuccessNotification: true, forcedMode: request.Mode);
            return true;
        }

        private void ImportNipFiles(IEnumerable<string> files, bool showSuccessNotification, Common.ProfileImportMode? forcedMode = null)
        {
            try
            {
                var fileList = (files ?? Enumerable.Empty<string>()).ToList();
                if (fileList.Count == 0)
                    return;

                // No explicit mode means the import was triggered interactively
                // (drag & drop or a file-association double-click). Only ask merge-vs-replace
                // when an existing profile would be touched - new profiles are just created.
                var mode = forcedMode;
                if (mode == null)
                {
                    if (_viewModel.ImportTargetsExistingProfile(fileList))
                    {
                        mode = UI.Views.Dialogs.ProfileImportModePrompt.Ask(this, fileList.Count);
                        if (mode == null)
                            return; // cancelled
                    }
                    else
                    {
                        mode = Common.ProfileImportMode.Replace;
                    }
                }

                var report = _viewModel.ImportFiles(fileList, mode.Value);
                _viewModel.RefreshCommand.Execute(null);

                if (string.IsNullOrWhiteSpace(report))
                {
                    if (showSuccessNotification)
                        _viewModel.ShowSnackbar("配置文件导入成功！", "成功");
                }
                else
                {
                    MessageBoxViewModel.Show($"部分配置文件无法导入！\r\n\r\n{report}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBoxViewModel.Show($"Import Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenBitEditor(uint settingId, uint value, string settingName)
        {
            var dialog = new UI.Views.Dialogs.BitEditorDialog(settingId, value, settingName);
            dialog.Owner = this;
            dialog.OnValueChanged = (newValue) => _viewModel.SetDwordValue(settingId, newValue);
            dialog.ShowDialog();
        }

        private void AddApplication_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddApplication();
        }

        private void RemoveApplication_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ApplicationItem app)
            {
                _viewModel.RemoveApplication(app);
            }
        }

        private void ModifiedProfilesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.SelectedItem is ModifiedProfileItem profile && combo.IsDropDownOpen)
            {
                _viewModel.SelectProfile(profile.ProfileName);
                combo.SelectedIndex = -1;
            }
        }

        private void ModifiedProfilesCombo_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter
                || sender is not ComboBox combo
                || !combo.IsDropDownOpen)
            {
                return;
            }

            var focusedProfile = GetFocusedModifiedProfileItem() ?? combo.SelectedItem as ModifiedProfileItem;
            if (focusedProfile == null)
            {
                return;
            }

            e.Handled = true;
            combo.IsDropDownOpen = false;
            _viewModel.SelectProfile(focusedProfile.ProfileName);
            combo.SelectedIndex = -1;
        }

        private ModifiedProfileItem GetFocusedModifiedProfileItem()
        {
            DependencyObject current = Keyboard.FocusedElement as DependencyObject;

            while (current != null)
            {
                if (current is ComboBoxItem comboBoxItem && comboBoxItem.DataContext is ModifiedProfileItem profile)
                {
                    return profile;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var items = new List<ActionCardsDialog.ActionCardItem>
            {
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "仅导出当前配置文件",
                    Description = "仅导出当前所选配置文件中您修改过的设置。",
                    IconPath = (Geometry)Application.Current.Resources["IconExport"],
                    IconFill = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                    Command = () => ExportCurrentOnly_Click(null, null)
                },
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "导出当前配置文件（含预定义设置）",
                    Description = "导出当前配置文件的全部设置，包括您未修改过的设置。",
                    IconPath = (Geometry)Application.Current.Resources["IconExport"],
                    IconFill = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                    Command = () => ExportCurrentWithPredefined_Click(null, null)
                },
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "选择要导出的配置文件",
                    Description = "从列表中选择多个已修改的配置文件，导出到单个文件中。",
                    IconPath = (Geometry)Application.Current.Resources["IconUser"],
                    IconFill = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                    ShowProfileSelection = true
                },
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "导出所有自定义配置文件",
                    Description = "导出所有包含用户自定义或已修改设置的配置文件。",
                    IconPath = (Geometry)Application.Current.Resources["IconUser"],
                    IconFill = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                    Command = () => ExportAllCustomized_Click(null, null)
                },
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "导出所有配置文件（NVIDIA 文本格式）",
                    Description = "以 NVIDIA 官方文本格式导出整个配置文件数据库。",
                    IconPath = (Geometry)Application.Current.Resources["IconNvidia"],
                    IconFill = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                    Command = () => ExportAllNVIDIA_Click(null, null)
                }
            };

            var dialog = new ActionCardsDialog("导出配置文件", items, "请选择配置文件的导出方式：");
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
            {
                dialog.SelectedItem.Command?.Invoke();
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var items = new List<ActionCardsDialog.ActionCardItem>
            {
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "替换导入的配置文件",
                    Description = "导入一个或多个 .nip 文件，并用文件内容替换每个所导入配置文件当前的应用程序和设置。",
                    IconPath = (Geometry)Application.Current.Resources["IconImport"],
                    IconFill = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                    Command = () => ImportProfiles_Click(null, null)
                },
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "合并导入的配置文件",
                    Description = "导入一个或多个 .nip 文件，并将其合并到文件中指定的配置文件。目标中的现有值保持不变，除非导入包含相同设置——此时以导入值优先。",
                    IconPath = (Geometry)Application.Current.Resources["IconUser"],
                    IconFill = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                    Command = () => MergeImportedProfiles_Click(null, null)
                },
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "导入（替换）所有驱动配置文件",
                    Description = "从 NVIDIA 文本格式文件中还原或替换所有配置文件。",
                    IconPath = (Geometry)Application.Current.Resources["IconNvidia"],
                    IconFill = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                    Command = () => ImportAllNVIDIA_Click(null, null)
                }
            };

            var dialog = new ActionCardsDialog("导入配置文件", items, "请选择配置文件的导入方式：");
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
            {
                dialog.SelectedItem.Command?.Invoke();
            }
        }

        private void ExportCurrentOnly_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportCurrentProfile(includePredefined: false);
        }

        private void ExportCurrentWithPredefined_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportCurrentProfile(includePredefined: true);
        }

        private async void ExportAllCustomized_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ExportAllCustomizedProfilesAsync();
        }

        private void ExportAllNVIDIA_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportAllProfilesNVIDIAFormat();
        }

        private void ImportProfiles_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ImportProfiles();
        }

        private void MergeImportedProfiles_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.MergeImportedProfiles();
        }

        private void ImportAllNVIDIA_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ImportAllProfilesNVIDIAFormat();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_windowSource != null)
            {
                _windowSource.RemoveHook(WndProc);
                _windowSource = null;
            }

            base.OnClosed(e);
        }
    }
}
