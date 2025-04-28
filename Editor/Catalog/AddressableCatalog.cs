using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog : ScriptableObject
    {
        [FormerlySerializedAs("AssetGroups")]
        [SerializeField, HideInInspector]
        public AssetGroup[] Groups;

        private void AddNewGroup()
        {
            // Issue new bundle id
            var bundleIdStart = (int) AssetBundleId.MonoScript + 1;
            var bundleIdMax = (int) AssetBundleIdUtils.MaxForNormalBundle();
            var bundleIdCandidates = Enumerable.Range(bundleIdStart, bundleIdMax - bundleIdStart + 1).ToHashSet();
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
            var newGroup = new AssetGroup("New Group", Array.Empty<AssetEntry>()) { BundleId = assetBundleId };
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

        public void AddEntries(AssetGroup group, AssetGUID[] guids)
        {
            Assert.IsFalse(group.IsGenerated, "Cannot add entry to generated group.");
            Undo.RecordObject(this, "Add Entries");

            foreach (var g in Groups)
            {
                if (g == group) continue;
                g.Internal_RemoveEntries(guids);
            }

            group.Internal_AddEntries(guids);
            EditorUtility.SetDirty(this);
            ClearCache();
        }

        public IEnumerable<AssetEntry> TraverseEntries_AddressAccess()
        {
            foreach (var group in Groups)
            {
                if (group.BundleId.AddressAccess() is false)
                    continue;

                foreach (var entry in group.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Address) is false)
                        yield return entry;
                }
            }
        }

        internal AssetBundleBuild[] GenerateBundleBuilds()
        {
            var builds = new AssetBundleBuild[Groups.Length];
            for (var i = 0; i < Groups.Length; i++)
                builds[i] = Groups[i].GenerateAssetBundleBuild();
            return builds;
        }
    }
}