namespace Goke.Services;

public interface IBackendApiBaseUrlResolver
{
    string Resolve(string baseUrl);
}
