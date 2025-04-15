using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using FuzzySearch = Sirenix.Utilities.Editor.FuzzySearch;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog
    {
        [ShowInInspector]
        private static string _searchPattern;

        [ShowInInspector, LabelText("Groups"), HideReferenceObjectPicker]
        [ListDrawerSettings(ShowPaging = false,
            CustomAddFunction = nameof(AddNewGroup),
            CustomRemoveElementFunction = nameof(RemoveGroup))]
        [OnValueChanged(nameof(ClearCache), includeChildren: true)]
        private AssetGroup[] _normalGroups
        {
            get => FilterGroups(Groups, _searchPattern, false);
            set
            {
                var generatedGroups = _generatedGroups;
                Groups = value.Concat(generatedGroups).ToArray();
            }
        }

        [ShowInInspector, LabelText("Generated Groups"), HideReferenceObjectPicker]
        [ListDrawerSettings(DefaultExpandedState = false, DraggableItems = false, ShowPaging = false)]
        private AssetGroup[] _generatedGroups => FilterGroups(Groups, _searchPattern, true);

        private static AssetGroup[] FilterGroups(AssetGroup[] groups, string searchPattern, bool generated)
        {
            var filtered = groups.Where(x => x.IsGenerated == generated);
            if (string.IsNullOrEmpty(_searchPattern))
                return filtered.ToArray();

            return filtered
                .Select(x =>
                {
                    var match = FuzzySearch.Contains(searchPattern, x.Key.Value, out var score);
                    if (match) return (Group: x, Match: true, Score: score);
                    match = searchPattern == x.BundleId.Name();
                    if (match) return (Group: x, Match: true, Score: 0);
                    return default;
                })
                .Where(x => x.Match)
                .OrderBy(x => x.Score)
                .Select(x => x.Group)
                .ToArray();
        }

        [Button("Build"), ButtonGroup(order: -1)]
        private void Build()
        {
            AddressableBuilder.Build(this, EditorUserBuildSettings.activeBuildTarget);
        }

        [Button("Generate"), ButtonGroup]
        private void GenerateGroups()
        {
            var groups = Groups.Where(x => !x.IsGenerated).ToList(); // keep normal groups

            var gen = new List<AssetGroup>();
            var methods = TypeCache.GetMethodsWithAttribute<AssetGroupGeneratorAttribute>();
            foreach (var method in methods)
            {
                var generatorName = method.Name;
                L.I($"[AddressableCatalog] Generating groups from {generatorName}");
                var meta = method.GetCustomAttribute<AssetGroupGeneratorAttribute>();
                var defs = (IEnumerable<AssetGroupGenerationDef>) method.Invoke(null, null);
                gen.AddRange(defs.Select(def => BuildAssetGroup(def, meta, generatorName)));
            }

            L.I("[AddressableCatalog] Post-processing generated groups");
            gen.ForEach(x =>
            {
                if (TryGetGroup(x.Key, out var orgGroup) is false)
                    return;

                // keep original bundle id, if bundle id is not set. (means no direct bundle access)
                if (x.BundleId is 0)
                {
                    L.I($"[AddressableCatalog] Group {x.Key.Value} already exists. Using original bundle id {orgGroup.BundleId.Name()}");
                    x.BundleId = orgGroup.BundleId;
                }

                // keep LastDependency
                x.LastDependency = orgGroup.LastDependency;
            });

            // sort generated groups (by bundle id, generator id, group key)
            gen.Sort((x, y) =>
            {
                var cmp = x.BundleId.CompareTo(y.BundleId);
                if (cmp != 0) return cmp;
                cmp = string.CompareOrdinal(x.GeneratorName, y.GeneratorName);
                return cmp != 0 ? cmp : string.CompareOrdinal(x.Key.Value, y.Key.Value);
            });

            groups.AddRange(gen);
            Groups = groups.ToArray();
            ClearCache();
            return;

            static AssetGroup BuildAssetGroup(AssetGroupGenerationDef def, AssetGroupGeneratorAttribute meta, string generatorName)
            {
                var groupName = def.GroupName;
                if (groupName is null)
                {
                    groupName = def.Assets.Length is 1
                        ? $"{generatorName}_{Path.GetFileNameWithoutExtension(def.Assets[0].Path)}"
                        : generatorName;
                }

                var group = new AssetGroup(groupName, BuildAssetEntries(def)) { GeneratorName = generatorName, };
                Assert.AreEqual(meta.BundleMajor.HasValue, def.BundleMinor.HasValue,
                    $"BundleStart and BundleSubId must be set together - {generatorName}");
                if (!def.BundleMinor.HasValue)
                {
                    L.I($"[AddressableCatalog] Group created: {groupName}");
                    return group;
                }

                group.BundleId = AssetBundleIdUtils.PackBundleId(meta.BundleMajor!.Value, def.BundleMinor.Value);
                L.I($"[AddressableCatalog] Group created: {groupName}, {group.BundleId.Name()}");
                return group;
            }

            static AssetEntry[] BuildAssetEntries(AssetGroupGenerationDef def)
            {
                return def.Assets
                    .Select(x =>
                    {
                        var guid = AssetDatabase.AssetPathToGUID(x.Path);
                        var fileName = Path.GetFileName(x.Path);
                        Assert.IsFalse(string.IsNullOrEmpty(guid),
                            $"Asset not found: address={x.Address}, path={x.Path}");
                        return new AssetEntry((AssetGUID) guid, x.Address) { HintName = fileName };
                    })
                    .ToArray();
            }
        }

        [Button("Sort"), ButtonGroup]
        private void SortEntries()
        {
            var normalGroups = Groups.Where(x => !x.IsGenerated).ToList();
            normalGroups.Sort((a, b) => a.Key.Value.CompareToOrdinal(b.Key.Value));
            foreach (var group in normalGroups)
                group.SortEntries();
            Groups = normalGroups.Concat(Groups.Where(x => x.IsGenerated)).ToArray();
            // ClearCache(); // no need to clear cache, as the address is not changed
        }

        internal static bool EditMode;

        [Button("$ToggleEditMode_Label"), ButtonGroup]
        private static void ToggleEditMode() => EditMode = !EditMode;
        private static string ToggleEditMode_Label() => EditMode ? "Edit Done" : "Edit";

        [ContextMenu("Reset Hint Name")]
        private void ResetHintName()
        {
            foreach (var group in Groups)
            foreach (var entry in group.Entries)
                entry.ResetHintName();
        }
    }
}