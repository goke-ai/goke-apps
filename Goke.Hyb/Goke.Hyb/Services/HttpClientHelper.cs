using System;
using System.Collections.Generic;
using System.Text;

namespace Goke.Hyb.Services;

/// <summary>
/// Helper class to manage HttpClient configuration and Url endpoint addresses.
/// </summary>
internal class HttpClientHelper
{
    public static HttpMessageHandler CreatePlatformMessageHandler()
    {
#if WINDOWS || MACCATALYST
        return new HttpClientHandler();
#else
        return new HttpsClientHandlerService().PlatformMessageHandler;
#endif
    }
}

internal class HttpsClientHandlerService
{
    public HttpMessageHandler PlatformMessageHandler
    {
        get
        {
#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (cert != null && cert.Issuer.Equals("CN=localhost"))
                    return true;
                return errors == System.Net.Security.SslPolicyErrors.None;
            };
            return handler;
#elif IOS
            var handler = new NSUrlSessionHandler
            {
                TrustOverrideForUrl = IsHttpsLocalhost
            };
            return handler;
#else
            throw new PlatformNotSupportedException("Only Android and iOS supported.");
#endif
        }
    }

#if IOS
    public bool IsHttpsLocalhost(NSUrlSessionHandler sender, string url, Security.SecTrust trust)
    {
        return url.StartsWith("https://localhost");
    }
#endif
}
