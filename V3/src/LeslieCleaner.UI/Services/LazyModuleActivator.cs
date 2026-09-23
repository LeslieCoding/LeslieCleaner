using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace LeslieCleaner.UI.Services;

/// <summary>
/// 模块懒加载激活器。容器注册服务经 <see cref="Resolve{T}"/> 获取；
/// 模块引擎（具体类型）经 <see cref="Create{T}"/> 按程序集+类型名延迟加载并构造
/// （构造函数依赖由 DI 容器注入，模块专属依赖经显式参数提供）。
/// 调用点方法的泛型参数决定模块程序集何时加载：首次页面激活/首次使用，而非进程启动。
/// </summary>
public interface ILazyModuleActivator
{
    /// <summary>从 DI 容器解析已注册服务。</summary>
    T Resolve<T>() where T : notnull;

    /// <summary>延迟加载模块程序集并构造具体引擎类型（单例语义由调用方字段缓存）。</summary>
    /// <typeparam name="T">返回类型（通常为具体引擎类型）。</typeparam>
    /// <param name="assemblyFile">模块程序集文件名（相对应用目录）。</param>
    /// <param name="typeName">全名类型名。</param>
    /// <param name="explicitArgs">容器无法解析的显式构造参数（按类型匹配）。</param>
    T Create<T>(string assemblyFile, string typeName, params object[] explicitArgs) where T : class;
}

/// <summary><see cref="ILazyModuleActivator"/> 实现。</summary>
public sealed class LazyModuleActivator(IServiceProvider services) : ILazyModuleActivator
{
    /// <inheritdoc />
    public T Resolve<T>() where T : notnull => services.GetRequiredService<T>();

    /// <inheritdoc />
    public T Create<T>(string assemblyFile, string typeName, params object[] explicitArgs) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        Assembly assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, assemblyFile));
        Type type = assembly.GetType(typeName, throwOnError: true)!;
        return (T)ActivatorUtilities.CreateInstance(services, type, explicitArgs);
    }
}
