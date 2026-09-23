using LeslieCleaner.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeslieCleaner.Infrastructure.Plugins;

/// <summary>Registers the isolated plugin runtime independently of UI composition.</summary>
public static class PluginRuntimeRegistration
{
    /// <summary>Adds the singleton out-of-process plugin runtime.</summary>
    public static IServiceCollection AddLeslieCleanerPluginRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IPluginRuntime, PluginRuntime>();
        return services;
    }
}
