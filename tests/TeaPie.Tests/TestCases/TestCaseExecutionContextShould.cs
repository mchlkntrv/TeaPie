using FluentAssertions;
using TeaPie.Templating;
using TeaPie.TestCases;

namespace TeaPie.Tests.TestCases;

public class TestCaseExecutionContextShould
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OverwriteARequestAndResponseOnlyWhenLoopsRepeatTheirName(bool repeatedByLoops)
    {
        var repeatable = new HashSet<string>(StringComparer.Ordinal) { "CreatePartner" };
        var context = new TestCaseExecutionContext(null!);
        if (repeatedByLoops)
        {
            context.TemplatedRequestNames = new TemplatedRequestNames(repeatable, repeatable);
        }

        var secondRequest = new HttpRequestMessage();
        var secondResponse = new HttpResponseMessage();
        context.RegisterRequest(new HttpRequestMessage(), "CreatePartner");
        context.RegisterResponse(new HttpResponseMessage(), "CreatePartner");

        var registerRequest = () => context.RegisterRequest(secondRequest, "CreatePartner");
        var registerResponse = () => context.RegisterResponse(secondResponse, "CreatePartner");

        if (repeatedByLoops)
        {
            registerRequest.Should().NotThrow();
            registerResponse.Should().NotThrow();
            context.Requests["CreatePartner"].Should().Be(secondRequest);
            context.Responses["CreatePartner"].Should().Be(secondResponse);
        }
        else
        {
            registerRequest.Should().Throw<ArgumentException>();
            registerResponse.Should().Throw<ArgumentException>();
        }
    }
}
