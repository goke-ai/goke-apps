
using Goke.Services;

namespace Goke.Hyb.Services;

internal sealed class BackendApiBaseUrlResolver : IBackendApiBaseUrlResolver
{
    public string Resolve(string baseUrl)
    {
#if DEBUG
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return baseUrl.Replace("localhost", "10.0.2.2", StringComparison.OrdinalIgnoreCase);
        }
#endif

        return baseUrl;
    }
}
