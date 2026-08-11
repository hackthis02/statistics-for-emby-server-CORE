using System.Collections.Generic;
using MediaBrowser.Model.Plugins;
using statistics.Models;
using statistics.Models.Configuration;

namespace statistics.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            UserStats = new List<UserStat>();
            showUnknownDVProfileCount = true;
            enableHyperlinks = false;
        }
        public List<UserStat> UserStats { get; set; }

        public ValueGroup MovieQualities { get; set; }
        public ValueGroup MovieCodecs { get; set; }
        public ValueGroup DolbyVisionProfiles { get; set; }

        public ValueGroup MostActiveUsers { get; set; }
        public ValueGroup TotalUsers { get; set; }

        public ValueGroup TotalMovies { get; set; }
        public ValueGroup TotalBoxsets { get; set; }
        public ValueGroup TotalMovieStudios { get; set; }
        public ValueGroup BiggestMovie { get; set; }
        public ValueGroup LongestMovie { get; set; }
        public ValueGroup OldestMovie { get; set; }
        public ValueGroup NewestMovie { get; set; }
        public ValueGroup HighestRating { get; set; }
        public ValueGroup LowestRating { get; set; }
        public ValueGroup NewestAddedMovie { get; set; }
        public ValueGroup HighestBitrateMovie { get; set; }
        public ValueGroup LowestBitrateMovie { get; set; }

        public ValueGroup TotalShows { get; set; }
        public ValueGroup TotalOwnedEpisodes { get; set; }
        public ValueGroup TotalShowStudios { get; set; }
        public ValueGroup MostWatchedShows { get; set; }
        public ValueGroup LeastWatchedShows { get; set; }
        public ValueGroup BiggestShow { get; set; }
        public ValueGroup LongestShow { get; set; }
        public ValueGroup NewestAddedEpisode { get; set; }
        public ValueGroup OldestShow { get; set; }
        public ValueGroup NewestShow { get; set; }
        public string BuildDate { get; set; }
        public string LastUpdated { get; set; }
        public string Version { get; set; }
        public string ServerId { get; set; }

        public bool enableHyperlinks { get; set; }
        public bool showUnknownDVProfileCount { get; set; }
        public MediaItemCollection MovieCodecItems { get; set; }
        public MediaItemCollection EpisodeCodecItems { get; set; }
        public MediaItemCollection MovieDVProfileItems { get; set; }
        public MediaItemCollection EpisodeDVProfileItems { get; set; }
        
        public static bool IsUnknownDolbyProfile(string profile)
        {
            if (string.IsNullOrEmpty(profile))
                return true;
            var unknownProfiles = new List<string>
            {
                "Unknown Media",
                "Unknown Dolby Profile",
                "Non Dolby Vision Compatible Codec",
                "No Dolby Profile"
            };
            return unknownProfiles.Contains(profile);
        }
    }
}

