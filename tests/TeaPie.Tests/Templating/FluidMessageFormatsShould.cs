using System.Text.RegularExpressions;
using Fluid;
using FluentAssertions;
using TeaPie.Templating;

namespace TeaPie.Tests.Templating;

// Pins the Fluid behavior that TeaPie parses, so that a Fluid upgrade that changes it fails here first.
public class FluidMessageFormatsShould
{
    [Fact]
    public void KeepTheFormatsThatTeaPieReliesOn()
    {
        var parser = new FluidParser();

        parser.TryParse("{% badtag %}", out _, out var error);
        Regex.IsMatch(error!, FluidMessageFormats.ParseErrorPositionPattern).Should().BeTrue(
            $"FluidParseErrorMapper.PositionRegex relies on this exact format, but Fluid returned: '{error}'");

        parser.TryParse("{% for i in (1..50) %}{{ i }}{% endfor %}", out var template, out _);
        var exhausted = new TemplateContext(new TemplateOptions { MaxSteps = 10 });
        var withinTheLimit = new TemplateContext(new TemplateOptions { MaxSteps = 1000 });
        var renderExhausted = () => template!.Render(exhausted);
        renderExhausted.Should().Throw<InvalidOperationException>();
        template!.Render(withinTheLimit);

        var stepAfterTheLimit = exhausted.IncrementSteps;
        var stepWithinTheLimit = withinTheLimit.IncrementSteps;
        stepAfterTheLimit.Should().Throw<InvalidOperationException>(
            "TemplateExpander.IsRenderStepLimitExceeded relies on this to tell the step limit from other errors");
        stepWithinTheLimit.Should().NotThrow();
    }
}
