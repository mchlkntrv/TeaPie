using FluentAssertions;
using TeaPie.TestCases;

namespace TeaPie.Tests.TestCases;

public class TestCaseExecutionContextShould
{
    [Fact]
    public void OverwriteAPreviouslyRegisteredRequestAndResponseWithTheSameNameInsteadOfThrowing()
    {
        var context = new TestCaseExecutionContext(null!);
        var secondRequest = new HttpRequestMessage();
        var secondResponse = new HttpResponseMessage();

        context.RegisterRequest(new HttpRequestMessage(), "CreatePartner");
        context.RegisterResponse(new HttpResponseMessage(), "CreatePartner");
        context.RegisterRequest(secondRequest, "CreatePartner");
        context.RegisterResponse(secondResponse, "CreatePartner");

        context.Requests["CreatePartner"].Should().Be(secondRequest);
        context.Responses["CreatePartner"].Should().Be(secondResponse);
    }
}
