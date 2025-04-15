using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public class AssetEntry : ISelfValidator
    {
        [SerializeField, HideInInspector]
        private string _guid;
        public AssetGUID GUID => (AssetGUID) _guid;
        [Delayed, TableColumnWidth(160, false)]
        public string Address = "";
        [HideInInspector]
        public string HintName;

        private Object _assetCache;

        [ShowInInspector, Required, AssetsOnly, OnValueChanged(nameof(Asset_OnValueChanged))]
        public Object Asset
        {
            get
            {
                if (_assetCache) return _assetCache;
                if (string.IsNullOrEmpty(_guid)) return null;
                return _assetCache = AssetDatabase.LoadMainAssetAtGUID(new GUID(_guid));
            }
            set
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out _guid, out long _);
                ResetHintName();
                _assetCache = value;
            }
        }

        // Used by Odin Inspector
        [UsedImplicitly]
        public AssetEntry() { }

        public AssetEntry(AssetGUID guid, string address)
        {
            _guid = guid.Value;
            Address = address;
        }

        public Type ResolveAssetType() => AssetDatabase.GetMainAssetTypeFromGUID((GUID) GUID);

        public string ResolveAssetPath() => AssetDatabase.GUIDToAssetPath((GUID) GUID);

        public void ResetHintName() => HintName = Path.GetFileName(ResolveAssetPath());

        public TAsset LoadAssetWithType<TAsset>() where TAsset : Object
        {
            var asset = Asset;
            if (asset is TAsset a) return a;
            var path = ResolveAssetPath();
            return AssetDatabase.LoadAssetAtPath<TAsset>(path);
        }

        private void Asset_OnValueChanged(Object asset)
        {
            _guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Asset));
            Address = asset.name;
        }

        void ISelfValidator.Validate(SelfValidationResult result)
        {
            var asset = Asset;
            if (!asset) return;

            if (!AssetDatabase.IsMainAsset(asset))
                result.AddError($"The asset '{asset.name}' is not a main asset. Please assign a main asset.");

            var path = AssetDatabase.GetAssetPath(asset);
            if (!IsPathValidForEntry(path))
                result.AddError($"The asset '{path}' is not valid for Addressable Asset Group '{Address}'.");
        }

        private static readonly string[] _excludedExtensions = { ".cs", ".dll", ".meta", ".preset", ".asmdef", ".asmref" };

        private static bool IsPathValidForEntry(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Exclude files that are not in the Assets or Packages folder
            if (!path.StartsWithOrdinal("Assets/")
                && !path.StartsWithOrdinal("Packages/"))
                return false;

            // Exclude Editor, Resources, and Gizmos folders
            if (path.ContainsOrdinal("/Editor/")
                || path.ContainsOrdinal("/Resources/")
                || path.ContainsOrdinal("/Gizmos/"))
                return false;

            // Exclude files with excluded extensions
            if (_excludedExtensions.Any(path.EndsWithOrdinal))
                return false;

            return true;
        }
    }
}