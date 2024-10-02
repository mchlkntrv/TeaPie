using Microsoft.Extensions.Logging;
using TeaPieDraft;
using TeaPieDraft.HttpClient;
using TeaPieDraft.Variables;

public class TeaPie : ITeaPie
{
    private readonly Application _application;

    internal TeaPie(Application application)
    {
        _application = application;
    }

    // Variables
    public VariablesCollection GlobalVariables => _application.GlobalVariables;
    public VariablesCollection EnvironmentVariables => _application.EnvironmentVariables;
    public VariablesCollection CollectionVariables => _application.CollectionVariables;
    public VariablesCollection ScopeVariables => _application.ScopeVariables;
    public VariablesCollection TestCaseVariables => _application.TestCaseVariables;

    public T? GetVariable<T>(string name, T? defaultValue = default) => _application.GetVariable(name, defaultValue);

    public void SetVariable<T>(string name, T value)
    {
        _application.SetVariable<T>(name, value);
    }

    public bool RemoveVariable(string name) => _application.RemoveVariable(name);

    public bool RemoveVariables(string prefix) => _application.RemoveVariables(prefix);


    // Execution context
    public Response Response => throw new NotImplementedException();

    public ILogger Logger => _application.Logger;

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

public abstract class ReportingService
{
    public abstract void SaveOutputTo(string filename);
}


public abstract class Runner
{
    public abstract void SetOrder(params string[] order); // Sets the order of tests
    public abstract void Skip(params string[] tests); // Skips specified tests
}

public abstract class Theory
{
    public abstract void Input<T>(string name, T value);
    public abstract void Expected<T>(string name, T value);
}
