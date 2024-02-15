using System;
using System.IO;
using System.Reflection;
using UnityEngine.Serialization;

namespace UnityEngine.AddressableAssets.Util
{
    /// <summary>
    /// Wrapper for serializing types for runtime.
    /// </summary>
    [Serializable]
    public struct SerializedType
    {
        [FormerlySerializedAs("m_assemblyName")]
        [SerializeField]
        string m_AssemblyName;

        /// <summary>
        /// The assembly name of the type.
        /// </summary>
        public string AssemblyName => m_AssemblyName;

        [FormerlySerializedAs("m_className")]
        [SerializeField]
        string m_ClassName;

        /// <summary>
        /// The name of the type.
        /// </summary>
        public string ClassName => m_ClassName;

        Type m_CachedType;

        /// <summary>
        /// Converts information about the serialized type to a formatted string.
        /// </summary>
        /// <returns>Returns information about the serialized type.</returns>
        public override string ToString()
        {
            return Value == null ? "<none>" : Value.Name;
        }

        /// <summary>
        /// Get and set the serialized type.
        /// </summary>
        public Type Value
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(m_AssemblyName) || string.IsNullOrEmpty(m_ClassName))
                        return null;

                    if (m_CachedType == null)
                    {
                        var assembly = Assembly.Load(m_AssemblyName);
                        if (assembly != null)
                            m_CachedType = assembly.GetType(m_ClassName);
                    }

                    return m_CachedType;
                }
                catch (Exception ex)
                {
                    //file not found is most likely an editor only type, we can ignore error.
                    if (ex.GetType() != typeof(FileNotFoundException))
                        Debug.LogException(ex);
                    return null;
                }
            }
            set
            {
                if (value != null)
                {
                    m_AssemblyName = value.Assembly.FullName;
                    m_ClassName = value.FullName;
                }
                else
                {
                    m_AssemblyName = m_ClassName = null;
                }
            }
        }

        /// <summary>
        /// Used for multi-object editing. Indicates whether or not property value was changed.
        /// </summary>
        public bool ValueChanged { get; set; }
    }
}
