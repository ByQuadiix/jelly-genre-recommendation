using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AnimeRecommendations.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeRecommendations;

/// <summary>
/// Registers plugin services into the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<RecommendationService>();
        serviceCollection.AddHostedService<FileTransformationHostedService>();
    }
}

/// <summary>
/// Hosted service that runs when all Jellyfin plugins and services are initialized.
/// Ensures FileTransformation registration occurs after FileTransformation is loaded.
/// </summary>
public class FileTransformationHostedService : IHostedService
{
    private readonly ILogger<FileTransformationHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTransformationHostedService"/> class.
    /// </summary>
    public FileTransformationHostedService(ILogger<FileTransformationHostedService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[AnimeRecommendations] FileTransformationHostedService: Registering File Transformation on startup...");
        FileTransformationIntegration.TryRegister(_logger);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
