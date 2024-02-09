using System;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Base class for IResourceProvider.
    /// </summary>
    public abstract class ResourceProviderBase : IResourceProvider
    {
        /// <summary>
        /// The extra behavior of the provider.
        /// </summary>
        protected ProviderBehaviourFlags m_BehaviourFlags = ProviderBehaviourFlags.None;

        /// <inheritdoc/>
        public virtual bool CanProvide(Type t, IResourceLocation location)
        {
            return GetDefaultType(location).IsAssignableFrom(t);
        }

        /// <summary>
        /// Converts information about the resource provider to a formatted string.
        /// </summary>
        /// <returns>Returns information about the resource provider.</returns>
        public override string ToString() => GetType().FullName;

        /// <summary>
        /// Release the specified object that was created from the specified location.
        /// </summary>
        /// <param name="location">The location of the object</param>
        /// <param name="obj">The object to release.</param>
        public virtual void Release(IResourceLocation location, object obj)
        {
        }

        /// <summary>
        /// Get the default type of object that this provider can provide.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public virtual Type GetDefaultType(IResourceLocation location)
        {
            return typeof(object);
        }

        /// <summary>
        /// Provide the object specified in the provideHandle.
        /// </summary>
        /// <param name="provideHandle">Contains all data needed to provide the requested object.</param>
        public abstract void Provide(ProvideHandle provideHandle);

        ProviderBehaviourFlags IResourceProvider.BehaviourFlags
        {
            get { return m_BehaviourFlags; }
        }
    }

    /// <summary>
    /// Contains options used in Resource Provider load requests.  ProviderLoadRequestOptions are used to specify
    /// parameters such as whether or not to ignore load failures and UnityWebRequest timeouts.
    /// </summary>
    [Serializable]
    public class ProviderLoadRequestOptions
    {
        [SerializeField]
        private bool m_IgnoreFailures = false;

        /// <summary>
        /// Creates a memberwise clone of a given ProviderLoadRequestOption.
        /// </summary>
        /// <returns>The newly created ProviderLoadRequestOption object</returns>
        public ProviderLoadRequestOptions Copy()
        {
            return (ProviderLoadRequestOptions)this.MemberwiseClone();
        }

        /// <summary>
        /// IgnoreFailures for provider load requests
        /// </summary>
        public bool IgnoreFailures
        {
            get { return m_IgnoreFailures; }
            set { m_IgnoreFailures = value; }
        }
    }
}
