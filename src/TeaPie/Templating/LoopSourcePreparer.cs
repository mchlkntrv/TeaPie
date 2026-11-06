using TeaPie.Variables;
using static TeaPie.Templating.TemplatingMessageText;

namespace TeaPie.Templating;

internal sealed class LoopSourcePreparer(
    ICollectionSourceResolver sourceResolver, IVariables variables, TemplatingLimits limits)
{
    private const string SourceAliasPrefix = "__teapie_loop_source_";

    public LoopSource?[] Prepare(
        TemplateExpansionInput input, IReadOnlyDictionary<string, object?> model, List<TextEdit> edits)
    {
        var (content, filePath, blocks, forSources, isDynamic) = input;
        var sources = new LoopSource?[blocks.Count];
        var assignOccurrences = AssignTags.FindOccurrences(content);

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var forSource = forSources[i];

            if (isDynamic[i])
            {
                AddSourceEdit(edits, block, forSource.ToFluidSource(forSource.CollectionExpression));
                continue;
            }

            EnsureSourceIsNotReassigned(input, i, forSource.CollectionExpression, assignOccurrences);
            var source = TemplatingErrors.InFile(
                filePath, () => sourceResolver.Resolve(forSource.CollectionExpression, model));
            sources[i] = source;

            if (source.Collection is null && LoopElseBranch.Find(block.Body) is not null)
            {
                var (line, column) = FluidParseErrorMapper.ToLineColumn(content, block.StartIndex);
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': the loop over the numeric range " +
                    $"'{Shorten(forSource.CollectionExpression)}' at ({line}:{column}) has an '{{% else %}}' branch, " +
                    "which is not supported for a numeric range. Remove the '{% else %}' branch, or loop over a " +
                    "collection variable (e.g. set in the '-init.csx' script) or an inline list such as (1, 2, 3).");
            }

            if (source.ItemCount == 0)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': loop over '{Shorten(forSource.CollectionExpression)}' produced " +
                    "zero items.");
            }

            if (source.ItemCount > limits.MaxLoopItems)
            {
                throw new InvalidOperationException(
                    $"Templating error in '{filePath}': loop over '{Shorten(forSource.CollectionExpression)}' has " +
                    $"{source.ItemCount} items, exceeding the maximum of {limits.MaxLoopItems} items per loop.");
            }

            var collection = source.Collection is not null ? $"{SourceAliasPrefix}{i}" : forSource.CollectionExpression;
            AddSourceEdit(edits, block, forSource.ToFluidSource(collection));
        }

        return sources;
    }

    public static void AddSourceAliases(Dictionary<string, object?> model, LoopSource?[] sources)
    {
        for (var i = 0; i < sources.Length; i++)
        {
            if (sources[i] is { Collection: not null } source)
            {
                model[$"{SourceAliasPrefix}{i}"] = source.Collection;
            }
        }
    }

    private void EnsureSourceIsNotReassigned(
        TemplateExpansionInput input, int blockIndex, string sourceExpression,
        List<AssignTags.Occurrence> assignOccurrences)
    {
        if (!variables.ContainsVariable(sourceExpression))
        {
            return;
        }

        var blocks = input.Blocks;
        var block = blocks[blockIndex];
        var ancestors = LoopBlockHierarchy.GetAncestorIndices(blockIndex, blocks);
        var visibleBefore = ancestors.Count == 0
            ? block.StartIndex
            : blocks[ancestors[^1]].StartIndex + blocks[ancestors[^1]].Length;

        var assignedName = assignOccurrences
            .Where(occurrence => occurrence.Position < visibleBefore
                && FluidExpressionIdentifier.StartsWithIdentifier(sourceExpression, occurrence.Name))
            .Select(occurrence => occurrence.Name)
            .FirstOrDefault();

        if (assignedName is not null)
        {
            var (line, column) = FluidParseErrorMapper.ToLineColumn(input.Content, block.StartIndex);
            throw new InvalidOperationException(
                $"Templating error in '{input.FilePath}': the loop source '{Shorten(sourceExpression)}' at ({line}:{column}) " +
                $"is reassigned with '{{% assign {Shorten(assignedName)} %}}', which is not supported, because the loop " +
                "would still read the TeaPie variable. Rename the assigned name, or prepare the collection in the " +
                "'-init.csx' script.");
        }
    }

    private static void AddSourceEdit(List<TextEdit> edits, LoopBlock block, string fluidSource)
    {
        if (!string.Equals(fluidSource, block.SourceExpression, StringComparison.Ordinal))
        {
            edits.Add(new TextEdit(block.SourceExpressionStartIndex, block.SourceExpressionRawLength, fluidSource));
        }
    }
}
