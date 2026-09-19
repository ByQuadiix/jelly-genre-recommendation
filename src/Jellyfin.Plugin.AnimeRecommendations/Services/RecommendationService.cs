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
    private static readonly System.Threading.SemaphoreSlim _rotationLock = new(1, 1);
    private static Dictionary<string, List<Guid>>? _memoryCache;

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
    /// <summary>
    /// Checks and performs the weekly rotation if needed or forced.
    /// </summary>
    /// <param name="force">Whether to force rotation regardless of time.</param>
    /// <returns>True if rotated, false otherwise.</returns>
    public bool RotateRecommendations(bool force = false)
    {
        _rotationLock.Wait();
        try
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null)
            {
                _logger.LogWarning("Plugin configuration is null. Skipping rotation.");
                return false;
            }

            var now = DateTime.UtcNow;
            var needsRotation = force ||
                                config.LastRotationTime == DateTime.MinValue ||
                                (now - config.LastRotationTime).TotalDays >= 7 ||
                                config.StoredRecommendations == null ||
                                config.StoredRecommendations.Count == 0 ||
                                _memoryCache == null ||
                                _memoryCache.Count == 0;

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

            _memoryCache = newRecommendations;
            config.RecommendationsMap = newRecommendations;
            config.LastRotationTime = now;
            Plugin.Instance?.SaveConfiguration();

            _logger.LogInformation("Weekly anime recommendations generated successfully with {Count} genres!", newRecommendations.Count);
            return true;
        }
        finally
        {
            _rotationLock.Release();
        }
    }

    /// <summary>
    /// Gets the weekly recommendations formatted for client consumption.
    /// </summary>
    /// <param name="userId">Optional user ID for personalized watch states and filtering.</param>
    /// <returns>Recommendations response object.</returns>
    public WeeklyRecommendationsResponse GetWeeklyRecommendations(Guid? userId = null)
    {
        var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();

        // Check if we need rotation
        bool hasStored = (config.StoredRecommendations != null && config.StoredRecommendations.Count > 0) ||
                         (_memoryCache != null && _memoryCache.Count > 0);

        if (!hasStored)
        {
            RotateRecommendations(force: true);
            config = Plugin.Instance?.Configuration ?? config;
        }

        // Sync memory cache with config if needed
        if (_memoryCache == null || _memoryCache.Count == 0)
        {
            _memoryCache = config.RecommendationsMap;
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
        if (_memoryCache != null && _memoryCache.Count > 0)
        {
            foreach (var kvp in _memoryCache)
            {
                foreach (var id in kvp.Value)
                {
                    allItemIds.Add(id);
                }
            }
        }
        else if (config.StoredRecommendations != null)
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

        _logger.LogInformation("GetWeeklyRecommendations: processing {Count} unique item IDs for user {UserId} (ExcludeWatched={ExcludeWatched})",
            allItemIds.Count, userId, config.ExcludeWatched);

        var itemsList = new List<RecommendationItemDto>();
        var skippedWatched = new List<RecommendationItemDto>();

        foreach (var id in allItemIds)
        {
            var item = _libraryManager.GetItemById(id);
            if (item == null)
            {
                _logger.LogWarning("GetWeeklyRecommendations: Item ID {Id} not found in library manager.", id);
                continue;
            }

            bool isPlayed = false;
            if (user != null)
            {
                var userData = _userDataManager.GetUserData(user, item);
                if (userData != null)
                {
                    isPlayed = item.IsPlayed(user, userData) || userData.Played;
                }
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

            // Exclude watched if configured
            if (config.ExcludeWatched && isPlayed)
            {
                skippedWatched.Add(dto);
                continue;
            }

            itemsList.Add(dto);
        }

        // Fallback: If ExcludeWatched filtered out everything, include them anyway so row is never empty
        if (itemsList.Count == 0 && skippedWatched.Count > 0)
        {
            _logger.LogWarning("All {Count} items were watched by user {UserId}. Falling back to showing watched items so recommendations row is not empty.",
                skippedWatched.Count, userId);
            itemsList.AddRange(skippedWatched);
        }

        _logger.LogInformation("GetWeeklyRecommendations completed. Returning {Count} items.", itemsList.Count);
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
