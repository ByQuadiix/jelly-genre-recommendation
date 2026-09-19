using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AnimeRecommendations.Configuration;
using Jellyfin.Plugin.AnimeRecommendations.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeRecommendations;

/// <summary>
/// The main plugin class for Anime Recommendations.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The unique plugin ID.
    /// </summary>
    public static readonly Guid PluginId = Guid.Parse("c5e4a8b2-6d1f-4b9e-9a72-8f1e5d3c9a01");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of <see cref="IApplicationPaths"/>.</param>
    /// <param name="xmlSerializer">Instance of <see cref="IXmlSerializer"/>.</param>
    /// <param name="logger">Instance of <see cref="ILogger{Plugin}"/>.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        Logger = logger;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    public ILogger<Plugin> Logger { get; }

    /// <inheritdoc />
    public override string Name => "Anime Recommendations";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <inheritdoc />
    public override string Description => "Wöchentlich rotierende Anime-Empfehlungen nach Genre auf der Startseite.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "animerecommendations",
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            }
        };
    }
}
