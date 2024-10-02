namespace TeaPieDraft.Exceptions
{
    internal class VariableNameViolationException : Exception
    {
        public VariableNameViolationException(string message)
            : base(message) { }
    }
}
