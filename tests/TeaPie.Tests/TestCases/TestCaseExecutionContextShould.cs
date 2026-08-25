using FluentAssertions;
using TeaPie.TestCases;

namespace TeaPie.Tests.TestCases;

public class TestCaseExecutionContextShould
{
    [Fact]
    public void RegisterARequestWithAUniqueNameWithoutIssue()
    {
        var context = new TestCaseExecutionContext(null!);
        var request = new HttpRequestMessage();

        var act = () => context.RegisterRequest(request, "CreatePartner");

        act.Should().NotThrow();
        context.Requests.Should().ContainKey("CreatePartner").WhoseValue.Should().Be(request);
    }

    [Fact]
    public void OverwriteAPreviouslyRegisteredRequestWithTheSameNameInsteadOfThrowing()
    {
        var context = new TestCaseExecutionContext(null!);
        var firstRequest = new HttpRequestMessage();
        var secondRequest = new HttpRequestMessage();

        context.RegisterRequest(firstRequest, "CreatePartner");
        var act = () => context.RegisterRequest(secondRequest, "CreatePartner");

        act.Should().NotThrow();
        context.Requests.Should().ContainKey("CreatePartner").WhoseValue.Should().Be(secondRequest);
    }

    [Fact]
    public void OverwriteAPreviouslyRegisteredResponseWithTheSameNameInsteadOfThrowing()
    {
        var context = new TestCaseExecutionContext(null!);
        var firstResponse = new HttpResponseMessage();
        var secondResponse = new HttpResponseMessage();

        context.RegisterResponse(firstResponse, "CreatePartner");
        var act = () => context.RegisterResponse(secondResponse, "CreatePartner");

        act.Should().NotThrow();
        context.Responses.Should().ContainKey("CreatePartner").WhoseValue.Should().Be(secondResponse);
    }
}
