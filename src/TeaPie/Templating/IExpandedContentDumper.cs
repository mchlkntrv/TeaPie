using TeaPie.StructureExploration;

namespace TeaPie.Templating;

internal interface IExpandedContentDumper
{
    void Dump(ApplicationContext context, TestCase testCase, string expandedContent);
}
