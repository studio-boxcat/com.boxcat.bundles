using System;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.AddressableAssets.ResourceLocators
{
    /// <summary>
    /// Interface used by the Addressables system to find the locations of a given key.
    /// </summary>
    public interface IResourceLocator
    {
        /// <summary>
        /// Retrieve the locations from a specified key.
        /// </summary>
        /// <param name="key">The key to use.</param>
        /// <param name="type">The resource type.</param>
        /// <returns>True if any locations were found with the specified key.</returns>
        IResourceLocation Locate(string key, Type type);
    }
}