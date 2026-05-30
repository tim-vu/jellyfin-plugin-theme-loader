using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Repositories;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.ThemeLoader.Tasks;

public sealed class StartupTask(
    IServiceScopeFactory scopeFactory,
    ILogger<StartupTask> logger) : IScheduledTask
{
    private static readonly Guid TransformationId = Guid.Parse("eb111ebb-f70f-4b7b-ac83-9868a78553ba");

    public string Name => "ThemeLoader Startup";

    public string Key => "JellyFin.Plugin.ThemeLoader.Startup";

    public string Description => "Startup service for ThemeLoader";

    public string Category => "Startup services";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var stateRepository = scope.ServiceProvider.GetRequiredService<IStateRepository>();
        var state = stateRepository.Get();
        var registered = TryRegister();
        state.SetInitializationFailed(!registered);
        stateRepository.Save();

        return Task.CompletedTask;
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo()
        {
            Type = TaskTriggerInfoType.StartupTrigger
        };
    }

    private bool TryRegister()
    {
        try
        {
            var fileTransformationAssembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(assembly => assembly.FullName?.Contains(".FileTransformation", StringComparison.OrdinalIgnoreCase) == true);

            var pluginInterfaceType = fileTransformationAssembly?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            var registerMethod = pluginInterfaceType?.GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.Static);

            if (registerMethod is null)
            {
                logger.LogWarning("File Transformation plugin not found. Theme Loader cannot inject CSS");
                return false;
            }

            JObject payload = new()
            {
                ["id"] = TransformationId,
                ["fileNamePattern"] = "index.html",
                ["callbackAssembly"] = typeof(FileTransformationCallback).Assembly.FullName,
                ["callbackClass"] = typeof(FileTransformationCallback).FullName,
                ["callbackMethod"] = nameof(FileTransformationCallback.Transform)
            };

            registerMethod.Invoke(null, [payload]);

            logger.LogInformation("Registered Theme Loader web HTML transformation");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register Theme Loader with File Transformation plugin");
            return false;
        }
    }
}