using TeaPieDraft.Pipelines.Base;

namespace TeaPieDraft.Pipelines.CollectionPipeline;

internal interface ICollectionPipelineContext<ItemType, ItemContextType> : IPipelineContext
{
    internal ItemType? Current { get; set; }
    internal IEnumerable<ItemType> Values { get; set; }
    ItemContextType? GetItemContext();
}
