using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using TeaPieDraft.Attributes;
using TeaPieDraft.Exceptions;
using TeaPieDraft.HttpClient;
using TeaPieDraft.Pipelines.Application;
using TeaPieDraft.ScriptHandling;
using TeaPieDraft.Variables;

namespace TeaPieDraft
{
    internal class Application : ITeaPie
    {
        internal static TeaPie? UserContext { get; private set; }

        private readonly ILogger _logger;
        private readonly StructureExplorer.StructureExplorer _structureExplorer;
        private readonly ScriptCompiler _scriptCompiler;
        private readonly ScriptRunner _scriptRunner;
        private readonly ScriptPreProcessor _scriptPreProcessor;

        internal Application(ILogger logger)
        {
            _logger = logger;
            _scriptCompiler = new ScriptCompiler();
            _structureExplorer = new StructureExplorer.StructureExplorer();
            _scriptRunner = new ScriptRunner();
            _scriptPreProcessor = new ScriptPreProcessor();
        }

        // Variables
        private readonly VariablesCollection _globalVariables = new();
        private readonly VariablesCollection _environmentVariables = new();
        private readonly VariablesCollection _collectionVariables = new();
        private readonly VariablesCollection _scopeVariables = new();
        private readonly VariablesCollection _testCaseVariables = new();

        public VariablesCollection GlobalVariables => _globalVariables;
        public VariablesCollection EnvironmentVariables => _environmentVariables;
        public VariablesCollection CollectionVariables => _collectionVariables;
        public VariablesCollection ScopeVariables => _scopeVariables;
        public VariablesCollection TestCaseVariables => _testCaseVariables;

        public T? GetVariable<T>(string name, T? defaultValue = default)
            => _testCaseVariables.Contains(name)
                ? _testCaseVariables.Get(name, defaultValue)
                : _scopeVariables.Contains(name)
                    ? _scopeVariables.Get(name, defaultValue)
                    : _collectionVariables.Contains(name)
                        ? _collectionVariables.Get(name, defaultValue)
                        : _environmentVariables.Contains(name)
                            ? _environmentVariables.Get(name, defaultValue)
                            : _globalVariables.Contains(name)
                                ? _globalVariables.Get(name, defaultValue)
                                : throw new VariableNotFoundException(name, typeof(T).Name);

        public void SetVariable<T>(string name, T value)
        {
            VariableNameValidator.Resolve(name);
            _scopeVariables.Set(name, value);
        }

        public bool RemoveVariable(string name)
            => _testCaseVariables.Contains(name)
                ? _testCaseVariables.Remove(name)
                : _scopeVariables.Contains(name)
                    ? _scopeVariables.Remove(name)
                    : _collectionVariables.Contains(name)
                        ? _collectionVariables.Remove(name)
                        : _environmentVariables.Contains(name)
                            ? _environmentVariables.Remove(name)
                            : _globalVariables.Contains(name) && _globalVariables.Remove(name);

        public bool RemoveVariables(string prefix)
            => _testCaseVariables.RemoveVariables(prefix) &&
            _scopeVariables.RemoveVariables(prefix) &&
            _collectionVariables.RemoveVariables(prefix) &&
            _environmentVariables.RemoveVariables(prefix) &&
            _globalVariables.RemoveVariables(prefix);


        // Run
        [MemberNotNull(nameof(UserContext))]
        internal async Task RunAsync(string path)
        {
            var userContext = new TeaPie(this);
            ScriptCompiler.UserContext = userContext;
            UserContext = userContext;

            var appContext = new ApplicationContext(path);
            var pipeline = ApplicationPipeline.CreateDefault();
            await pipeline.RunAsync(appContext);
        }

        // Execution context
        public Response Response => throw new NotImplementedException();

        public ILogger Logger => _logger;

        public ReportingService ReportingService => throw new NotImplementedException();

        Request ITeaPie.Request => throw new NotImplementedException();

        public void AddTestTheory(string theoryName, Action<Theory> theory)
        {
            throw new NotImplementedException();
        }

        public void DelayRequest(int milliseconds)
        {
            throw new NotImplementedException();
        }

        public void DisableTimeout()
        {
            throw new NotImplementedException();
        }

        public void EnableTimeout()
        {
            throw new NotImplementedException();
        }

        public Response GetResponse(string name)
        {
            throw new NotImplementedException();
        }

        public void NextFolder(string folderName)
        {
            throw new NotImplementedException();
        }

        public void NextTestCase(string testCaseName)
        {
            throw new NotImplementedException();
        }

        public void Retry()
        {
            throw new NotImplementedException();
        }

        public void RetryUntilStatusCode(params int[] statusCodes)
        {
            throw new NotImplementedException();
        }

        public void RetryUntilTestsPass(params int[] statusCodes)
        {
            throw new NotImplementedException();
        }

        public void SetLogger(ILogger logger)
        {
            throw new NotImplementedException();
        }

        public void SetTimeout(int milliseconds)
        {
            throw new NotImplementedException();
        }

        public void SkipFolder(string folderName)
        {
            throw new NotImplementedException();
        }

        public void SkipTestCase(string testCaseName)
        {
            throw new NotImplementedException();
        }

        public void SkipTestCases(params string[] testCaseNames)
        {
            throw new NotImplementedException();
        }

        public void Test(string testName, Action test)
        {
            throw new NotImplementedException();
        }

        public void Test(string testName, Func<Task> asyncTest)
        {
            throw new NotImplementedException();
        }

        Request ITeaPie.GetRequest(string name)
        {
            throw new NotImplementedException();
        }
    }
}
