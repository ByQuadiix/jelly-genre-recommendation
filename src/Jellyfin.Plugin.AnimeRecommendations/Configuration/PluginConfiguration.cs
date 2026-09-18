using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AnimeRecommendations.Configuration;

/// <summary>
/// Plugin configuration for Anime Recommendations.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        SelectedGenres = new List<string>
        {
            "Action",
            "Abenteuer",
            "Comedy",
            "Romance",
            "Fantasy",
            "Sci-Fi",
            "Drama",
            "Mystery"
        };
        ItemsPerGenre = 12;
        ExcludeWatched = false;
        RotationDay = DayOfWeek.Monday;
        LastRotationTime = DateTime.MinValue;
        StoredRecommendations = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        SectionTitle = "Anime-Empfehlungen der Woche";
    }

    /// <summary>
    /// Gets or sets the ID of the selected library (e.g. Anime).
    /// </summary>
    public Guid SelectedLibraryId { get; set; }

    /// <summary>
    /// Gets or sets the name of the selected library.
    /// </summary>
    public string SelectedLibraryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of genres to generate recommendations for.
    /// </summary>
    public List<string> SelectedGenres { get; set; }

    /// <summary>
    /// Gets or sets the number of recommended anime per genre.
    /// </summary>
    public int ItemsPerGenre { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether already watched anime should be excluded.
    /// </summary>
    public bool ExcludeWatched { get; set; }

    /// <summary>
    /// Gets or sets the weekday on which weekly rotation occurs.
    /// </summary>
    public DayOfWeek RotationDay { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when recommendations were last rotated.
    /// </summary>
    public DateTime LastRotationTime { get; set; }

    /// <summary>
    /// Gets or sets the cached weekly recommendation item IDs grouped by genre.
    /// </summary>
    public Dictionary<string, List<Guid>> StoredRecommendations { get; set; }

    /// <summary>
    /// Gets or sets the title displayed above the recommendation row on the home page.
    /// </summary>
    public string SectionTitle { get; set; }
}
