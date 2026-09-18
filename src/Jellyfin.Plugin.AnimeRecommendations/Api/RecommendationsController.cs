using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Jellyfin.Plugin.AnimeRecommendations.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeRecommendations.Api;

/// <summary>
/// API Controller for Anime Recommendations.
/// </summary>
[ApiController]
[Route("Recommendations")]
[Produces("application/json")]
public class RecommendationsController : ControllerBase
{
    private readonly RecommendationService _recommendationService;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<RecommendationsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationsController"/> class.
    /// </summary>
    public RecommendationsController(
        RecommendationService recommendationService,
        ILibraryManager libraryManager,
        ILogger<RecommendationsController> logger)
    {
        _recommendationService = recommendationService;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets the weekly anime recommendations.
    /// </summary>
    /// <param name="userId">Optional user ID for personalized filtering.</param>
    /// <returns>Weekly recommendations.</returns>
    [HttpGet("Weekly")]
    public ActionResult<WeeklyRecommendationsResponse> GetWeeklyRecommendations([FromQuery] Guid? userId)
    {
        try
        {
            var result = _recommendationService.GetWeeklyRecommendations(userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weekly recommendations.");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets all virtual folders/libraries to populate library selector.
    /// </summary>
    /// <returns>List of libraries.</returns>
    [HttpGet("Libraries")]
    public ActionResult<IEnumerable<LibraryFolderDto>> GetLibraries()
    {
        try
        {
            var list = new Dictionary<Guid, string>();

            // Source 1: Virtual folders
            try
            {
                var virtualFolders = _libraryManager.GetVirtualFolders();
                if (virtualFolders != null)
                {
                    foreach (var f in virtualFolders)
                    {
                        if (Guid.TryParse(f.ItemId, out var g) && !string.IsNullOrWhiteSpace(f.Name))
                        {
                            list[g] = f.Name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch virtual folders.");
            }

            // Source 2: UserRootFolder children
            try
            {
                var root = _libraryManager.GetUserRootFolder();
                if (root?.Children != null)
                {
                    foreach (var child in root.Children)
                    {
                        if (child != null && child.Id != Guid.Empty && !string.IsNullOrWhiteSpace(child.Name))
                        {
                            list[child.Id] = child.Name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch UserRootFolder children.");
            }

            var result = list.Select(kv => new LibraryFolderDto { Id = kv.Key, Name = kv.Value }).ToList();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching libraries.");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Forces an immediate re-roll of weekly recommendations.
    /// </summary>
    /// <returns>Updated recommendations.</returns>
    [HttpPost("Reroll")]
    public ActionResult<WeeklyRecommendationsResponse> RerollRecommendations()
    {
        try
        {
            _recommendationService.RotateRecommendations(force: true);
            var result = _recommendationService.GetWeeklyRecommendations();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-rolling recommendations.");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Serves the client-side JavaScript file to be injected or included into the Jellyfin Web UI.
    /// </summary>
    /// <returns>JavaScript file content.</returns>
    [HttpGet("ClientScript.js")]
    [Produces("application/javascript")]
    public IActionResult GetClientScript()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{assembly.GetName().Name}.Web.recommendations.js";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                // Fallback: look for any resource ending in recommendations.js
                var found = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("recommendations.js", StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    using var fallbackStream = assembly.GetManifestResourceStream(found);
                    if (fallbackStream != null)
                    {
                        using var reader = new StreamReader(fallbackStream, Encoding.UTF8);
                        return Content(reader.ReadToEnd(), "application/javascript", Encoding.UTF8);
                    }
                }

                return NotFound("// recommendations.js not found in assembly resources");
            }

            using var streamReader = new StreamReader(stream, Encoding.UTF8);
            var script = streamReader.ReadToEnd();
            return Content(script, "application/javascript", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving client script.");
            return StatusCode(500, $"// Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets diagnostic status information.
    /// </summary>
    /// <returns>Status object.</returns>
    [HttpGet("Status")]
    public ActionResult<object> GetStatus()
    {
        var config = Plugin.Instance?.Configuration;
        return Ok(new
        {
            isFileTransformationActive = FileTransformationIntegration.IsFileTransformationActive,
            lastRotationTime = config?.LastRotationTime,
            libraryName = config?.SelectedLibraryName,
            libraryId = config?.SelectedLibraryId,
            itemsPerGenre = config?.ItemsPerGenre,
            excludeWatched = config?.ExcludeWatched,
            genres = config?.SelectedGenres,
            totalCachedItems = config?.StoredRecommendations?.Values.Sum(l => l.Count) ?? 0
        });
    }
}

/// <summary>
/// Simple DTO representing a library folder.
/// </summary>
public class LibraryFolderDto
{
    /// <summary>
    /// Gets or sets the library ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the library display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
