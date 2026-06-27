namespace Goke.Services;

public sealed class BackendApiOptions
{
    public const string SectionName = "BackendApi";

    public string BaseUrl { get; set; } = "https://localhost:7187/";
    public string LoginPath { get; set; } = "identity/login";
    public string RegisterPath { get; set; } = "identity/register";
    public string LogoutPath { get; set; } = "identity/logout";
    public string RefreshPath { get; set; } = "identity/refresh";
    public string MePath { get; set; } = "identity/me";
    public string WeatherPath { get; set; } = "api/weather";
}
