using System;

namespace UnityEngine
{
    /// <summary>
    /// Used to restrict an AssetReference field or property to only allow items wil specific labels.  This is only enforced through the UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class AssetReferenceUIRestriction : Attribute
    {
        /// <summary>
        /// Validates that the referenced asset allowable for this asset reference.
        /// </summary>
        /// <param name="obj">The Object to validate.</param>
        /// <returns>Whether the referenced asset is valid.</returns>
        public virtual bool ValidateAsset(Object obj)
        {
            return true;
        }

        /// <summary>
        /// Validates that the referenced asset allowable for this asset reference.
        /// </summary>
        /// <param name="path">The path to the asset in question.</param>
        /// <returns>Whether the referenced asset is valid.</returns>
        public virtual bool ValidateAsset(string path)
        {
            return true;
        }
    }
}
