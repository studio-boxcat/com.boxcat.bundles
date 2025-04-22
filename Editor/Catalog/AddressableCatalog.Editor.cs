using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using FuzzySearch = Sirenix.Utilities.Editor.FuzzySearch;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog
    {
        [ShowInInspector, OnValueChanged("_searchPattern_OnValueChanged")]
        private static string _searchPattern;
        private static bool _isSearching => !string.IsNullOrEmpty(_searchPattern);

        [ShowInInspector, LabelText("Groups"), HideReferenceObjectPicker, HideIf("_isSearching")]
        [ListDrawerSettings(ShowPaging = false, DraggableItems = false,
            CustomAddFunction = nameof(AddNewGroup),
            CustomRemoveElementFunction = nameof(RemoveGroup))]
        [OnValueChanged(nameof(ClearCache), includeChildren: true)]
        private AssetGroup[] _normalGroups
        {
            get => Groups.Where(x => !x.IsGenerated).ToArray();
            set
            {
                var generatedGroups = _generatedGroups;
                Groups = value.Concat(generatedGroups).ToArray();
            }
        }

        [ShowInInspector, LabelText("Generated Groups"), HideReferenceObjectPicker, HideIf("_isSearching")]
        [ListDrawerSettings(DefaultExpandedState = false, DraggableItems = false, ShowPaging = true)]
        private AssetGroup[] _generatedGroups => Groups.Where(x => x.IsGenerated).ToArray();

        [ShowInInspector, LabelText("Search"), HideReferenceObjectPicker, ShowIf("_isSearching")]
        [TableList(AlwaysExpanded = true, ShowPaging = true, NumberOfItemsPerPage = 20)]
        [OnCollectionChanged(Before = nameof(_searchedEntries_OnCollectionChanged_Before))]
        private List<SearchedEntry> _searchedEntries;

        private void _searchPattern_OnValueChanged()
        {
            _searchedEntries ??= new List<SearchedEntry>();
            _searchedEntries.Clear();
            foreach (var group in Groups)
            foreach (var entry in group.Entries)
            {
                var content = entry.Address + entry.HintName + group.Key.Value + group.BundleId.Name();
                var match = FuzzySearch.Contains(_searchPattern, content, out var score);
                if (match) _searchedEntries.Add(new SearchedEntry(group, entry, score));
            }

            _searchedEntries.Sort((a, b) =>
            {
                var cmp = b.Score.CompareTo(a.Score);
                if (cmp != 0) return cmp;
                return a.AssetGroup.BundleId - b.AssetGroup.BundleId;
            });
        }

        private void _searchedEntries_OnCollectionChanged_Before(CollectionChangeInfo change)
        {
            if (change.ChangeType is not CollectionChangeType.RemoveIndex)
                return;

            Undo.RecordObject(this, "Remove Searched Entry");
            var e = _searchedEntries[change.Index];
            var group = e.AssetGroup;
            group.Internal_RemoveEntry(e.AssetEntry);
            ClearCache();
            EditorUtility.SetDirty(this);
        }

        [Button("Build"), ButtonGroup(order: -1)]
        private void Build()
        {
            AddressableBuilder.Build(this, EditorUserBuildSettings.activeBuildTarget);
        }

        [Button("Generate"), ButtonGroup]
        private void GenerateGroupsAndCodes()
        {
            GenerateGroups();
            AddressablesCodeGenerator.GenerateCode(this, AssetDatabase.GUIDToAssetPath("73dee367a1284cea987dd7ac55b7b5e9"));
        }

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

        [ShowInInspector]
        private readonly struct SearchedEntry
        {
            [HideInInspector] public readonly AssetGroup AssetGroup;
            [HideInInspector] public readonly AssetEntry AssetEntry;
            [HideInInspector] public readonly int Score;

            [ShowInInspector, DisplayAsString]
            [TableColumnWidth(120, false)]
            public string Group
            {
                get => AssetGroup.Key.Value;
                // for odin inspector
                set => throw new NotSupportedException("Cannot set group name.");
            }

            [ShowInInspector, DisplayAsString]
            [TableColumnWidth(40, false)]
            public string Bundle
            {
                get => AssetGroup.BundleId.Name();
                // for odin inspector
                set => throw new NotSupportedException("Cannot set bundle name.");
            }

            [ShowInInspector, OnValueChanged("ClearGroupCache"), DisableIf(nameof(_isGenerated))]
            public string Address
            {
                get => AssetEntry.Address;
                set => AssetEntry.Address = value;
            }

            [ShowInInspector, OnValueChanged("ClearGroupCache"), DisableIf(nameof(_isGenerated))]
            public Object MainAsset
            {
                get => AssetEntry.MainAsset;
                set => AssetEntry.MainAsset = value;
            }

            private bool _isGenerated => AssetGroup.IsGenerated;

            public SearchedEntry(AssetGroup group, AssetEntry entry, int score)
            {
                AssetGroup = group;
                AssetEntry = entry;
                Score = score;
            }

            private void ClearGroupCache() => AssetGroup.ClearCache();
        }
    }
}