using System;
using System.ComponentModel;
using System.IO;
using UnityEngine.ResourceManagement.Util;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides raw text from a local or remote URL.
    /// </summary>
    [DisplayName("Text Data Provider")]
    public class TextDataProvider : ResourceProviderBase
    {
        /// <summary>
        /// Controls whether errors are logged - this is disabled when trying to load from the local cache since failures are expected
        /// </summary>
        public bool IgnoreFailures { get; set; }

        internal class InternalOp
        {
            TextDataProvider m_Provider;
            ProvideHandle m_PI;
            bool m_IgnoreFailures;
            private bool m_Complete = false;

            public void Start(ProvideHandle provideHandle, TextDataProvider rawProvider)
            {
                m_PI = provideHandle;
                m_PI.SetWaitForCompletionCallback(WaitForCompletionHandler);
                m_Provider = rawProvider;

                // override input options with options from Location if included
                if (m_PI.Location.Data is ProviderLoadRequestOptions providerData)
                {
                    m_IgnoreFailures = providerData.IgnoreFailures;
                }
                else
                {
                    m_IgnoreFailures = rawProvider.IgnoreFailures;
                }

                var path = m_PI.Location.InternalId;
                if (ResourceManagerConfig.ShouldPathUseWebRequest(path))
                {
                    throw new NotSupportedException(path);
                }
                else if (File.Exists(path))
                {
#if NET_4_6
                    if (path.Length >= 260)
                        path = @"\\?\" + path;
#endif
                    var text = File.ReadAllText(path);
                    object result = ConvertText(text);
                    m_PI.Complete(result, result != null, result == null ? new Exception($"Unable to load asset of type {m_PI.Type} from location {m_PI.Location}.") : null);
                    m_Complete = true;
                }
                else
                {
                    Exception exception = null;
                    //Don't log errors when loading from the persistentDataPath since these files are expected to not exist until created
                    if (m_IgnoreFailures)
                    {
                        m_PI.Complete<object>(null, true, exception);
                        m_Complete = true;
                    }
                    else
                    {
                        exception = new Exception(string.Format("Invalid path in " + nameof(TextDataProvider) + " : '{0}'.", path));
                        m_PI.Complete<object>(null, false, exception);
                        m_Complete = true;
                    }
                }
            }

            bool WaitForCompletionHandler()
            {
                return m_Complete;
            }

            private object ConvertText(string text)
            {
                try
                {
                    return m_Provider.Convert(m_PI.Type, text);
                }
                catch (Exception e)
                {
                    if (!m_IgnoreFailures)
                        Debug.LogException(e);
                    return null;
                }
            }
        }

        /// <summary>
        /// Method to convert the text into the object type requested.  Usually the text contains a JSON formatted serialized object.
        /// </summary>
        /// <param name="type">The object type the text is converted to.</param>
        /// <param name="text">The text to be converted.</param>
        /// <returns>The converted object.</returns>
        public virtual object Convert(Type type, string text)
        {
            return text;
        }

        /// <summary>
        /// Provides raw text data from the location.
        /// </summary>
        /// <param name="provideHandle">The data needed by the provider to perform the load.</param>
        public override void Provide(ProvideHandle provideHandle)
        {
            new InternalOp().Start(provideHandle, this);
        }
    }
}
