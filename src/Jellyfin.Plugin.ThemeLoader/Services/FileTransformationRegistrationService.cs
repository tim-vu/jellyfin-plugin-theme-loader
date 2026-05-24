using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.ThemeLoader.Services;

public sealed class FileTransformationRegistrationService(ILogger<FileTransformationRegistrationService> logger) : IHostedService
{
    private static readonly Guid TransformationId = Guid.Parse("eb111ebb-f70f-4b7b-ac83-9868a78553ba");

    private readonly ILogger<FileTransformationRegistrationService> _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        bool registered = TryRegister();
        var plugin = Plugin.Instance;

        if (plugin is null)
        {
            return Task.CompletedTask;
        }

        plugin.Configuration.InitializationFailed = !registered;

        plugin.UpdateConfiguration(plugin.Configuration);

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
                _logger.LogWarning("File Transformation plugin not found. Theme Loader cannot inject CSS.");
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
            _logger.LogInformation("Registered Theme Loader web HTML transformation.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Theme Loader with File Transformation plugin.");
            return false;
        }
    }
}
