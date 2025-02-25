using System.Net.Http.Headers;
using TeaPie.Http.Parsing;
using static Xunit.Assert;

namespace TeaPie.Tests.Http.Parsing;

public class RetryStrategyLineParserShould
{
    private readonly HttpRequestHeaders _headers = new HttpClient().DefaultRequestHeaders;
    private readonly RetryStrategyDirectiveLineParser _parser = new();

    [Fact]
    public void ParseRetryStrategyDirective()
    {
        const string line = "## RETRY-STRATEGY: FastRetry";
        HttpParsingContext context = new(_headers);

        True(_parser.CanParse(line, context));
        _parser.Parse(line, context);

        Equal("FastRetry", context.RetryStrategyName);
    }

    [Fact]
    public void ThrowExceptionOnInvalidRetryStrategyDirective()
    {
        const string line = "## INVALID-STRATEGY: SomethingElse";
        HttpParsingContext context = new(_headers);

        False(_parser.CanParse(line, context));
    }

    [Fact]
    public void NotParseOtherDirective()
    {
        const string line = "## ANOTHER-DIRECTIVE: OAuth2";
        var context = new HttpParsingContext(_headers);

        var canParse = _parser.CanParse(line, context);

        False(canParse);
        Throws<InvalidOperationException>(() => _parser.Parse(line, context));
    }
}
