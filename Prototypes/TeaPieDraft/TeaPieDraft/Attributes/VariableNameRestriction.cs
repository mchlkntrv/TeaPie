using System.Text.RegularExpressions;
using TeaPieDraft.Exceptions;

namespace TeaPieDraft.Attributes
{
    internal class VariableNameValidator
    {
        private const string pattern = @"^[a-zA-Z0-9-]+$";

        internal VariableNameValidator()
        {
        }

        internal static void Resolve(object? name)
        {
            if (!IsValid(name, out var errors))
            {
                throw new VariableNameViolationException(string.Join(Environment.NewLine, errors));
            }
        }

        internal static bool IsValid(object? value)
            => IsValid(value, out var _);

        private static bool IsValid(object? value, out List<string> validationErrors)
        {
            validationErrors = [];
            if (value == null)
            {
                validationErrors.Add("The name has to be in string format.");
                return true;
            }

            if (value is not string name)
            {
                validationErrors.Add("The name has to be in string format.");
                return false;
            }

            if (Regex.IsMatch(name, pattern))
            {
                return true;
            }
            else
            {
                validationErrors.Add($"Name '{name}' contains invalid characters (only a-z, A-Z, 0-9 and '-' is allowed).");
                return false;
            }
        }
    }
}
