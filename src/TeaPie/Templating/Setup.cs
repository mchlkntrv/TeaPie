using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace TeaPie.Templating;

internal static class Setup
{
    public static IServiceCollection AddTemplating(this IServiceCollection services)
    {
        services.AddSingleton<ILoopBlockScanner, LoopBlockScanner>();
        services.AddSingleton<INonFluidSyntaxMasker, NonFluidSyntaxMasker>();
        services.AddSingleton<ICollectionSourceResolver, CollectionSourceResolver>();
        services.AddSingleton<IVariablesFluidModelBuilder, VariablesFluidModelBuilder>();
        services.AddSingleton(new TemplatingLimits());
        services.AddSingleton<ITemplateExpander, TemplateExpander>();

        services.AddSingleton<IExpandedContentDumper>(_ => new ExpandedContentDumper(AnsiConsole.Console));

        return services;
    }
}
