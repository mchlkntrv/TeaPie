## General introduction
--------------------
In order to make this framework easy to use, there will be only one instance of public class `TeaPie`, which will hold all necessary functionality and information for user. `TeaPie` class will be designed as `Proxy` for end-user. Application will create instance in initialisation phase:
```csharp
TeaPie tp = new();
```
Then, end-user can approach all necessary attributes in following way:
```csharp
var body = tp.Request.Body;
```

## Testing API
--------------------
Tests within this framework can be divided into two categories:
- **Determined tests** - without possibility to input parameters
- **Theoretical tests** - these tests are represented by testing scenario(s) with custom parameters

### Determined tests
**API**
```csharp
public void Test(Action testFunction);
public void Test(string testName, Action testFunction);

public async Task TestAsync(Task<Action> testFunction);
public async Task TestAsync(string testName, Task<Action> testFunction);
```
**Examples of usage**
```csharp
tp.Test(() => {
    tp.Response.Body.Should().Not().Be().Empty();
});


tp.Test("Status Code Should Be 200.", () => {
    tp.Response.StatusCode.Should().Be().Equal(200);
});
```

### Theoretical tests
**API**
```csharp
public void AddTestTheory(Action<Theory> testFunction);
public void AddTestTheory(string testName, Action<Theory> testFunction);
```

**Examples of usage**
```csharp
tp.AddTestTheory(theory => {
    theory.Input<int>("m", 100); // Input parameter 'm' is set to 100
    theory.Input<int>("c", 5); // Input parameter 'c' is set to 5

    theory.Expected<int>("E", 2500); // Parameter 'E' is expected to be 2 500, after request execution
});


tp.AddTestTheory("Pythagorean theorem.", theory => {
    theory.Input<double>("a", 3);
    theory.Input<double>("b", 4);

    theory.Expected<double>("c", 5.0);execution
});
```

Method `tp.Test()` will be called in the **post-response** scripts in the most cases. Althought, there is no problem to use this method within **pre-request** script - e.g. when you want to check the state before request execution.

On the other hand method `tp.AddTestTheory()` must be located in **pre-request** script, other-wise it will be ignored.


## Variables API
--------------------
User will be able to work with multi-level variables. End-user will be able to access these variables either generally or on concrete level.
Levels:
1. `Global level`
2. `Environment level`
3. `Collection level` - this level exists only when running collection (not single test only)
4. `Scope level`
5. `Test level`

The order of the levels determines in which order will be variables collected. The priority of variables rise with the level. So, for example if variable `foo` is defined on `global level` with the value `moo`, end-user can override its value on all levels from `environment level` to `test level`.

Working with variables on specific level:
```csharp
// Approaching variables
var globalVar = pm.GlobalVariables.Get<int>("MyGlobalVariable");
var enviroVar = pm.EnvironmentVariables.Get<string>("MyEnvVariable");
var collVar = pm.CollectionVariables.Get<bool>("MyCollectionVariable");
var scopeVar = pm.ScopeVariables.Get<bool>("MyScopeVariable");
var testVar = pm.TestVariables.Get<DateTime>("MyTestVariable");

// Altering variables
pm.GlobalVariables.Set<int>("MyGlobalVariable", 123);
pm.EnvironmentVariables.Set<string>("MyEnvVariable", "Testlab");
pm.CollectionVariables.Set<bool>("MyCollectionVariable", true);
pm.TestVariables.Set<DateTime>("MyTestVariable", '2024-08-01');

// Deleting variables
pm.GlobalVariables.Delete("MyGlobalVariable");
pm.EnvironmentVariables.Delete("MyEnvVariable");
pm.CollectionVariables.Delete("MyCollectionVariable");
pm.TestVariables.Delete("MyTestVariable");
```

Approaching variables generally:
```csharp
pm.SetVariable<bool>("IsDevlabEnvironment", false);
var myVar = pm.GetVariable<bool>("IsDevlabEnvironment");
m.DeleteVariable("IsDevlabEnvironment");
pm.DeleteVariables("Temp.Collection.*"); // Deletes all variables with such a prefix
```

By approaching variables generally, method `GetVariable<T>()` will search for variable from highest level to lowest level (from `test level` to `global level`) and will return first found. Method `SetVariable<T>()` will automatically store the variable on `scope level`. Methods `DeleteVariable()` and `DeleteVariables()` delete variables on all levels on which occur.

## Test Runner API
--------------------
By default, runner takes all tests and run them in alphabetical order by their names. Althought, you can manipulate with the order even during run-time. For these purposes, you can use these methods:
```csharp
public void NextTest(string testName); // Specifies, which test should run next
public void NextFolder(string folderName); // Specifies, which folder should run next
public void SkipTest(string testName); // Specifies, which test should be ignored/skipped
public void SkipTests(params string[] testNames); // Specifies, which tests should be ignored/skipped
public void SkipFolder(string folderName); // Specifies, which folder should be skipped at all
```
These methods can be especially useful, when `Response` contains values that alter next behavior, or when tests failed and next request(s) don't make sense anymore.

**Be careful with these methods, since you can cause endless loop!**


## Execution Context API
--------------------
For end-user it is necessary to work with the attributes of current `Request` and its `Response`. Currently executed request and its response is stored in these attributes:
```csharp
var req = tp.Request;
req.Headers.Add("Application-Type", "25");

...

var res = tp.Response;
var body = res.Body.ToJson();
tp.SetVariable<int>("EntityId", body.Id);
```

Since, usually tests are run within collection, user should be able to access to older request/responses. This has one condition - the request/response has to be named in order to find it. Retrieval of these information wil be pretty straightforward:
```csharp
var oldResponse = tp.Responses["MyFirstRequest"];
var oldBody = oldResponse.Body.ToJson();
if (oldBody.TotalSum > 100){
    tp.Request.Body.ToJson().Add("DoNotRecalculate", "true");
}else{
    tp.Request.Body.ToJson().Add("DoNotRecalculate", "false");
}

var oldRequest = tp.Requests["MyFirstRequest"];
foreach(var header in oldRequest.Headers){
    tp.Request.Headers.Add(header.Key, header.Value);
}
```


## Logging API
--------------------