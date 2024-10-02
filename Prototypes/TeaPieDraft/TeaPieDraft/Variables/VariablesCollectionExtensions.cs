namespace TeaPieDraft.Variables
{
    public static class VariablesCollectionExtensions
    {
        public static bool GetBool(this VariablesCollection collection, string key, bool defaultValue = default)
            => collection.Get(key, defaultValue);

        public static int GetInt(this VariablesCollection collection, string key, int defaultValue = default)
            => collection.Get(key, defaultValue);

        public static long GetLong(this VariablesCollection collection, string key, long defaultValue = default)
            => collection.Get(key, defaultValue);

        public static double GetDouble(this VariablesCollection collection, string key, double defaultValue = default)
            => collection.Get(key, defaultValue);

        public static char GetString(this VariablesCollection collection, string key, char defaultValue = default)
            => collection.Get(key, defaultValue);

        public static string? GetString(this VariablesCollection collection, string key, string? defaultValue = default)
            => collection.Get(key, defaultValue);

        public static DateTime? GetDateTime(this VariablesCollection collection, string key, DateTime? defaultValue = default)
            => collection.Get(key, defaultValue);
    }
}
