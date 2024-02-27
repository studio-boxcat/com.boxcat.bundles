using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UnityEditor.AddressableAssets
{
    /// <summary>
    /// Class used to get and set the default Addressable Asset settings object.
    /// </summary>
    public class AddressableDefaultSettings : ScriptableObject
    {
        [SerializeField]
        internal string m_AddressableAssetSettingsGuid;

        static AddressableAssetSettings _settings;

        /// <summary>
        /// Gets the default addressable asset settings object.  This will return null during editor startup if EditorApplication.isUpdating or EditorApplication.isCompiling are true.
        /// </summary>
        public static AddressableAssetSettings Settings
        {
            get
            {
                if (_settings is not null)
                    return _settings;
                if (EditorBuildSettings.TryGetConfigObject("com.unity.addressableassets", out AddressableDefaultSettings so))
                    return _settings = LoadSettingsObject(so.m_AddressableAssetSettingsGuid);
                return null;
            }
        }

        static bool _loading = false;

        static AddressableAssetSettings LoadSettingsObject(string guid)
        {
            //prevent re-entrant stack overflow
            if (_loading)
            {
                Debug.LogWarning("Detected stack overflow when accessing AddressableAssetSettingsDefaultObject.Settings object.");
                return null;
            }

            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("Invalid guid for default AddressableAssetSettings object.");
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogErrorFormat("Unable to determine path for default AddressableAssetSettings object with guid {0}.", guid);
                return null;
            }

            _loading = true;
            var settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(path);
            _loading = false;
            return settings;
        }
    }
}