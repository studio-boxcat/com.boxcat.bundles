using System;
using System.Linq;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets
{
    [Serializable]
    public class AssetEntry : ISelfValidator
    {
        [SerializeField, HideInInspector]
        private string _guid;
        public AssetGUID GUID => (AssetGUID) _guid;
        [Delayed]
        public string Address;
        [HideInInspector]
        public string HintName;

        private Object _assetCache;

        [ShowInInspector, Required, AssetsOnly, OnValueChanged(nameof(Asset_OnValueChanged))]
        public Object Asset
        {
            get
            {
                if (_assetCache) return _assetCache;
                return _assetCache = AssetDatabaseUtils.LoadAssetWithGUID<Object>(_guid);
            }
            set
            {
                _guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
                _assetCache = value;
            }
        }

        // Used by Odin Inspector
        [UsedImplicitly]
        public AssetEntry() { }

        public AssetEntry(string guid, string address)
        {
            _guid = guid;
            Address = address;
        }

        public string ResolveAssetPath() => AssetDatabase.GUIDToAssetPath((GUID) GUID);

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