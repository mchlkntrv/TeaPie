namespace TeaPie.Templating;

internal static class TemplatingErrors
{
    public const string Prefix = "Templating error";

    public static string StripPrefix(string message)
    {
        const string prefix = Prefix + ": ";
        return message.StartsWith(prefix, StringComparison.Ordinal) ? message[prefix.Length..] : message;
    }

    public static InvalidOperationException InFile(string filePath, string message, Exception? innerException = null)
        => new($"{Prefix} in '{filePath}': {StripPrefix(message)}", innerException);

    public static T InFile<T>(string filePath, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (InvalidOperationException ex)
        {
            throw InFile(filePath, ex.Message, ex);
        }
    }
}
