namespace nvidiaProfileInspector.UI.Views.Dialogs
{
    using nvidiaProfileInspector.Common;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Media;

    /// <summary>
    /// Asks the user how an interactively triggered .nip import should be applied
    /// (drag &amp; drop or file-association double-click). Returns null when cancelled.
    /// </summary>
    public static class ProfileImportModePrompt
    {
        public static ProfileImportMode? Ask(Window owner, int fileCount)
        {
            var noun = fileCount == 1 ? "profile" : "profiles";
            var items = new List<ActionCardsDialog.ActionCardItem>
            {
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "替换",
                    Description = $"重置每个目标{noun}，仅写入导入的应用程序和设置。",
                    IconPath = Application.Current.Resources["IconImport"] as Geometry,
                    IconFill = Application.Current.Resources["TextSecondaryBrush"] as Brush,
                },
                new ActionCardsDialog.ActionCardItem
                {
                    Title = "合并",
                    Description = $"保留每个目标{noun}中的现有值，仅覆盖导入中包含的设置。",
                    IconPath = Application.Current.Resources["IconUser"] as Geometry,
                    IconFill = Application.Current.Resources["TextSecondaryBrush"] as Brush,
                },
            };

            var subtitle = fileCount == 1
                ? "请选择此 .nip 文件的导入方式："
                : $"请选择这 {fileCount} 个 .nip 文件的导入方式：";

            var dialog = new ActionCardsDialog("导入配置文件", items, subtitle);
            if (owner != null && owner.IsLoaded)
                dialog.Owner = owner;
            else
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            if (dialog.ShowDialog() != true || dialog.SelectedItem == null)
                return null;

            return dialog.SelectedItem.Title == "合并"
                ? ProfileImportMode.Merge
                : ProfileImportMode.Replace;
        }
    }
}
