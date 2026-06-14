namespace Goke.Web.Services;

public sealed class AdminStatusMessageStore
{
    private string? message;

    public string? Consume()
    {
        var current = message;
        message = null;
        return current;
    }

    public void Set(string value)
    {
        message = value;
    }
}
