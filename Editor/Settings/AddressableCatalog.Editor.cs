using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine.AddressableAssets;

namespace UnityEditor.AddressableAssets
{
    public partial class AddressableCatalog
    {
        [ShowInInspector, LabelText("Groups"), HideReferenceObjectPicker]
        [ListDrawerSettings(DraggableItems = false, ShowPaging = false, ShowFoldout = false)]
        [OnValueChanged(nameof(ClearCache), includeChildren: true)]
        [CustomContextMenu("Toggle Edit Name", nameof(ToggleEditName))]
        public AssetGroup[] NormalGroups
        {
            get => Groups
                .Where(x => !x.IsGenerated)
                .ToArray();
            set => Groups = value.Concat(Groups.Where(x => x.IsGenerated)).ToArray();
        }

        [ShowInInspector, LabelText("Generated Groups"), ReadOnly, HideReferenceObjectPicker]
        [ListDrawerSettings(DefaultExpandedState = false, DraggableItems = false, ShowPaging = false)]
        public AssetGroup[] GeneratedGroups => Groups
            .Where(x => x.IsGenerated)
            .ToArray();

        [Button, PropertyOrder(-1)]
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

        [Button, PropertyOrder(-1)]
        private void AssignBundleId()
        {
            var bundleId = AssetBundleId.BuiltInShader + 1;
            foreach (var group in Groups)
                group.BundleId = bundleId++;
        }

        internal static bool EditNameEnabled;
        private static void ToggleEditName() => EditNameEnabled = !EditNameEnabled;
    }
}