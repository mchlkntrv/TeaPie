using System.Text.Encodings.Web;
using Fluid;
using Fluid.Ast;
using Fluid.Values;

namespace TeaPie.Templating;

internal sealed class RenderedValueGuard : AstRewriter
{
    private readonly TemplateDataValues _dataValues;

    private RenderedValueGuard(TemplateDataValues dataValues)
    {
        _dataValues = dataValues;
    }

    public static IFluidTemplate Apply(IFluidTemplate template, TemplateDataValues dataValues)
        => new RenderedValueGuard(dataValues).VisitTemplate(template);

    protected override Statement VisitOutputStatement(OutputStatement outputStatement)
        => Guard(outputStatement);

    protected override Statement VisitCycleStatement(CycleStatement cycleStatement)
        => Guard(cycleStatement);

    private GuardedStatement Guard(Statement statement)
        => new(statement, BracesLiteralFinder.ContainsBracesLiteral(statement), _dataValues);

    private sealed class GuardedStatement(
        Statement statement, bool bracesWrittenInTemplate, TemplateDataValues dataValues) : Statement
    {
        public override async ValueTask<Completion> WriteToAsync(
            TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            using var valueWriter = new StringWriter();
            var completion = await statement.WriteToAsync(valueWriter, encoder, context);

            await writer.WriteAsync(dataValues.Protect(valueWriter.ToString(), bracesWrittenInTemplate));

            return completion;
        }
    }

    private sealed class BracesLiteralFinder : AstVisitor
    {
        private bool _found;

        public static bool ContainsBracesLiteral(Statement statement)
        {
            var finder = new BracesLiteralFinder();
            finder.Visit(statement);
            return finder._found;
        }

        protected override Expression VisitLiteralExpression(LiteralExpression literalExpression)
        {
            if (literalExpression.Value is StringValue literal
                && (literal.ToStringValue().Contains("{{", StringComparison.Ordinal)
                    || literal.ToStringValue().Contains("}}", StringComparison.Ordinal)))
            {
                _found = true;
            }

            return base.VisitLiteralExpression(literalExpression);
        }
    }
}
