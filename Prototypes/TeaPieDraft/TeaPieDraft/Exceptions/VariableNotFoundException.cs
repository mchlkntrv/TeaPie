namespace TeaPieDraft.Exceptions
{
    internal class VariableNotFoundException : Exception
    {
        public VariableNotFoundException(string key, string type)
            : base($"Variable with name '{key}' of type '{type}' was not found.") { }
    }
}
