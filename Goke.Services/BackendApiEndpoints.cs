using Microsoft.Extensions.Options;

namespace Goke.Services
{
    public sealed class BackendApiEndpoints(IOptions<BackendApiOptions> options, IBackendApiBaseUrlResolver? baseUrlResolver = null)
    {
        public const string ClientName = "BackendApi";

        private readonly BackendApiOptions options = options.Value;
        private readonly IBackendApiBaseUrlResolver baseUrlResolver = baseUrlResolver ?? new DefaultBackendApiBaseUrlResolver();

        public Uri BaseUri => new(GetPlatformBaseUrl(), UriKind.Absolute);
        public Uri LoginUri => new(BaseUri, options.LoginPath);
        public Uri RegisterUri => new(BaseUri, options.RegisterPath);
        public Uri LogoutUri => new(BaseUri, options.LogoutPath);
        public Uri RefreshUri => new(BaseUri, options.RefreshPath);
        public Uri MeUri => new(BaseUri, options.MePath);
        public Uri WeatherUri => new(BaseUri, options.WeatherPath);

        private string GetPlatformBaseUrl()
        {
            return baseUrlResolver.Resolve(options.BaseUrl);
        }
    }

}
