using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeRecommendations.Services;

/// <summary>
/// Service responsible for managing weekly recommendations.
/// </summary>
public class RecommendationService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<RecommendationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationService"/> class.
    /// </summary>
    public RecommendationService(
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IUserManager userManager,
        ILogger<RecommendationService> logger)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Checks and performs the weekly rotation if needed or forced.
    /// </summary>
    /// <param name="force">Whether to force rotation regardless of time.</param>
    /// <returns>True if rotated, false otherwise.</returns>
    public bool RotateRecommendations(bool force = false)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var needsRotation = force ||
                            config.LastRotationTime == DateTime.MinValue ||
                            (now - config.LastRotationTime).TotalDays >= 7 ||
                            config.StoredRecommendations == null ||
                            config.StoredRecommendations.Count == 0;

        if (!needsRotation)
        {
            _logger.LogDebug("Weekly recommendations are still valid. Skipping rotation.");
            return false;
        }

        _logger.LogInformation("Generating fresh weekly anime recommendations...");

        // Determine target library folder
        BaseItem? targetFolder = null;
        if (config.SelectedLibraryId != Guid.Empty)
        {
            targetFolder = _libraryManager.GetItemById(config.SelectedLibraryId);
        }

        // Fallback: Find library containing 'anime' or first video library
        if (targetFolder == null)
        {
            var virtualFolders = _libraryManager.GetVirtualFolders();
            var animeFolder = virtualFolders.FirstOrDefault(f => f.Name.Contains("anime", StringComparison.OrdinalIgnoreCase));
            if (animeFolder != null && Guid.TryParse(animeFolder.ItemId, out var folderGuid))
            {
                targetFolder = _libraryManager.GetItemById(folderGuid);
                if (targetFolder != null)
                {
                    config.SelectedLibraryId = targetFolder.Id;
                    config.SelectedLibraryName = targetFolder.Name;
                }
            }
        }

        // Query all series and movies in the library (or entire server if no library specified)
        var query = new InternalItemsQuery
        {
            Recursive = true,
            IncludeItemTypes = new[] { BaseItemKind.Series, BaseItemKind.Movie },
            IsVirtualItem = false
        };

        if (targetFolder != null)
        {
            query.ParentId = targetFolder.Id;
        }

        var allItems = _libraryManager.GetItemList(query);
        _logger.LogInformation("Found {Count} total items in target library for recommendations.", allItems.Count);

        if (allItems.Count == 0)
        {
            _logger.LogWarning("No items found to generate recommendations from!");
            return false;
        }

        var newRecommendations = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        var genres = config.SelectedGenres != null && config.SelectedGenres.Count > 0
            ? config.SelectedGenres
            : new List<string> { "Action", "Abenteuer", "Comedy", "Romance", "Fantasy", "Sci-Fi" };

        var itemsPerGenre = config.ItemsPerGenre > 0 ? config.ItemsPerGenre : 12;

        foreach (var genre in genres)
        {
            var matchingItems = allItems
                .Where(item => item.Genres != null && item.Genres.Any(g => g.Equals(genre, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matchingItems.Count == 0)
            {
                // Also check partial matching (e.g. "Adventure" / "Abenteuer")
                matchingItems = allItems
                    .Where(item => item.Genres != null && item.Genres.Any(g => g.Contains(genre, StringComparison.OrdinalIgnoreCase) || genre.Contains(g, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            // Shuffle randomly
            var shuffled = matchingItems.OrderBy(_ => Random.Shared.Next()).ToList();
            var selectedIds = shuffled.Take(itemsPerGenre).Select(i => i.Id).ToList();

            newRecommendations[genre] = selectedIds;
            _logger.LogInformation("Selected {Count} recommendations for genre '{Genre}'.", selectedIds.Count, genre);
        }

        config.RecommendationsMap = newRecommendations;
        config.LastRotationTime = now;
        Plugin.Instance?.SaveConfiguration();

        _logger.LogInformation("Weekly anime recommendations generated successfully!");
        return true;
    }

    /// <summary>
    /// Gets the weekly recommendations formatted for client consumption.
    /// </summary>
    /// <param name="userId">Optional user ID for personalized watch states and filtering.</param>
    /// <returns>Recommendations response object.</returns>
    public WeeklyRecommendationsResponse GetWeeklyRecommendations(Guid? userId = null)
    {
        var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();

        // Ensure we have recommendations
        if (config.StoredRecommendations == null || config.StoredRecommendations.Count == 0)
        {
            RotateRecommendations(force: true);
        }

        User? user = null;
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            user = _userManager.GetUserById(userId.Value);
        }

        var response = new WeeklyRecommendationsResponse
        {
            Title = string.IsNullOrWhiteSpace(config.SectionTitle) ? "Anime-Empfehlungen der Woche" : config.SectionTitle,
            LastRotationTime = config.LastRotationTime,
            Genres = config.SelectedGenres?.ToList() ?? new List<string>()
        };

        var allItemIds = new HashSet<Guid>();
        if (config.StoredRecommendations != null)
        {
            foreach (var group in config.StoredRecommendations)
            {
                if (group.ItemIds != null)
                {
                    foreach (var id in group.ItemIds)
                    {
                        allItemIds.Add(id);
                    }
                }
            }
        }

        var itemsList = new List<RecommendationItemDto>();

        foreach (var id in allItemIds)
        {
            var item = _libraryManager.GetItemById(id);
            if (item == null)
            {
                continue;
            }

            bool isPlayed = false;
            if (user != null)
            {
                var userData = _userDataManager.GetUserData(user, item);
                if (userData != null)
                {
                    isPlayed = userData.Played;
                }
            }

            // Exclude watched if configured
            if (config.ExcludeWatched && isPlayed)
            {
                continue;
            }

            string? primaryImageTag = null;
            try
            {
                var img = item.GetImageInfo(ImageType.Primary, 0);
                if (img != null)
                {
                    primaryImageTag = img.DateModified.Ticks.ToString();
                }
            }
            catch
            {
                // Ignored if image info is not set
            }

            var dto = new RecommendationItemDto
            {
                Id = item.Id,
                Name = item.Name,
                ProductionYear = item.ProductionYear,
                CommunityRating = item.CommunityRating,
                Genres = item.Genres?.ToList() ?? new List<string>(),
                PrimaryImageTag = primaryImageTag,
                Played = isPlayed,
                Type = item.GetType().Name,
                Overview = item.Overview
            };

            itemsList.Add(dto);
        }

        response.Items = itemsList;
        return response;
    }
}

/// <summary>
/// Response model for weekly recommendations.
/// </summary>
public class WeeklyRecommendationsResponse
{
    /// <summary>
    /// Gets or sets the title of the section.
    /// </summary>
    public string Title { get; set; } = "Anime-Empfehlungen der Woche";

    /// <summary>
    /// Gets or sets the list of active genres.
    /// </summary>
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of recommendation items.
    /// </summary>
    public List<RecommendationItemDto> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when recommendations were last rotated.
    /// </summary>
    public DateTime LastRotationTime { get; set; }
}

/// <summary>
/// DTO representing a recommended item.
/// </summary>
public class RecommendationItemDto
{
    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the production year.
    /// </summary>
    public int? ProductionYear { get; set; }

    /// <summary>
    /// Gets or sets the community rating.
    /// </summary>
    public float? CommunityRating { get; set; }

    /// <summary>
    /// Gets or sets the genres.
    /// </summary>
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// Gets or sets the primary image tag.
    /// </summary>
    public string? PrimaryImageTag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this item has been played by the requesting user.
    /// </summary>
    public bool Played { get; set; }

    /// <summary>
    /// Gets or sets the item type (Series, Movie, etc.).
    /// </summary>
    public string Type { get; set; } = "Series";

    /// <summary>
    /// Gets or sets the overview text.
    /// </summary>
    public string? Overview { get; set; }
}
