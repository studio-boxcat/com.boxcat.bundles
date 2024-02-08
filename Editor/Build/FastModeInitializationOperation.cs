using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace UnityEditor.AddressableAssets.Settings
{
    internal class FastModeInitializationOperation : AsyncOperationBase<IResourceLocator>
    {
        AddressablesImpl m_addressables;
        AddressableAssetSettings m_settings;
        AsyncOperationHandle<IList<AsyncOperationHandle>> groupOp;

        public FastModeInitializationOperation(AddressablesImpl addressables, AddressableAssetSettings settings)
        {
            m_addressables = addressables;
            m_settings = settings;
            m_addressables.ResourceManager.RegisterForCallbacks();
        }

        internal static T GetBuilderOfType<T>(AddressableAssetSettings settings, bool includeSubclasses) where T : class, IDataBuilder
        {
            System.Type typeToFind = typeof(T);
            if (!includeSubclasses)
            {
                foreach (var db in settings.DataBuilders)
                    if (db.GetType() == typeToFind)
                        return db as T;
                return null;
            }

            ScriptableObject dataBuilder = null;
            foreach (var db in settings.DataBuilders)
            {
                var currentType = db.GetType();
                if (dataBuilder == null)
                {
                    if (currentType == typeToFind || currentType.IsSubclassOf(typeToFind))
                        dataBuilder = db;
                }
                else if (currentType.IsSubclassOf(dataBuilder.GetType()))
                    dataBuilder = db;
            }

            return dataBuilder as T;
        }

        ///<inheritdoc />
        protected override bool InvokeWaitForCompletion()
        {
            if (IsDone)
                return true;

            m_RM?.Update(Time.unscaledDeltaTime);
            if (!HasExecuted)
                InvokeExecute();
            return true;
        }

        protected override void Execute()
        {
            var db = GetBuilderOfType<BuildScriptFastMode>(m_settings, true);
            if (db == null)
                UnityEngine.Debug.Log($"Unable to find {nameof(BuildScriptFastMode)} or subclass builder in settings assets. Using default Instance and Scene Providers.");

            var locator = new AddressableAssetSettingsLocator(m_settings);
            m_addressables.AddResourceLocator(locator);
            m_addressables.AddResourceLocator(new DynamicResourceLocator(m_addressables));

            //NOTE: for some reason, the data builders can get lost from the settings asset during a domain reload - this only happens in tests and custom instance and scene providers are not needed
            m_addressables.InstanceProvider = new InstanceProvider();
            m_addressables.SceneProvider = new SceneProvider();
            m_addressables.ResourceManager.ResourceProviders.Add(new AssetDatabaseProvider());
            m_addressables.ResourceManager.ResourceProviders.Add(new AtlasSpriteProvider());

            Complete(locator, true, null);
        }
    }
}
