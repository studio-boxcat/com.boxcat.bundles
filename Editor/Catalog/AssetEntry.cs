#nullable enable
using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Bundles.Editor
{
    [Serializable, HideReferenceObjectPicker]
    public class AssetEntry : ISelfValidator
    {
        [SerializeField, HideInInspector]
        private string _guid;
        private GUID _guidCache;
        public GUID GUID
        {
            get
            {
                if (_guidCache.Empty())
                    _guidCache = new GUID(_guid);
                return _guidCache;
            }
        }

        [SerializeField, Delayed, TableColumnWidth(200, false), OnValueChanged(nameof(_address_OnValueChanged))]
        private string _address = "";
        public string Address => _address;

        [SerializeField, HideInInspector]
        private Address _hash;
        public Address Hash => _hash;

        [HideInInspector]
        public string HintName = "";

        [NonSerialized]
        private Object? _mainAsset;

        [ShowInInspector, Required, AssetsOnly, OnValueChanged(nameof(MainAsset_OnValueChanged))]
        public Object? MainAsset
        {
            get
            {
                if (_mainAsset is not null) // not null for most cases
                    return _mainAsset;

                var guid = GUID;
                if (guid.Empty()) return null;
                return _mainAsset = AssetDatabase.LoadMainAssetAtGUID(guid);
            }
            set
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out var guid, out long _);
                SetGUID(guid);
                ResetHintName();
                _mainAsset = value;
            }
        }

        // Used by Odin Inspector
        [UsedImplicitly]
        public AssetEntry()
        {
            _guid = "";
        }

        public AssetEntry(GUID guid, string address)
        {
            _guid = guid.ToString();
            _address = address;
            ResetHash(address);
        }

        private void SetGUID(string guid)
        {
            _guid = guid;
            _guidCache = default; // Reset cache
        }

        public void SetAddress(string address)
        {
            _address = address;
            ResetHash(address);
        }

        public Type ResolveAssetType() => AssetDatabase.GetMainAssetTypeFromGUID(GUID);

        public string? ResolveAssetPath()
        {
            if (GUID.Empty()) return null;
            var path = AssetDatabase.GUIDToAssetPath(GUID);
            return path.NotEmpty() ? path : null;
        }

        public void ResetHintName() => HintName = Path.GetFileName(ResolveAssetPath()) ?? "";

        private void _address_OnValueChanged(string value) => ResetHash(value);

        private void ResetHash(string value) => _hash = AddressUtils.Hash(value);

        public TAsset LoadAssetWithType<TAsset>() where TAsset : Object
        {
            var asset = MainAsset;
            if (asset is TAsset a) return a;
            var path = ResolveAssetPath();
            return AssetDatabase.LoadAssetAtPath<TAsset>(path);
        }

        private void MainAsset_OnValueChanged(Object asset)
        {
            _guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MainAsset));
            SetAddress(asset.name);
        }

        void ISelfValidator.Validate(SelfValidationResult result)
        {
            if (_hash != AddressUtils.Hash(_address))
                result.AddError($"The address '{_address}' does not match the hash '{_hash.Val()}'. Please reset the address or reassign the asset.");

            var asset = MainAsset;
            if (asset)
            {
                if (!AssetDatabase.IsMainAsset(asset))
                    result.AddError($"The asset '{asset.name}' is not a main asset. Please assign a main asset.");

                var path = AssetDatabase.GetAssetPath(asset);
                if (!IsPathValidForEntry(path))
                    result.AddError($"The asset '{path}' is not valid for Asset Group '{Address}'.");
            }
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
            if (path.ContainsOrd("/Editor/")
                || path.ContainsOrd("/Resources/")
                || path.ContainsOrd("/Gizmos/"))
                return false;

            // Exclude files with excluded extensions
            if (_excludedExtensions.Any(path.EndsWithOrd))
                return false;

            return true;
        }
    }
}