using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
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
        StoredRecommendations = new List<GenreRecommendationGroup>();
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
    /// Using a List of GenreRecommendationGroup instead of IDictionary ensures compatibility with XmlSerializer.
    /// </summary>
    public List<GenreRecommendationGroup> StoredRecommendations { get; set; }

    /// <summary>
    /// Gets or sets a helper dictionary representation for code convenience, ignored by XmlSerializer.
    /// </summary>
    [XmlIgnore]
    public Dictionary<string, List<Guid>> RecommendationsMap
    {
        get
        {
            var dict = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
            if (StoredRecommendations != null)
            {
                foreach (var group in StoredRecommendations)
                {
                    if (!string.IsNullOrWhiteSpace(group.Genre))
                    {
                        dict[group.Genre] = group.ItemIds ?? new List<Guid>();
                    }
                }
            }
            return dict;
        }
        set
        {
            StoredRecommendations = value?.Select(kv => new GenreRecommendationGroup
            {
                Genre = kv.Key,
                ItemIds = kv.Value ?? new List<Guid>()
            }).ToList() ?? new List<GenreRecommendationGroup>();
        }
    }

    /// <summary>
    /// Gets or sets the title displayed above the recommendation row on the home page.
    /// </summary>
    public string SectionTitle { get; set; }
}

/// <summary>
/// Represents a genre recommendation group for XML serialization.
/// </summary>
public class GenreRecommendationGroup
{
    /// <summary>
    /// Gets or sets the genre name.
    /// </summary>
    public string Genre { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of recommended item IDs for this genre.
    /// </summary>
    public List<Guid> ItemIds { get; set; } = new();
}
