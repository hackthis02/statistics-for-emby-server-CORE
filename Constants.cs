﻿namespace Statistics
{
    internal static class Constants
    {
        internal const string Name = "Statistics";
        internal const string ShowProgressName = "UserBasedShowOverview";
        internal const string MainStatisticsName = "UserBasedStatistics";
        internal const string Description = "Get statistics from your collection";

        internal const string FavoriteMovieGenres = "Favorite Movie Genres";
        internal const string favoriteShowGenres = "Favorite Show Genres";
        internal const string LastSeenMovies = "Last Seen Movies";
        internal const string LastSeenShows = "Last Seen TV Series";
        internal const string TotalWatched = "Total Time Watched";
        internal const string TotalWatchableTime = "Total Watchable Time";
        internal const string FavoriteYears = "Favorite Movie Years";
        internal const string MostActiveUsers = "Most Active Users";
        internal const string TotalMovies = "Total Movies";
        internal const string TotalMoviesWatched = "Total Movies Watched";
        internal const string TotalCollections = "Total Collections";
        internal const string TotalShows = "Total TV Series";
        internal const string TotalEpisodes = "Total Episodes";
        internal const string TotalEpisodesWatched = "Total Episodes Watched";
        internal const string TotalShowsFinished = "Total Series Finished";
        internal const string MediaQualities = "Media Qualities";
        internal const string MediaCodecs = "Media Codecs";
        internal const string LongestMovie = "Longest Movie Runtime";
        internal const string LongestShow = "Longest TV Series Runtime";
        internal const string BiggestMovie = "Largest Movie";
        internal const string BiggestShow = "Largest TV Series Total Size";
        internal const string MostWatchedShows = "Most Watched Shows";
        internal const string OldesPremieredtMovie = "Oldest Premiered Movie";
        internal const string NewestPremieredMovie = "Newest Premiered Movie";
        internal const string OldestPremieredShow = "Oldest Premiered Show";
        internal const string NewestAddedMovie = "Newest Added Movie";
        internal const string NewestAddedEpisode = "Newest Added Episode";
        internal const string NewestPremieredShow = "Newest Premiered Show";
        internal const string HighestMovieRating = "Highest Movie Rating";
        internal const string LowestMovieRating = "Lowest Movie Rating";
        internal const string HighestBitrate = "Highest Movie Bitrate";
        internal const string LowestBitrate = "Lowest Movie Bitrate";
        internal const string TotalStudios = "Total Studios";
        internal const string TotalNetworks = "Total Networks";
        internal const string TotalUsers = "Total Users";

        //Help text for stats
        internal const string HelpMostActiveUsers = "Top 5 users that are the most active on the Emby server. This includes viewing movies and episodes.";
        internal const string HelpUserTotalMovies = "Total movies this user can see in his/her Emby library.";
        internal const string HelpUserTotalMoviesWatched = "Total movies this user has watched.";
        internal const string HelpUserTotalEpisodesWatched = "Total episodes this user has watched.";
        internal const string HelpUserMostWatchedShows = "Most watched shows based on episodes finished, not series completed.";
        internal const string HelpQualities = "Entries with Resolution Not Available can be located in the log file after debug logging has been enabled by searching CalculateMovieQualities.";
        internal const string HelpCodec = "Entries with Unknown can be located in the log file after debug logging has been enabled by searching CalculateMovieCodecs";
        internal const string HelpUserTotalShowsFinished = "Total shows this user has finished watching. Only normal episodes, so no specials are needed to be watched.";
        internal const string HelpUserTotalShows = "Total TV Series this user can see in his/her Emby library.";
        internal const string HelpUserTotalEpisode = "Total episodes this user can see in his/her Emby library.";
        internal const string HelpUserTotalCollections = "Total collections this user can see in his/her Emby library.";
        internal const string HelpUserToMovieYears = "Top 5 years the user watched movies.";
        internal const string HelpUserTopMovieGenres = "Top 5 movie genres the user watched.";
        internal const string HelpUserTopShowGenres = "Top 5 show genres the user watched.";
        internal const string NoData = "NO DATA FOUND!";
    }
}