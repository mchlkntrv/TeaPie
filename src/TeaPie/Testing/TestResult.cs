﻿using Dunet;

namespace TeaPie.Testing;

[Union]
public partial record TestResult
{
    public partial record NotRun;
    public partial record Passed(long Duration);
    public partial record Failed(long Duration, string ErrorMessage, Exception? Exception);

    public required string TestName { get; init; }
    public string TestCasePath { get; init; }
    public required string SourceType { get; init; }
    public string? RequestName { get; init; }
}
