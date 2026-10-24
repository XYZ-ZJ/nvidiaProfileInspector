namespace nvidiaProfileInspector.UI.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;

    public static class SettingsListExtensions
    {
        public static void CopyProperties(SettingItemViewModel source, SettingItemViewModel target)
        {
            if (source == null || target == null)
                return;

            target.OriginalItem = source.OriginalItem;

            // When the list is only reordered, source and target can be the same instance.
            // Reapplying value sources in that case invalidates the combo box item source and
            // can clear the currently displayed setting value in the ListView cell.
            if (!ReferenceEquals(source, target))
                target.UpdateValueSources(source.DwordValues, source.QwordValues, source.StringValues, source.BinaryValues);

            // Restore the current display value after refreshing the value source list.
            target.SelectedValue = source.SelectedValue;
            target.IsModified = source.IsModified;

            // Fire PropertyChanged for all derived properties using Reflection
            target.GetType()
            .GetMethod("OnPropertyChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(target, new object[] { string.Empty });
        }

        public static void CopyProperties<T>(T source, T target) where T : class
        {
            if (source == null || target == null)
                return;

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToList();

            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(source);
                    prop.SetValue(target, value);
                }
                catch
                {
                    // Ignore properties that can't be copied
                }
            }
        }

        public static void IncrementalPatchSettingsListOrdered(
        this ObservableCollection<SettingItemViewModel> targetCollection,
        IEnumerable<SettingItemViewModel> sourceList,
        Func<SettingItemViewModel, SettingItemViewModel, bool> matchPredicate = null)
        {
            if (targetCollection == null) throw new ArgumentNullException(nameof(targetCollection));
            if (sourceList == null) throw new ArgumentNullException(nameof(sourceList));

            var sourceItems = sourceList.ToList();
            var oldTargetItems = targetCollection.ToList();

            matchPredicate ??= (s, t) => s.SettingId == t.SettingId;

            var matchedTargets = new HashSet<SettingItemViewModel>();

            // Stage 1: Add missing items and remove non-existing items
            foreach (var sourceItem in sourceItems)
            {
                var matchingTarget = oldTargetItems.FirstOrDefault(t => !matchedTargets.Contains(t) && matchPredicate(sourceItem, t));

                if (matchingTarget != null)
                {
                    CopyProperties(sourceItem, matchingTarget);
                    matchedTargets.Add(matchingTarget);
                }
                else
                {
                    targetCollection.Add(sourceItem);
                }
            }

            // Remove items that don't exist in source
            for (int i = oldTargetItems.Count - 1; i >= 0; i--)
            {
                if (!matchedTargets.Contains(oldTargetItems[i]))
                {
                    targetCollection.Remove(oldTargetItems[i]);
                }
            }

            // Stage 2: Reorder everything to match source order
            int targetIndex = 0;
            foreach (var sourceItem in sourceItems)
            {
                var matchingTarget = targetCollection.FirstOrDefault(t => matchPredicate(sourceItem, t));
                if (matchingTarget != null)
                {
                    int currentIndex = targetCollection.IndexOf(matchingTarget);
                    if (currentIndex != targetIndex)
                    {
                        targetCollection.RemoveAt(currentIndex);
                        targetCollection.Insert(targetIndex, matchingTarget);

                        Debug.WriteLine($"已将 SettingId 为 {matchingTarget.SettingId} 的项从索引 {currentIndex} 移动到 {targetIndex}");
                    }
                    targetIndex++;
                }
            }

            // Debug validation - use current collection, not snapshot
#if DEBUG
            if (sourceItems.Count != targetCollection.Count)
            {
                Debug.WriteLine($"警告：修补后源集合与目标集合的数量不一致。源数量：{sourceItems.Count}，目标数量：{targetCollection.Count}");
            }

            for (int i = 0; i < sourceItems.Count && i < targetCollection.Count; i++)
            {
                if (sourceItems[i].SettingId != targetCollection[i].SettingId)
                {
                    Debug.WriteLine($"警告：索引 {i} 处不匹配。源 SettingId：{sourceItems[i].SettingId}，目标 SettingId：{targetCollection[i].SettingId}");
                }
            }
#endif
        }
    }
}

