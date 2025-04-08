using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine.AddressableAssets;
using FuzzySearch = Sirenix.Utilities.Editor.FuzzySearch;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog
    {
        [ShowInInspector]
        private static string _searchPattern;

        [ShowInInspector, LabelText("Groups"), HideReferenceObjectPicker]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = false, ShowPaging = false,
            CustomAddFunction = nameof(AddNewGroup),
            CustomRemoveElementFunction = nameof(RemoveGroup))]
        [OnValueChanged(nameof(ClearCache), includeChildren: true)]
        private AssetGroup[] _normalGroups
        {
            get => FilterGroups(Groups, _searchPattern, false);
            set => throw new NotSupportedException(); // placeholder for odin inspector
        }

        [ShowInInspector, LabelText("Generated Groups"), HideReferenceObjectPicker]
        [ListDrawerSettings(DefaultExpandedState = false, DraggableItems = false, ShowPaging = false)]
        private AssetGroup[] _generatedGroups => FilterGroups(Groups, _searchPattern, true);

        private void AddNewGroup()
        {
            if (string.IsNullOrEmpty(_searchPattern))
                throw new Exception("Search pattern is empty. Please set a search pattern.");

            // Issue new bundle id
            var bundleIdStart = (int) AssetBundleId.BuiltInShader + 1;
            var bundleIdCandidates = Enumerable.Range(bundleIdStart, (int) AssetBundleId.Max - bundleIdStart + 1).ToHashSet();
            foreach (var group in Groups) bundleIdCandidates.Remove((int) group.BundleId);
            var assetBundleId = (AssetBundleId) bundleIdCandidates.First();

            // Insert index = first non-generated group
            var i = Groups.Length - 1;
            for (; i >= 0; i--)
            {
                if (!Groups[i].IsGenerated)
                    break;
            }
            var insertIndex = i + 1;

            // Create new group
            var newGroup = new AssetGroup(_searchPattern, Array.Empty<AssetEntry>()) { BundleId = assetBundleId };
            var groups = Groups.ToList();
            groups.Insert(insertIndex, newGroup);
            Groups = groups.ToArray();
        }

        private void RemoveGroup(AssetGroup group)
        {
            if (group.IsGenerated)
                throw new Exception("Cannot remove generated group.");

            var groups = Groups.ToList();
            groups.Remove(group);
            Groups = groups.ToArray();
        }

        private static AssetGroup[] FilterGroups(AssetGroup[] groups, string searchPattern, bool generated)
        {
            var filtered = groups.Where(x => x.IsGenerated == generated);
            if (string.IsNullOrEmpty(_searchPattern))
                return filtered.ToArray();

            return filtered
                .Select(x =>
                {
                    var match = FuzzySearch.Contains(searchPattern, x.Key.Value, out var score);
                    return (Group: x, Match: match, Score: score);
                })
                .Where(x => x.Match)
                .OrderBy(x => x.Score)
                .Select(x => x.Group)
                .ToArray();
        }

        [Button, ButtonGroup(order: -1)]
        private void GenerateGroups()
        {
            var groups = Groups.Where(x => !x.IsGenerated).ToList(); // keep normal groups

            var generatedGroups = new List<AssetGroup>();
            var methods = TypeCache.GetMethodsWithAttribute<AssetGroupGeneratorAttribute>();
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<AssetGroupGeneratorAttribute>();
                var generatorId = attr.GeneratorId;
                var oldCount = generatedGroups.Count;
                method.Invoke(null, parameters: new object[] { generatedGroups });
                for (var i = oldCount; i < generatedGroups.Count; i++)
                    generatedGroups[i].GeneratorId = generatorId;
            }

            generatedGroups.Sort((x, y) =>
            {
                var cmp = string.CompareOrdinal(x.GeneratorId, y.GeneratorId);
                return cmp != 0 ? cmp : string.CompareOrdinal(x.Key.Value, y.Key.Value);
            });

            groups.AddRange(generatedGroups);
            Groups = groups.ToArray();
            ClearCache();
        }

        internal static bool EditNameEnabled;

        [Button("$ToggleEditName_Label"), ButtonGroup]
        private static void ToggleEditName() => EditNameEnabled = !EditNameEnabled;
        private static string ToggleEditName_Label() => EditNameEnabled ? "Edit Name Done" : "Edit Name";
    }
}