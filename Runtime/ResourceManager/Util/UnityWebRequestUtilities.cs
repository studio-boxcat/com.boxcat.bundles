using System;
using UnityEngine.Networking;

namespace UnityEngine.ResourceManagement.Util
{
    /// <summary>
    /// Utility class for extracting information from UnityWebRequests.
    /// </summary>
    public class UnityWebRequestUtilities
    {
        /// <summary>
        /// Determines if a web request resulted in an error.
        /// </summary>
        /// <param name="webReq">The web request.</param>
        /// <param name="result"></param>
        /// <returns>True if a web request resulted in an error.</returns>
        public static bool RequestHasErrors(UnityWebRequest webReq)
        {
            if (webReq == null || !webReq.isDone)
                return false;

#if UNITY_2020_1_OR_NEWER
            switch (webReq.result)
            {
                case UnityWebRequest.Result.InProgress:
                case UnityWebRequest.Result.Success:
                    return false;
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.DataProcessingError:
                    return true;
                default:
                    throw new NotImplementedException($"Cannot determine whether UnityWebRequest succeeded or not from result : {webReq.result}");
            }
#else
            var isError = webReq.isHttpError || webReq.isNetworkError;
            return isError;
#endif
        }
    }

}
