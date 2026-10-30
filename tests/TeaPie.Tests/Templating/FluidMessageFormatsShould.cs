using System.Text.RegularExpressions;
using Fluid;
using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

public class FluidMessageFormatsShould
{
    [Theory]
    [InlineData(10, true)]
    [InlineData(1000, false)]
    public void KeepRejectingStepsOnlyAfterTheStepLimitWasExceeded(int maxSteps, bool exhausted)
    {
        var parser = new FluidParser();
        parser.TryParse("{% for i in (1..50) %}{{ i }}{% endfor %}", out var template, out _);
        var context = new TemplateContext(new TemplateOptions { MaxSteps = maxSteps });

        try
        {
            template!.Render(context);
        }
        catch (InvalidOperationException)
        {
        }

        var act = context.IncrementSteps;

        if (exhausted)
        {
            act.Should().Throw<InvalidOperationException>(
                "TemplateExpander.IsRenderStepLimitExceeded relies on this to tell the step limit from other errors");
        }
        else
        {
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void StillReportParseErrorsInTheExpectedAtLineColumnFormat()
    {
        var parser = new FluidParser();

        parser.TryParse("{% badtag %}", out _, out var error);

        Regex.IsMatch(error!, FluidMessageFormats.ParseErrorPositionPattern).Should().BeTrue(
            $"FluidParseErrorMapper.PositionRegex relies on this exact format, but Fluid returned: '{error}'");
    }
}
