using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Build.Layout
{
    /// <summary>
    /// Helper methods for gathering data about a build layout.
    /// </summary>
    internal class BuildLayoutHelpers
    {
        /// <summary>
        /// Gather a list of Explicit Assets defined in a BuildLayout
        /// </summary>
        /// <param name="layout">The BuildLayout generated during a build</param>
        /// <returns>A list of ExplicitAsset data.</returns>
        public static IEnumerable<BuildLayout.ExplicitAsset> EnumerateAssets(BuildLayout layout)
        {
            return EnumerateBundles(layout).SelectMany(b => b.Files).SelectMany(f => f.Assets);
        }

        internal static IEnumerable<BuildLayout.DataFromOtherAsset> EnumerateImplicitAssets(BuildLayout layout)
        {
            return EnumerateBundles(layout).SelectMany(b => b.Files).SelectMany(f => f.Assets).SelectMany(a => a.InternalReferencedOtherAssets);
        }

        internal static IEnumerable<BuildLayout.DataFromOtherAsset> EnumerateImplicitAssets(BuildLayout.Bundle bundle)
        {
            return bundle.Files.SelectMany(f => f.OtherAssets);
        }

        /// <summary>
        /// Gather a list of Explicit Assets defined in a Bundle
        /// </summary>
        /// <param name="bundle">The Bundle data generated during a build</param>
        /// <returns>A list of ExplicitAssets defined in the Bundle</returns>
        public static IEnumerable<BuildLayout.ExplicitAsset> EnumerateAssets(BuildLayout.Bundle bundle)
        {
            return bundle.Files.SelectMany(f => f.Assets);
        }

        /// <summary>
        /// Gather a list of Bundle data defined in a BuildLayout
        /// </summary>
        /// <param name="layout">The BuildLayout generated during a build</param>
        /// <returns>A list of the Bundle data defined in a BuildLayout</returns>
        public static IEnumerable<BuildLayout.Bundle> EnumerateBundles(BuildLayout layout)
        {
            foreach (BuildLayout.Bundle b in layout.BuiltInBundles)
                yield return b;

            foreach (BuildLayout.Bundle b in layout.Groups.Select(g => g.Bundle))
                yield return b;
        }

        /// <summary>
        /// Gather a list of File data defined in a BuildLayout
        /// </summary>
        /// <param name="layout">The BuildLayout generated during a build</param>
        /// <returns>A list of File data</returns>
        public static IEnumerable<BuildLayout.File> EnumerateFiles(BuildLayout layout)
        {
            return EnumerateBundles(layout).SelectMany(b => b.Files);
        }

        /// <summary>
        /// Gets the enum AssetType associated with the param systemType ofType
        /// </summary>
        /// <param name="ofType">The Type of the asset</param>
        /// <returns>An AssetType or <see cref="AssetType.Other" /> if null or unknown.</returns>
        public static AssetType GetAssetType(Type ofType)
        {
            // For Resources/unity_builtin_extra, we don't have a type, so we return Other.
            if (ofType is null)
                return AssetType.Other;

            if (ofType == typeof(SceneAsset))
                return AssetType.Scene;
            if (ofType == typeof(Animations.AnimatorController))
                return AssetType.AnimationController;

            if (typeof(ScriptableObject).IsAssignableFrom(ofType))
                return AssetType.ScriptableObject;
            if (typeof(MonoBehaviour).IsAssignableFrom(ofType))
                return AssetType.MonoBehaviour;
            if (typeof(Component).IsAssignableFrom(ofType))
                return AssetType.Component;

            if (ofType.FullName == "UnityEditor.Audio.AudioMixerController")
                return AssetType.AudioMixer;
            if (ofType.FullName == "UnityEditor.Audio.AudioMixerGroupController")
                return AssetType.AudioMixerGroup;

            return Enum.TryParse(ofType.Name, out AssetType assetType)
                ? assetType : AssetType.Other;
        }
    }
}