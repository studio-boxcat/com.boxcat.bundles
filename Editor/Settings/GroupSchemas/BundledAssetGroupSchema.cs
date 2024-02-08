using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Settings.GroupSchemas
{
    /// <summary>
    /// Schema used for bundled asset groups.
    /// </summary>
//    [CreateAssetMenu(fileName = "BundledAssetGroupSchema.asset", menuName = "Addressables/Group Schemas/Bundled Assets")]
    [DisplayName("Content Packing & Loading")]
    public class BundledAssetGroupSchema : AddressableAssetGroupSchema
    {
        /// <summary>
        /// Defines how bundles are created.
        /// </summary>
        public enum BundlePackingMode
        {
            /// <summary>
            /// Creates a bundle for all non-scene entries and another for all scenes entries.
            /// </summary>
            PackTogether,

            /// <summary>
            /// Creates a bundle per entry.  This is useful if each entry is a folder as all sub entries will go to the same bundle.
            /// </summary>
            PackSeparately,
        }

        /// <summary>
        /// Defines how internal bundles are named. This is used for both caching and for inter-bundle dependecies.  If possible, GroupGuidProjectIdHash should be used as it is stable and unique between projects.
        /// </summary>
        public enum BundleInternalIdMode
        {
            /// <summary>
            /// Use the guid of the group asset
            /// </summary>
            GroupGuid,

            /// <summary>
            /// Use the hash of the group asset guid and the project id
            /// </summary>
            GroupGuidProjectIdHash,

            /// <summary>
            /// Use the hash of the group asset, the project id and the guids of the entries in the group
            /// </summary>
            GroupGuidProjectIdEntriesHash
        }

        /// <summary>
        /// Options for compressing bundles in this group.
        /// </summary>
        public enum BundleCompressionMode
        {
            /// <summary>
            /// Use to indicate that bundles will not be compressed.
            /// </summary>
            Uncompressed,

            /// <summary>
            /// Use to indicate that bundles will be compressed using the LZ4 compression algorithm.
            /// </summary>
            LZ4,

            /// <summary>
            /// Use to indicate that bundles will be compressed using the LZMA compression algorithm.
            /// </summary>
            LZMA
        }

        [SerializeField]
        BundleInternalIdMode m_InternalBundleIdMode = BundleInternalIdMode.GroupGuidProjectIdHash;

        /// <summary>
        /// Internal bundle naming mode
        /// </summary>
        public BundleInternalIdMode InternalBundleIdMode
        {
            get => m_InternalBundleIdMode;
            set
            {
                if (m_InternalBundleIdMode != value)
                {
                    m_InternalBundleIdMode = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        BundleCompressionMode m_Compression = BundleCompressionMode.LZ4;

        /// <summary>
        /// Build compression.
        /// </summary>
        public BundleCompressionMode Compression
        {
            get => m_Compression;
            set
            {
                if (m_Compression != value)
                {
                    m_Compression = value;
                    SetDirty(true);
                }
            }
        }

        [SerializeField]
        bool m_IncludeAddressInCatalog = true;

        /// <summary>
        /// If enabled, addresses are included in the content catalog.  This is required if assets are to be loaded via their main address.
        /// </summary>
        public bool IncludeAddressInCatalog
        {
            get => m_IncludeAddressInCatalog;
            set
            {
                if (m_IncludeAddressInCatalog != value)
                {
                    m_IncludeAddressInCatalog = value;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// Gets the build compression settings for bundles in this group.
        /// </summary>
        /// <param name="bundleId">The bundle id.</param>
        /// <returns>The build compression.</returns>
        public virtual BuildCompression GetBuildCompressionForBundle(string bundleId)
        {
            //Unfortunately the BuildCompression struct is not serializable (nor is it settable), therefore this enum needs to be used to return the static members....
            switch (m_Compression)
            {
                case BundleCompressionMode.Uncompressed: return BuildCompression.Uncompressed;
                case BundleCompressionMode.LZ4: return BuildCompression.LZ4;
                case BundleCompressionMode.LZMA: return BuildCompression.LZMA;
            }

            return default(BuildCompression);
        }

        [FormerlySerializedAs("m_includeInBuild")]
        [SerializeField]
        [Tooltip("If true, the assets in this group will be included in the build of bundles.")]
        bool m_IncludeInBuild = true;

        /// <summary>
        /// If true, the assets in this group will be included in the build of bundles.
        /// </summary>
        public bool IncludeInBuild
        {
            get => m_IncludeInBuild;
            set
            {
                if (m_IncludeInBuild != value)
                {
                    m_IncludeInBuild = value;
                    SetDirty(true);
                }
            }
        }

        [FormerlySerializedAs("m_bundleMode")]
        [SerializeField]
        [Tooltip(
            "Controls how bundles are packed.  If set to PackTogether, a single asset bundle will be created for the entire group, with the exception of scenes, which are packed in a second bundle.  If set to PackSeparately, an asset bundle will be created for each entry in the group; in the case that an entry is a folder, one bundle is created for the folder and all of its sub entries.")]
        BundlePackingMode m_BundleMode = BundlePackingMode.PackTogether;

        /// <summary>
        /// Controls how bundles are packed.  If set to PackTogether, a single asset bundle will be created for the entire group, with the exception of scenes, which are packed in a second bundle.  If set to PackSeparately, an asset bundle will be created for each entry in the group; in the case that an entry is a folder, one bundle is created for the folder and all of its sub entries.
        /// </summary>
        public BundlePackingMode BundleMode
        {
            get => m_BundleMode;
            set
            {
                if (m_BundleMode != value)
                {
                    m_BundleMode = value;
                    SetDirty(true);
                }
            }
        }

        /// <summary>
        /// Used to determine if dropdown should be custom
        /// </summary>
        private bool m_UseCustomPaths = false;

        /// <summary>
        /// Set default values taken from the assigned group.
        /// </summary>
        /// <param name="group">The group this schema has been added to.</param>
        protected override void OnSetGroup(AddressableAssetGroup group)
        {
            //this can happen during the load of the addressables asset
        }

        /// <summary>
        /// Used to determine how the final bundle name should look.
        /// </summary>
        public enum BundleNamingStyle
        {
            /// <summary>
            /// Use to indicate that the hash should be appended to the bundle name.
            /// </summary>
            AppendHash,

            /// <summary>
            /// Use to indicate that the bundle name should not contain the hash.
            /// </summary>
            NoHash,

            /// <summary>
            /// Use to indicate that the bundle name should only contain the given hash.
            /// </summary>
            OnlyHash,

            /// <summary>
            /// Use to indicate that the bundle name should only contain the hash of the file name.
            /// </summary>
            FileNameHash
        }

        /// <summary>
        /// Used to draw the Bundle Naming popup
        /// </summary>
        [CustomPropertyDrawer(typeof(BundleNamingStyle))]
        class BundleNamingStylePropertyDrawer : PropertyDrawer
        {
            /// <summary>
            /// Custom Drawer for the BundleNamingStyle in order to display easier to understand display names.
            /// </summary>
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                DrawGUI(position, property, label);
            }

            internal static int DrawGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                bool showMixedValue = EditorGUI.showMixedValue;
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.showMixedValue = showMixedValue;

                GUIContent[] contents = new GUIContent[4];
                contents[0] = new GUIContent("Filename", "Leave filename unchanged.");
                contents[1] = new GUIContent("Append Hash to Filename", "Append filename with the AssetBundle content hash.");
                contents[2] = new GUIContent("Use Hash of AssetBundle", "Replace filename with AssetBundle hash.");
                contents[3] = new GUIContent("Use Hash of Filename", "Replace filename with hash of filename.");

                int enumValue = property.enumValueIndex;
                enumValue = enumValue == 0 ? 1 : enumValue == 1 ? 0 : enumValue;

                EditorGUI.BeginChangeCheck();
                int newValue = EditorGUI.Popup(position, new GUIContent(label.text, "Controls how the output AssetBundle's will be named."), enumValue, contents);
                if (EditorGUI.EndChangeCheck())
                {
                    newValue = newValue == 0 ? 1 : newValue == 1 ? 0 : newValue;
                    property.enumValueIndex = newValue;
                }

                EditorGUI.EndProperty();
                return newValue;
            }
        }

        [SerializeField]
        BundleNamingStyle m_BundleNaming;

        /// <summary>
        /// Naming style to use for generated AssetBundle(s).
        /// </summary>
        public BundleNamingStyle BundleNaming
        {
            get => m_BundleNaming;
            set
            {
                if (m_BundleNaming != value)
                {
                    m_BundleNaming = value;
                    SetDirty(true);
                }
            }
        }

        private bool m_ShowPaths = true;

        /// <summary>
        /// Used for drawing properties in the inspector.
        /// </summary>
        public override void ShowAllProperties()
        {
            m_ShowPaths = true;
            AdvancedOptionsFoldout.IsActive = true;
        }

        /// <inheritdoc/>
        public override void OnGUI()
        {
            AdvancedOptionsFoldout.IsActive = GUI.AddressablesGUIUtility.FoldoutWithHelp(AdvancedOptionsFoldout.IsActive, new GUIContent("Advanced Options"), () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("editor/groups/ContentPackingAndLoadingSchema.html#advanced-options");
                Application.OpenURL(url);
            });
            if (AdvancedOptionsFoldout.IsActive)
                ShowAdvancedProperties(SchemaSerializedObject);
            SchemaSerializedObject.ApplyModifiedProperties();
        }

        /// <inheritdoc/>
        public override void OnGUIMultiple(List<AddressableAssetGroupSchema> otherSchemas)
        {
            List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges = null;

            List<BundledAssetGroupSchema> otherBundledSchemas = new List<BundledAssetGroupSchema>();
            foreach (var schema in otherSchemas)
            {
                otherBundledSchemas.Add(schema as BundledAssetGroupSchema);
            }

            foreach (var schema in otherBundledSchemas)
                schema.m_ShowPaths = m_ShowPaths;

            EditorGUI.BeginChangeCheck();
            AdvancedOptionsFoldout.IsActive = GUI.AddressablesGUIUtility.BeginFoldoutHeaderGroupWithHelp(AdvancedOptionsFoldout.IsActive, new GUIContent("Advanced Options"), () =>
            {
                string url = AddressableAssetUtility.GenerateDocsURL("editor/groups/ContentPackingAndLoadingSchema.html#advanced-options");
                Application.OpenURL(url);
            }, 10);
            if (AdvancedOptionsFoldout.IsActive)
            {
                ShowAdvancedPropertiesMulti(SchemaSerializedObject, otherSchemas, ref queuedChanges);
            }

            EditorGUI.EndFoldoutHeaderGroup();

            SchemaSerializedObject.ApplyModifiedProperties();
            if (queuedChanges != null)
            {
                Undo.SetCurrentGroupName("bundledAssetGroupSchemasUndos");
                foreach (var schema in otherBundledSchemas)
                    Undo.RecordObject(schema, "BundledAssetGroupSchema" + schema.name);

                foreach (var change in queuedChanges)
                {
                    foreach (var schema in otherBundledSchemas)
                        change.Invoke(this, schema);
                }
            }

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }

        static GUI.FoldoutSessionStateValue AdvancedOptionsFoldout = new GUI.FoldoutSessionStateValue("Addressables.BundledAssetGroup.AdvancedOptions");

        GUIContent m_CompressionContent = new GUIContent("Asset Bundle Compression", "Compression method to use for asset bundles.");
        GUIContent m_IncludeInBuildContent = new GUIContent("Include in Build", "If disabled, these bundles will not be included in the build.");

        GUIContent m_IncludeAddressInCatalogContent = new GUIContent("Include Addresses in Catalog",
            "If disabled, addresses from this group will not be included in the catalog.  This is useful for reducing the size of the catalog if addresses are not needed.");

        GUIContent m_InternalBundleIdModeContent = new GUIContent("Internal Bundle Id Mode",
            $"Specifies how the internal id of the bundle is generated.  This must be set to {BundleInternalIdMode.GroupGuid} or {BundleInternalIdMode.GroupGuidProjectIdHash} to ensure proper caching on device.");

        GUIContent m_BundleModeContent = new GUIContent("Bundle Mode", "Controls how bundles are created from this group.");
        GUIContent m_BundleNamingContent = new GUIContent("Bundle Naming Mode", "Controls the final file naming mode for bundles in this group.");


        void ShowAdvancedProperties(SerializedObject so)
        {
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_Compression)), m_CompressionContent, true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_IncludeInBuild)), m_IncludeInBuildContent, true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_IncludeAddressInCatalog)), m_IncludeAddressInCatalogContent, true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_InternalBundleIdMode)), m_InternalBundleIdModeContent, true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_BundleMode)), m_BundleModeContent, true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(m_BundleNaming)), m_BundleNamingContent, true);
        }

        void ShowAdvancedPropertiesMulti(SerializedObject so, List<AddressableAssetGroupSchema> otherBundledSchemas, ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges)
        {
            ShowSelectedPropertyMulti(so, nameof(m_Compression), m_CompressionContent, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.Compression = src.Compression, ref m_Compression);
            ShowSelectedPropertyMulti(so, nameof(m_IncludeInBuild), m_IncludeInBuildContent, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.IncludeInBuild = src.IncludeInBuild,
                ref m_IncludeInBuild);
            ShowSelectedPropertyMulti(so, nameof(m_IncludeAddressInCatalog), m_IncludeAddressInCatalogContent, otherBundledSchemas, ref queuedChanges,
                (src, dst) => dst.IncludeAddressInCatalog = src.IncludeAddressInCatalog, ref m_IncludeAddressInCatalog);
            ShowSelectedPropertyMulti(so, nameof(m_InternalBundleIdMode), m_InternalBundleIdModeContent, otherBundledSchemas, ref queuedChanges,
                (src, dst) => dst.InternalBundleIdMode = src.InternalBundleIdMode, ref m_InternalBundleIdMode);
            ShowSelectedPropertyMulti(so, nameof(m_BundleMode), m_BundleModeContent, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.BundleMode = src.BundleMode, ref m_BundleMode);
            ShowSelectedPropertyMulti(so, nameof(m_BundleNaming), m_BundleNamingContent, otherBundledSchemas, ref queuedChanges, (src, dst) => dst.BundleNaming = src.BundleNaming, ref m_BundleNaming);
        }

        void ShowSelectedPropertyMulti<T>(SerializedObject so, string propertyName, GUIContent label, List<AddressableAssetGroupSchema> otherSchemas,
            ref List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>> queuedChanges, Action<BundledAssetGroupSchema, BundledAssetGroupSchema> a, ref T propertyValue)
        {
            SerializedProperty serializedProperty = so.FindProperty(propertyName);
            Type propertySystemType = typeof(T);
            if (label == null)
                label = new GUIContent(serializedProperty.displayName);
            ShowMixedValue(serializedProperty, otherSchemas, propertySystemType, propertyName);

            T newValue = default(T);
            SerializedPropertyType serializedPropertyType = SerializedPropertyType.Generic;
            EditorGUI.BeginChangeCheck();
            if (propertySystemType == typeof(bool))
            {
                newValue = (T)(object)EditorGUILayout.Toggle(label, (bool)(object)propertyValue);
                serializedPropertyType = SerializedPropertyType.Boolean;
            }
            else if (propertySystemType.IsEnum)
            {
                serializedPropertyType = SerializedPropertyType.Enum;
                if (propertySystemType == typeof(BundleNamingStyle))
                {
                    Rect rect = EditorGUILayout.GetControlRect();
                    int enumValue = BundleNamingStylePropertyDrawer.DrawGUI(rect, serializedProperty, label);
                    newValue = (T)(object)enumValue;
                }
                else
                {
                    int enumValue = Convert.ToInt32(EditorGUILayout.EnumPopup(label, (Enum)(object)propertyValue));
                    newValue = (T)(object)enumValue;
                }
            }
            else if (propertySystemType == typeof(int))
            {
                newValue = (T)(object)EditorGUILayout.IntField(label, (int)(object)propertyValue);
                serializedPropertyType = SerializedPropertyType.Integer;
            }
            else
            {
                EditorGUILayout.PropertyField(serializedProperty, label, true);
                so.ApplyModifiedProperties();
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (serializedPropertyType != SerializedPropertyType.Generic)
                {
                    HashSet<SerializedProperty> properties = new HashSet<SerializedProperty>() {serializedProperty};
                    foreach (AddressableAssetGroupSchema otherSchema in otherSchemas)
                        properties.Add(otherSchema.SchemaSerializedObject.FindProperty(propertyName));

                    foreach (SerializedProperty propertyForValueDestination in properties)
                    {
                        var destinationSerializedObject = propertyForValueDestination.serializedObject;
                        switch (serializedPropertyType)
                        {
                            case SerializedPropertyType.Boolean:
                                propertyForValueDestination.boolValue = (bool)(object)newValue;
                                break;
                            case SerializedPropertyType.Integer:
                                propertyForValueDestination.intValue = (int)(object)newValue;
                                break;
                            case SerializedPropertyType.Enum:
                                propertyForValueDestination.enumValueIndex = (int)(object)newValue;
                                break;
                        }

                        destinationSerializedObject.ApplyModifiedProperties();
                    }
                }
                else if (a != null)
                {
                    if (queuedChanges == null)
                        queuedChanges = new List<Action<BundledAssetGroupSchema, BundledAssetGroupSchema>>();
                    queuedChanges.Add(a);
                }
            }

            EditorGUI.showMixedValue = false;
        }
    }
}
