using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Settings
{
    using Object = UnityEngine.Object;

    internal static class AddressableAssetUtility
    {
        internal static bool IsInResources(string path)
        {
            return path.Contains("/Resources/", StringComparison.Ordinal);
        }

        internal static bool TryGetPathAndGUIDFromTarget(Object target, out string path, out string guid)
        {
            if (target == null)
            {
                guid = string.Empty;
                path = string.Empty;
                return false;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out guid, out long _))
            {
                guid = string.Empty;
                path = string.Empty;
                return false;
            }

            path = AssetDatabase.GetAssetOrScenePath(target);
            return IsPathValidForEntry(path);
        }

        static readonly string[] _excludedExtensions = {".cs", ".js", ".boo", ".exe", ".dll", ".meta", ".preset", ".asmdef"};

        internal static bool IsPathValidForEntry(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            Assert.IsFalse(path.Contains('\\'), "Path contains '\\' - not a valid path for AddressableAssetEntry: " + path);

            if (!path.StartsWith("Assets", StringComparison.Ordinal) && !IsPathValidPackageAsset(path))
                return false;

            if (IsExcludedExtension(path))
                return false;

            // asset type
            if (path.Contains("/Editor/", StringComparison.Ordinal))
                return false;
            if (path.Contains("/Gizmos/", StringComparison.Ordinal))
                return false;

            // assets in config folder are not valid entries
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings == null || !path.StartsWith(settings.ConfigFolder, StringComparison.Ordinal);

            static bool IsExcludedExtension(string path)
            {
                foreach (var ext in _excludedExtensions)
                {
                    if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }

            static bool IsPathValidPackageAsset(string path)
            {
                return path.StartsWith("Packages/", StringComparison.Ordinal)
                       && path.EndsWith("package.json", StringComparison.Ordinal) is false;
            }
        }

        class TypeManager
        {
            public static List<Type> GetManagerTypes(Type rootType)
            {
                var types = new List<Type>();
                try
                {
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (a.IsDynamic)
                            continue;
                        foreach (var t in a.ExportedTypes)
                        {
                            if (t != rootType && rootType.IsAssignableFrom(t) && !t.IsAbstract)
                                types.Add(t);
                        }
                    }
                }
                catch (Exception)
                {
                    // ignored
                }

                return types;
            }
        }

        class TypeManager<T> : TypeManager
        {
            // ReSharper disable once StaticMemberInGenericType
            static List<Type> s_Types;

            public static List<Type> Types
            {
                get
                {
                    if (s_Types == null)
                        s_Types = GetManagerTypes(typeof(T));

                    return s_Types;
                }
            }
        }

        static Dictionary<Type, string> s_CachedDisplayNames = new();

        internal static string GetCachedTypeDisplayName(Type type)
        {
            string result = "<none>";
            if (type != null)
            {
                if (!s_CachedDisplayNames.TryGetValue(type, out result))
                {
                    var displayNameAtr = type.GetCustomAttribute<DisplayNameAttribute>();
                    if (displayNameAtr != null)
                    {
                        result = displayNameAtr.DisplayName;
                    }
                    else
                        result = type.Name;

                    s_CachedDisplayNames.Add(type, result);
                }
            }

            return result;
        }

        struct PackageData
        {
            public string version;
        }

        private static string m_Version = null;

        internal static string GetVersionFromPackageData()
        {
            if (string.IsNullOrEmpty(m_Version))
            {
                var jsonFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.unity.addressables/package.json");
                var packageData = JsonUtility.FromJson<PackageData>(jsonFile.text);
                var split = packageData.version.Split('.');
                if (split.Length < 2)
                    throw new Exception("Could not get correct version data for Addressables package");
                m_Version = $"{split[0]}.{split[1]}";
            }

            return m_Version;
        }

        internal class SortedDelegate<T1, T2, T3, T4>
        {
            public delegate void Delegate(T1 arg1, T2 arg2, T3 arg3, T4 arg4);

            private readonly SortedList<int, Delegate> m_SortedInvocationList = new SortedList<int, Delegate>();
            private readonly List<(int, Delegate)> m_RegisterQueue = new List<(int, Delegate)>();
            private bool m_IsInvoking;

            /// <summary>
            /// Removes a delegate from the invocation list.
            /// </summary>
            /// <param name="toUnregister">Delegate to remove</param>
            public void Unregister(Delegate toUnregister)
            {
                IList<int> keys = m_SortedInvocationList.Keys;
                for (int i = 0; i < keys.Count; ++i)
                {
                    m_SortedInvocationList[keys[i]] -= toUnregister;
                    if (m_SortedInvocationList[keys[i]] == null)
                    {
                        m_SortedInvocationList.Remove(keys[i]);
                        break;
                    }
                }

                if (m_IsInvoking && m_RegisterQueue.Count > 0)
                {
                    for (int i = m_RegisterQueue.Count - 1; i >= 0; --i)
                    {
                        if (m_RegisterQueue[i].Item2 == toUnregister)
                        {
                            m_RegisterQueue.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// Add a delegate to the invocation list
            /// </summary>
            /// <param name="toRegister">Delegate to add</param>
            /// <param name="order">Order to call the delegate in the invocation list</param>
            public void Register(Delegate toRegister, int order)
            {
                if (m_IsInvoking)
                {
                    m_RegisterQueue.Add((order, toRegister));
                    return;
                }

                FlushRegistrationQueue();
                RegisterToInvocationList(toRegister, order);
            }

            private void RegisterToInvocationList(Delegate toRegister, int order)
            {
                // unregister first, this will remove the delegate from another order if it is added
                Unregister(toRegister);
                if (m_SortedInvocationList.ContainsKey(order))
                    m_SortedInvocationList[order] += toRegister;
                else
                    m_SortedInvocationList.Add(order, toRegister);
            }

            /// <summary>
            /// Invoke all delegates in the invocation list for the given parameters
            /// </summary>
            /// <param name="arg1"></param>
            /// <param name="arg2"></param>
            /// <param name="arg3"></param>
            /// <param name="arg4"></param>
            public void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                if (m_IsInvoking)
                    return;

                FlushRegistrationQueue();
                Invoke_Internal(arg1, arg2, arg3, arg4);
            }

            private void Invoke_Internal(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
            {
                m_IsInvoking = true;
                foreach (var invocationList in m_SortedInvocationList)
                {
                    invocationList.Value?.Invoke(arg1, arg2, arg3, arg4);
                }

                m_IsInvoking = false;
            }

            private void FlushRegistrationQueue()
            {
                if (m_RegisterQueue.Count > 0)
                {
                    for (int i = m_RegisterQueue.Count - 1; i >= 0; --i)
                        RegisterToInvocationList(m_RegisterQueue[i].Item2, m_RegisterQueue[i].Item1);
                }
            }

            public static SortedDelegate<T1, T2, T3, T4> operator +(SortedDelegate<T1, T2, T3, T4> self, Delegate delegateToAdd)
            {
                int lastInOrder = self.m_SortedInvocationList.Keys[self.m_SortedInvocationList.Count - 1];
                self.Register(delegateToAdd, lastInOrder + 1);
                return self;
            }

            public static SortedDelegate<T1, T2, T3, T4> operator -(SortedDelegate<T1, T2, T3, T4> self, Delegate delegateToRemove)
            {
                self.Unregister(delegateToRemove);
                return self;
            }

            public static bool operator ==(SortedDelegate<T1, T2, T3, T4> obj1, SortedDelegate<T1, T2, T3, T4> obj2)
            {
                bool aNull = ReferenceEquals(obj1, null);
                bool bNull = ReferenceEquals(obj2, null);

                if (aNull && bNull)
                    return true;
                if (!aNull && bNull)
                    return obj1.m_SortedInvocationList.Count == 0;
                if (aNull && !bNull)
                    return obj2.m_SortedInvocationList.Count == 0;
                if (ReferenceEquals(obj1, obj2))
                    return true;
                return obj1.Equals(obj2);
            }

            public static bool operator !=(SortedDelegate<T1, T2, T3, T4> lhs, SortedDelegate<T1, T2, T3, T4> rhs)
            {
                return !(lhs == rhs);
            }

            protected bool Equals(SortedDelegate<T1, T2, T3, T4> other)
            {
                return Equals(m_SortedInvocationList, other.m_SortedInvocationList);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((SortedDelegate<T1, T2, T3, T4>) obj);
            }

            public override int GetHashCode()
            {
                return (m_SortedInvocationList != null ? m_SortedInvocationList.GetHashCode() : 0);
            }
        }

        internal static void MoveEntriesToGroup(AddressableAssetSettings settings, List<AddressableAssetEntry> entries, AddressableAssetGroup group)
        {
            foreach (var entry in entries)
            {
                if (entry.parentGroup != group)
                    settings.MoveEntry(entry, group);
            }
        }
    }
}