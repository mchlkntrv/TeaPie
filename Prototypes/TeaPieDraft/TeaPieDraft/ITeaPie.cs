using Microsoft.Extensions.Logging;
using TeaPieDraft.HttpClient;
using TeaPieDraft.Variables;

namespace TeaPieDraft
{
    internal interface ITeaPie
    {
        public void NextTestCase(string testCaseName); // Test case that will run next
        public void NextFolder(string folderName); // Folder that will run next
        public void SkipTestCase(string testCaseName); // Test case to be ignored/skipped
        public void SkipTestCases(params string[] testCaseNames); // Test cases to be ignored/skipped
        public void SkipFolder(string folderName); // Folder to be skipped at all

        // Testing methods
        public void Test(string testName, Action test); // Determined test method
        public void Test(string testName, Func<Task> asyncTest); // Async test method
        public void AddTestTheory(string theoryName, Action<Theory> theory); // Adds a test theory

        // Delay/Timeout
        public void DelayRequest(int milliseconds); // Delays the request
        public void SetTimeout(int milliseconds); // Sets the timeout
        public void DisableTimeout(); // Disables the timeout
        public void EnableTimeout(); // Enables the timeout

        // Retrying
        public void RetryUntilStatusCode(params int[] statusCodes); // Will retry request until one of the 'statusCodes' is returned.
        public void RetryUntilTestsPass(params int[] statusCodes); // Will retry request until all of the tests are successfull
        public void Retry(); // This function will retry current request(s)

        // Logger methods
        public void SetLogger(ILogger logger); // Sets the logger

        // Variables
        public VariablesCollection GlobalVariables { get; }
        public VariablesCollection EnvironmentVariables { get; }
        public VariablesCollection CollectionVariables { get; }
        public VariablesCollection ScopeVariables { get; }
        public VariablesCollection TestCaseVariables { get; }

        // Variables methods
        public void SetVariable<T>(string name, T value); // Sets a variable
        public T? GetVariable<T>(string name, T? defaultValue = default); // Gets a variable
        public bool RemoveVariable(string name); // Removes all variables by their prefix
        public bool RemoveVariables(string prefix); // Removes all variables by their prefix

        // Properties/Fields
        public Request Request { get; } // Gets the current request
        public Response Response { get; } // Gets the current response
        public ILogger Logger { get; } // Gets the current response
        public ReportingService ReportingService { get; }

        // Retrieval methods
        public Response GetResponse(string name); // Retrieves a named response
        public Request GetRequest(string name); // Retrieves a named request
    }
}
