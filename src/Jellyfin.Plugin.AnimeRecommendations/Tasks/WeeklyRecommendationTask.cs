using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AnimeRecommendations.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeRecommendations.Tasks;

/// <summary>
/// Scheduled task to rotate anime recommendations weekly.
/// </summary>
public class WeeklyRecommendationTask : IScheduledTask
{
    private readonly RecommendationService _recommendationService;
    private readonly ILogger<WeeklyRecommendationTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeeklyRecommendationTask"/> class.
    /// </summary>
    public WeeklyRecommendationTask(
        RecommendationService recommendationService,
        ILogger<WeeklyRecommendationTask> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Anime-Empfehlungen wöchentlich rotieren";

    /// <inheritdoc />
    public string Key => "AnimeWeeklyRecommendationsTask";

    /// <inheritdoc />
    public string Description => "Wählt jede Woche zufällige Anime aus der konfigurierten Bibliothek für die angegebenen Genres aus.";

    /// <inheritdoc />
    public string Category => "Recommendations";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var rotationDay = Plugin.Instance?.Configuration?.RotationDay ?? DayOfWeek.Monday;

        return new[]
        {
            // Run weekly on the configured day at 03:00 AM
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.WeeklyTrigger,
                DayOfWeek = rotationDay,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            },
            // Also check at server startup in case the schedule was missed
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.StartupTrigger
            }
        };
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing Weekly Anime Recommendations task...");
        progress?.Report(10);

        try
        {
            // If triggered on startup, only rotate if expired; if executed manually or scheduled, rotate
            _recommendationService.RotateRecommendations(force: false);
            progress?.Report(100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during weekly anime recommendations task.");
            throw;
        }

        return Task.CompletedTask;
    }
}
