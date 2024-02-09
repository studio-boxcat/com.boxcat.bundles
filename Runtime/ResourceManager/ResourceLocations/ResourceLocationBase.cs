using System;
using System.Collections.Generic;

namespace UnityEngine.ResourceManagement.ResourceLocations
{
    /// <summary>
    /// Basic implementation of IResourceLocation
    /// </summary>
    public class ResourceLocationBase : IResourceLocation
    {
        string m_Id;
        ResourceProviderType m_ProviderId;
        int m_DependencyHashCode;
        int m_HashCode;
        Type m_Type;
        List<IResourceLocation> m_Dependencies;
        string m_PrimaryKey;

        /// <summary>
        /// Internal id.
        /// </summary>
        public string InternalId => m_Id;

        /// <summary>
        /// Provider Id.  This is usually set to the FullName property of the type of the provider class.
        /// </summary>
        public ResourceProviderType ProviderId => m_ProviderId;

        /// <summary>
        /// List of dependencies that must be loaded before this location.  This value may be null.
        /// </summary>
        public IList<IResourceLocation> Dependencies => m_Dependencies;

        /// <summary>
        /// Convenience method to see if there are any dependencies.
        /// </summary>
        public bool HasDependencies => m_Dependencies != null && m_Dependencies.Count > 0;

        /// <inheritdoc/>
        public string PrimaryKey
        {
            get { return m_PrimaryKey; }
            set { m_PrimaryKey = value; }
        }

        /// <summary>
        /// Precomputed hash code of dependencies.
        /// </summary>
        public int DependencyHashCode
        {
            get { return m_DependencyHashCode; }
        }

        /// <summary>
        /// The type of the resource for th location.
        /// </summary>
        public Type ResourceType
        {
            get { return m_Type; }
        }

        /// <summary>
        /// Compute the hash of this location for the specified type.
        /// </summary>
        /// <param name="t">The type to hash with.</param>
        /// <returns>The combined hash code of the location and type.</returns>
        public int Hash(Type t)
        {
            return (m_HashCode * 31 + t.GetHashCode()) * 31 + DependencyHashCode;
        }

        /// <summary>
        /// Returns the Internal name used by the provider to load this location
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return m_Id;
        }

        /// <summary>
        /// Construct a new ResourceLocationBase.
        /// </summary>
        /// <param name="name">The name of the location.  This is usually set to the primary key, or "address" of the location.</param>
        /// <param name="id">The internal id of the location.  This is used by the IResourceProvider to identify the object to provide.  For example this may contain the file path or url of an asset.</param>
        /// <param name="providerId">The provider id.  This is set to the FullName of the type of the provder class.</param>
        /// <param name="t">The type of the object to provide.</param>
        /// <param name="dependencies">Locations for the dependencies of this location.</param>
        public ResourceLocationBase(string name, string id, ResourceProviderType providerId, Type t, params IResourceLocation[] dependencies)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));
            m_PrimaryKey = name;
            m_HashCode = (name.GetHashCode() * 31 + id.GetHashCode()) * 31 + providerId.GetHashCode();
            m_Id = id;
            m_ProviderId = providerId;
            m_Dependencies = new List<IResourceLocation>(dependencies);
            m_Type = t == null ? typeof(object) : t;
            ComputeDependencyHash();
        }

        /// <summary>
        /// Compute the dependency hash for this location
        /// </summary>
        public void ComputeDependencyHash() // TODO: dependency hash is no longer just objects
        {
            m_DependencyHashCode = m_Dependencies.Count > 0 ? 17 : 0;
            foreach (var d in m_Dependencies)
                m_DependencyHashCode = m_DependencyHashCode * 31 + d.Hash(typeof(object));
        }
    }
}
