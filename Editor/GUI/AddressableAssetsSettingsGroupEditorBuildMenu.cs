using System;
using System.IO;
using System.Text;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;

namespace UnityEditor.AddressableAssets.Build
{
    /// <summary>
    /// Makes a new build of Addressables using the BuildScript selectable in the menu
    /// </summary>
    public class AddressablesBuildMenuNewBuild : AddressableAssetsSettingsGroupEditor.IAddressablesBuildMenu
    {
        /// <inheritdoc />
        public virtual string BuildMenuPath
        {
            get => "New Build";
        }

        /// <inheritdoc />
        public virtual bool SelectableBuildScript
        {
            get => true;
        }

        /// <inheritdoc />
        public virtual int Order
        {
            get => -20;
        }

        /// <inheritdoc />
        public virtual bool OnPrebuild(AddressablesDataBuilderInput input)
        {
            return true;
        }

        /// <inheritdoc />
        public virtual bool OnPostbuild(AddressablesDataBuilderInput input, AddressablesPlayerBuildResult result)
        {
            return true;
        }
    }

}
