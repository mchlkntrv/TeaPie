using TeaPieDraft.Attributes;

namespace TeaPieDraft.Variables
{
    public class VariablesCollection
    {
        private readonly Dictionary<string, Variable> _variables = [];

        // Constructors
        internal VariablesCollection() { }

        internal VariablesCollection(Dictionary<string, Variable> collection)
        {
            foreach (var item in collection)
            {
                _variables.Add(item.Key, item.Value);
            };
        }

        internal VariablesCollection(VariablesCollection collection)
        {
            _variables = collection.KeyValuePairs;
        }

        // Internal methods
        private Dictionary<string, Variable> KeyValuePairs => _variables;

        internal object? this[string key] => _variables[key].Value;

        internal ICollection<string> Keys => _variables.Keys;

        internal ICollection<Variable> Values => _variables.Values;

        internal int Count => _variables.Count;

        internal Variable Get(string key) => _variables[key];

        internal void Set(string key, Variable value)
        {
            VariableNameValidator.Resolve(key);
            _variables[key] = value;
        }
        internal void Clear() => _variables.Clear();

        internal bool RemoveVariables(string prefix)
        {
            var variablesToDelete = _variables.Where(x => x.Key.StartsWith(prefix));
            var deleted = variablesToDelete.Any();
            foreach (var item in variablesToDelete)
            {
                deleted = deleted && _variables.Remove(item.Key);
            }
            return deleted;
        }

        // Public interface
        public bool Contains(string key) => _variables.ContainsKey(key);

        public T? Get<T>(string key, T? defaultValue = default)
        {
            try
            {
                var variable = Get(key);
                return variable.GetValue<T>();
            }
            catch (Exception)
            {
                // throw new VariableNotFoundException(key, typeof(T).Name);
                return defaultValue;
            }
        }

        public void Set<T>(string key, T? value)
        {
            VariableNameValidator.Resolve(key);
            Set(key, new Variable(key, value));
        }

        public bool Remove(string key) => _variables.Remove(key);
    }
}
