using System.Text.Encodings.Web;
using Fluid;
using Fluid.Ast;
using Fluid.Values;

namespace TeaPie.Templating;

internal sealed class LoopWrapper : AstRewriter
{
    private readonly TemplatingLimits _limits;
    private readonly Action<int, int, int> _onProgress;
    private readonly Action<int, string, int> _onLoopRendered;
    private readonly List<string> _loopVariableNames = [];
    private int _depth;

    private LoopWrapper(
        TemplatingLimits limits, Action<int, int, int> onProgress, Action<int, string, int> onLoopRendered)
    {
        _limits = limits;
        _onProgress = onProgress;
        _onLoopRendered = onLoopRendered;
    }

    // onProgress gets the top-level loop index, its request count and its output length so far, after each write;
    // onLoopRendered gets the top-level loop index, its output and its final request count.
    public static IFluidTemplate Wrap(
        IFluidTemplate template, IReadOnlyList<string> expectedLoopVariableNames, TemplatingLimits limits,
        Action<int, int, int> onProgress, Action<int, string, int> onLoopRendered)
    {
        var wrapper = new LoopWrapper(limits, onProgress, onLoopRendered);
        var wrapped = wrapper.VisitTemplate(template);

        if (!wrapper._loopVariableNames.SequenceEqual(expectedLoopVariableNames, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Templating error: internal error - the loops found in the template " +
                $"({string.Join(", ", wrapper._loopVariableNames)}) do not match the loops found by the scanner " +
                $"({string.Join(", ", expectedLoopVariableNames)}).");
        }

        return wrapped;
    }

    public override Statement Visit(Statement statement)
    {
        if (statement is not ForStatement loop)
        {
            return base.Visit(statement);
        }

        var isTopLevel = _depth == 0;
        var loopIndex = _loopVariableNames.Count;
        if (isTopLevel)
        {
            _loopVariableNames.Add(loop.Identifier);
        }

        _depth++;
        var rewritten = (ForStatement)base.Visit(loop);
        _depth--;

        return isTopLevel
            ? new TopLevelLoopStatement(
                rewritten,
                _limits.MaxLoopItems,
                (requestCount, length) => _onProgress(loopIndex, requestCount, length),
                (output, count) => _onLoopRendered(loopIndex, output, count))
            : new NestedLoopStatement(rewritten, _limits.MaxLoopItems);
    }

    protected override Statement VisitCaptureStatement(CaptureStatement captureStatement)
        => new SizeCheckedCaptureStatement((CaptureStatement)base.VisitCaptureStatement(captureStatement), _limits);

    private static async ValueTask ValidateOptions(ForStatement loop, int maxOptionValue, TemplateContext context)
    {
        await ValidateOption(loop, "limit:", loop.Limit, maxOptionValue, context);
        await ValidateOption(loop, "offset:", loop.Offset, maxOptionValue, context);
    }

    private static async ValueTask ValidateOption(
        ForStatement loop, string name, Expression? expression, int maxOptionValue, TemplateContext context)
    {
        if (expression is null)
        {
            return;
        }

        var value = await expression.EvaluateAsync(context);
        if (value is not NumberValue || !ForTagSourceParser.IsValidOptionValue(value.ToNumberValue(), maxOptionValue))
        {
            throw new InvalidOperationException(
                $"Templating error: option '{name}' of the '{{% for {loop.Identifier} %}}' loop must be a whole " +
                $"number from 0 to {maxOptionValue}, but is '{value.ToStringValue()}'.");
        }
    }

    private sealed class TopLevelLoopStatement(
        ForStatement loop, int maxOptionValue, Action<int, int> onProgress, Action<string, int> onRendered)
        : Statement
    {
        public override async ValueTask<Completion> WriteToAsync(
            TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            await ValidateOptions(loop, maxOptionValue, context);

            using var loopWriter = new RequestCountingWriter(onProgress);
            var completion = await loop.WriteToAsync(loopWriter, encoder, context);
            loopWriter.Complete();
            var output = loopWriter.ToString();

            onRendered(output, loopWriter.RequestCount);
            await writer.WriteAsync(output);

            return completion;
        }
    }

    private sealed class NestedLoopStatement(ForStatement loop, int maxOptionValue) : Statement
    {
        public override async ValueTask<Completion> WriteToAsync(
            TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            await ValidateOptions(loop, maxOptionValue, context);
            return await loop.WriteToAsync(writer, encoder, context);
        }
    }

    private sealed class SizeCheckedCaptureStatement(CaptureStatement capture, TemplatingLimits limits) : Statement
    {
        public override async ValueTask<Completion> WriteToAsync(
            TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var completion = await capture.WriteToAsync(writer, encoder, context);
            ValueSizeGuard.EnsureWithinLimits(
                context.GetValue(capture.Identifier), $"'{{% capture {capture.Identifier} %}}'", limits);

            return completion;
        }
    }
}
