namespace TeaPieDraft.Variables
{
    internal class Variable(string name, object? value)
    {
        public string Name { get; set; } = name;
        public object? Value { get; set; } = value;
        public T? GetValue<T>() => (T?)Value;
    }
}
