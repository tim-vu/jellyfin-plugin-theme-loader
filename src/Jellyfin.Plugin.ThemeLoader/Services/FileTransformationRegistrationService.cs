using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ThemeLoader.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.ThemeLoader.Services;

public sealed class FileTransformationRegistrationService(
    ILogger<FileTransformationRegistrationService> logger,
    IServiceScopeFactory scopeFactory) : IHostedService
{
    private static readonly Guid TransformationId = Guid.Parse("eb111ebb-f70f-4b7b-ac83-9868a78553ba");

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var stateRepository = scope.ServiceProvider.GetRequiredService<IStateRepository>();
        var registered = TryRegister();
        var state = stateRepository.Get();
        state.SetInitializationFailed(!registered);
        stateRepository.Save();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
                ["fileNamePattern"] = @"(?i)(index|web)\.html$",
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
