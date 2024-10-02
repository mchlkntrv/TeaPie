namespace TeaPieDraft.ScriptHandling;
internal class FileReader
{
    internal static async Task<string> GetFileContentAsync(string path)
        => await File.ReadAllTextAsync(path);
}
