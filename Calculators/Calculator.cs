using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using statistics.Calculators;
using statistics.Models;
using statistics.Models.Configuration;
using Statistics.Api;
using Statistics.Models;
using Statistics.ViewModel;

namespace Statistics.Helpers
{
    public class Calculator : BaseCalculator
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly List<MediaBrowser.Controller.Entities.Movies.Movie> _allMovies;
        private readonly List<Series> _allSeries;
        private readonly List<Episode> _allEpisodes;
        private readonly List<User> _allUsers;
        private readonly Dictionary<string, int> _tvdbEpisodeCounts; // Cache for tvdb episode counts
        private readonly IUserDataManager _userDataManager; // Store UserDataManager for reuse

        public Calculator(User user, IUserManager userManager, ILibraryManager libraryManager,
            IUserDataManager userDataManager, IFileSystem fileSystem, ILogger logger, UpdateModel tvdbData)
            : base(userManager, libraryManager, userDataManager)
        {
            User = user;
            _fileSystem = fileSystem;
            _logger = logger;
            _userDataManager = userDataManager; // Initialize here

            // Fetch all media items once in constructor
            _allMovies = GetAllMovies().ToList();
            _allSeries = GetAllSeries().ToList();
            _allEpisodes = GetAllOwnedEpisodes().ToList();
            _allUsers = GetAllUser().ToList();

            // Pre-process tvdbData for faster lookup in CalculateTotalFinishedShows
            _tvdbEpisodeCounts = tvdbData?.IdList.ToDictionary(x => x.ShowId, x => x.Count) ?? new Dictionary<string, int>(); // Null check for tvdbData
        }

        #region TopYears

        public ValueGroup CalculateFavoriteYears()
        {
            // Optimization: Directly filter GetAllMovies instead of materializing to movieList first
            var source = (User == null
                    ? GetAllMovies().Where(m => GetAllUser().Any(u => _userDataManager.GetUserData(u, m).Played))
                    : GetAllMovies().Where(m => _userDataManager.GetUserData(User, m).Played))
                .GroupBy(m => m.ProductionYear ?? 0)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToDictionary(g => g.Key, g => g.Count());

            return new ValueGroup
            {
                Title = Constants.FavoriteYears,
                ValueLineOne = string.Join(", ", source.OrderByDescending(g => g.Value).Select(g => g.Key)), // Optimization: Removed ToList() here
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserToMovieYears : null,
                Size = "half"
            };
        }

        #endregion

        #region LastSeen

        private IOrderedEnumerable<T> OrderViewedItemsByLastPlayedDate<T>(IEnumerable<T> items) where T : BaseItem
        {
            return items.OrderByDescending(m =>
            {
                User userToUse = User;
                if (User == null)
                {
                    userToUse = _allUsers.FirstOrDefault(u => _userDataManager.GetUserData(u, m).Played && _userDataManager.GetUserData(u, m).LastPlayedDate.HasValue); // Optimization: Use pre-fetched _allUsers
                }
                return userToUse != null ? _userDataManager.GetUserData(userToUse, m).LastPlayedDate : null;
            });
        }

        public ValueGroup CalculateLastSeenShows()
        {
            // Optimization: Directly use pre-fetched episodes and avoid ToList() after Take(8)
            var viewedEpisodes = OrderViewedItemsByLastPlayedDate(GetAllViewedEpisodesByUser())
                .Take(8);

            var lastSeenList = viewedEpisodes
                .Select(item => new LastSeenModel
                {
                    Name = $"{item.Series?.Name} - S{item.Season?.IndexNumber:00}:E{item.IndexNumber:00} - {item.Name}", // Null-conditional operators for safety
                    Played = _userDataManager.GetUserData(User, item).LastPlayedDate?.DateTime ?? DateTime.MinValue,
                    UserName = null
                }.ToString()).ToList();

            return new ValueGroup
            {
                Title = Constants.LastSeenShows,
                ValueLineOne = string.Join("<br/>", lastSeenList),
                ValueLineTwo = "",
                ValueLineThree = null,
                Size = "large"
            };
        }

        public ValueGroup CalculateLastSeenMovies()
        {
            // Optimization: Directly use pre-fetched movies and avoid ToList() after Take(8)
            var viewedMovies = OrderViewedItemsByLastPlayedDate(GetAllViewedMoviesByUser())
                .Take(8);

            var lastSeenList = viewedMovies
                .Select(item => new LastSeenModel
                {
                    Name = item.Name,
                    Played = _userDataManager.GetUserData(User, item).LastPlayedDate?.DateTime ?? DateTime.MinValue,
                    UserName = null
                }.ToString()).ToList();

            return new ValueGroup
            {
                Title = Constants.LastSeenMovies,
                ValueLineOne = string.Join("<br/>", lastSeenList),
                ValueLineTwo = "",
                ValueLineThree = null,
                Size = "large"
            };
        }

        #endregion

        #region TopGenres

        private ValueGroup CalculateFavoriteGenres(IEnumerable<BaseItem> mediaItems, string title, string helpConstant)
        {
            // Optimization: Use ToLookup for potentially faster grouping in memory if genres are repeated a lot. In this case GroupBy is clear and likely fast enough.
            var result = mediaItems
                .Where(m => m.IsVisible(User))
                .SelectMany(m => m.Genres)
                .GroupBy(genre => genre)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .ToDictionary(g => g.Key, g => g.Count());

            return new ValueGroup
            {
                Title = title,
                ValueLineOne = string.Join(", ", result.Select(g => g.Key)), // Optimization: Removed ToList() here
                ExtraInformation = User != null ? helpConstant : null,
                ValueLineTwo = "",
                ValueLineThree = null,
                Size = "half"
            };
        }

        public ValueGroup CalculateFavoriteMovieGenres()
        {
            return CalculateFavoriteGenres(_allMovies, Constants.FavoriteMovieGenres, Constants.HelpUserTopMovieGenres); // Optimization: Use pre-fetched _allMovies
        }

        public ValueGroup CalculateFavoriteShowGenres()
        {
            return CalculateFavoriteGenres(_allSeries, Constants.favoriteShowGenres, Constants.HelpUserTopShowGenres); // Optimization: Use pre-fetched _allSeries
        }

        #endregion

        #region PlayedViewTime

        private ValueGroup CalculateTime(IEnumerable<BaseItem> items, bool onlyPlayed, string titleConstant)
        {
            // Optimization: Filter items directly, avoid unnecessary ToList()
            var filteredItems = User == null
                ? items.Where(m => _allUsers.Any(u => _userDataManager.GetUserData(u, m).Played) || !onlyPlayed) // Optimization: Use pre-fetched _allUsers
                : items.Where(m => (_userDataManager.GetUserData(User, m).Played || !onlyPlayed) && m.IsVisible(User));

            var runTime = new RunTime();
            foreach (var item in filteredItems)
            {
                runTime.Add(item.RunTimeTicks);
            }

            return new ValueGroup
            {
                Title = onlyPlayed ? Constants.TotalWatched : Constants.TotalWatchableTime,
                ValueLineOne = runTime.ToLongString(),
                ValueLineTwo = "",
                ValueLineThree = null,
                Size = "half"
            };
        }

        public ValueGroup CalculateMovieTime(bool onlyPlayed = true)
        {
            return CalculateTime(_allMovies, onlyPlayed, onlyPlayed ? Constants.TotalWatched : Constants.TotalWatchableTime); // Optimization: Use pre-fetched _allMovies
        }

        public ValueGroup CalculateShowTime(bool onlyPlayed = true)
        {
            return CalculateTime(_allEpisodes, onlyPlayed, onlyPlayed ? Constants.TotalWatched : Constants.TotalWatchableTime); // Optimization: Use pre-fetched _allEpisodes
        }

        public ValueGroup CalculateOverallTime(bool onlyPlayed = true)
        {
            // Optimization: Directly use Sum and pre-fetched lists where possible
            var totalTicks = (User == null
                    ? GetAllBaseItems().Where(m => _allUsers.Any(u => _userDataManager.GetUserData(u, m).Played) || !onlyPlayed) // Optimization: Use pre-fetched _allUsers
                    : GetAllBaseItems().Where(m => (_userDataManager.GetUserData(User, m).Played || !onlyPlayed) && m.IsVisible(User)))
                .Sum(item => item.RunTimeTicks ?? 0);

            var runTime = new RunTime();
            runTime.Add(totalTicks);

            return new ValueGroup
            {
                Title = onlyPlayed ? Constants.TotalWatched : Constants.TotalWatchableTime,
                ValueLineOne = runTime.ToLongString(),
                ValueLineTwo = "",
                ValueLineThree = null,
                Raw = runTime.Ticks,
                Size = "half"
            };
        }

        #endregion

        #region TotalMedia

        private ValueGroup CalculateTotalMediaCount<T>(string title, string helpConstant = null, string lineTwoTitle = null, Func<int> countAction = null) where T : BaseItem
        {
            int count = countAction != null ? countAction() : GetOwnedCount(typeof(T));
            return new ValueGroup
            {
                Title = title,
                ValueLineOne = $"{count}",
                ValueLineTwo = lineTwoTitle,
                ValueLineThree = lineTwoTitle != null ? $"{GetOwnedCount(typeof(Episode))}" : null, // Hardcoded Episode for TotalShows
                ExtraInformation = User != null ? helpConstant : null
            };
        }

        public ValueGroup CalculateTotalMovies()
        {
            return CalculateTotalMediaCount<MediaBrowser.Controller.Entities.Movies.Movie>(Constants.TotalMovies, Constants.HelpUserTotalMovies);
        }

        public ValueGroup CalculateTotalShows()
        {
            return CalculateTotalMediaCount<Series>(Constants.TotalShows, Constants.HelpUserTotalShows, Constants.TotalEpisodes);
        }

        public ValueGroup CalculateTotalOwnedEpisodes()
        {
            return CalculateTotalMediaCount<Episode>(Constants.TotalEpisodes, Constants.HelpUserTotalEpisode);
        }

        public ValueGroup CalculateTotalBoxsets()
        {
            return CalculateTotalMediaCount<BoxSet>(Constants.TotalCollections, Constants.HelpUserTotalCollections, countAction: () => GetBoxsets().Count());
        }

        private ValueGroup CalculateTotalMediaWatched<T>(string title, string helpConstant, Func<decimal, decimal> percentageCalculation) where T : BaseItem
        {
            // Optimization: Avoid conditional type check within the method by specializing calls for movies and episodes.
            int viewedMediaCount = 0;
            if (typeof(T) == typeof(MediaBrowser.Controller.Entities.Movies.Movie))
            {
                viewedMediaCount = GetAllViewedMoviesByUser().Count();
            }
            else if (typeof(T) == typeof(Episode)) // Assuming episodes are the only other type counted this way. If not, consider more robust type handling.
            {
                viewedMediaCount = _allSeries.Sum(GetPlayedEpisodeCount); // Optimization: use pre-fetched series
            }

            var totalMediaCount = GetOwnedCount(typeof(T));

            decimal percentage = decimal.Zero;
            if (totalMediaCount > 0)
                percentage = percentageCalculation(totalMediaCount);


            return new ValueGroup
            {
                Title = title,
                ValueLineOne = $"{viewedMediaCount} ({percentage}%)",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? helpConstant : null
            };
        }


        public ValueGroup CalculateTotalMoviesWatched()
        {
            return CalculateTotalMediaWatched<MediaBrowser.Controller.Entities.Movies.Movie>(Constants.TotalMoviesWatched, Constants.HelpUserTotalMoviesWatched, totalMoviesCount => Math.Round(GetAllViewedMoviesByUser().Count() / (decimal)totalMoviesCount * 100m, 1)); //Explicit cast to decimal
        }

        public ValueGroup CalculateTotalEpiosodesWatched()
        {
            return CalculateTotalMediaWatched<Episode>(Constants.TotalEpisodesWatched, Constants.HelpUserTotalEpisodesWatched, totalEpisodesCount => Math.Round(_allSeries.Sum(GetPlayedEpisodeCount) / (decimal)totalEpisodesCount * 100m, 1)); // Optimization: use pre-fetched series and explicit cast
        }

        public ValueGroup CalculateTotalFinishedShows()
        {
            int count = 0;

            foreach (var show in _allSeries) // Optimization: Iterate pre-fetched series
            {
                if (_tvdbEpisodeCounts.TryGetValue(show.GetProviderId(MetadataProviders.Tvdb), out var totalEpisodesFromTvdb))
                {
                    var totalEpisodes = totalEpisodesFromTvdb;
                    var seenEpisodes = GetPlayedEpisodeCount(show);

                    if (seenEpisodes > totalEpisodes) //Corrected logic, seenEpisodes can be higher if TVDB data is outdated
                        totalEpisodes = seenEpisodes;

                    if (totalEpisodes > 0 && totalEpisodes == seenEpisodes)
                        count++;
                }
            }

            return new ValueGroup
            {
                Title = Constants.TotalShowsFinished,
                ValueLineOne = $"{count}",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserTotalShowsFinished : null
            };
        }

        public ValueGroup CalculateTotalMovieStudios()
        {
            // Optimization: Use HashSet constructor with SelectMany and Studios to directly create the set.
            var studioSet = new HashSet<string>(_allMovies // Optimization: Use pre-fetched movies
                .Where(x => x.Studios != null && x.Studios.Any())
                .SelectMany(movie => movie.Studios));

            return new ValueGroup
            {
                Title = Constants.TotalStudios,
                ValueLineOne = $"{studioSet.Count}",
                ValueLineTwo = "",
                ValueLineThree = null,
            };
        }

        public ValueGroup CalculateTotalShowStudios()
        {
            // Optimization: Use HashSet constructor with SelectMany and Studios to directly create the set.
            var networkSet = new HashSet<string>(_allSeries // Optimization: Use pre-fetched series
                .Where(x => x.Studios != null && x.Studios.Any())
                .SelectMany(series => series.Studios));

            return new ValueGroup
            {
                Title = Constants.TotalNetworks,
                ValueLineOne = $"{networkSet.Count}",
                ValueLineTwo = "",
                ValueLineThree = null,
            };
        }

        public ValueGroup CalculateTotalUsers()
        {
            return new ValueGroup
            {
                Title = Constants.TotalUsers,
                ValueLineOne = $"{_allUsers.Count}", // Optimization: Use pre-fetched count
                ValueLineTwo = "",
                ValueLineThree = null,
            };
        }

        #endregion

        #region MostActiveUsers

        public ValueGroup CalculateMostActiveUsers(Dictionary<string, RunTime> users)
        {
            var mostActiveUsers = users.OrderByDescending(x => x.Value).Take(6);

            // Optimization: Use string interpolation directly in Select and join once outside the loop
            var tempList = mostActiveUsers.Select(x => $"<tr><td>{x.Key}</td>{x.Value}</tr>");
            var tableRows = string.Join("", tempList);

            return new ValueGroup
            {
                Title = Constants.MostActiveUsers,
                ValueLineOne = $"<table><tr><td></td><td>Days</td><td>Hours</td><td>Minutes</td></tr>{tableRows}</table>",
                ValueLineTwo = "",
                ValueLineThree = null,
                Size = "half",
                ExtraInformation = Constants.HelpMostActiveUsers
            };
        }

        #endregion

        #region Quality

        public ValueGroup CalculateMovieQualities()
        {
            var qualityCounts = new Dictionary<string, VideoQualityModel>();

            // Optimization: Iterate pre-fetched movies
            foreach (var movie in _allMovies.Where(w => w.Name != null).OrderBy(x => x.Name))
            {
                try
                {
                    var quality = GetMediaResolution(movie.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video));
                    if (!qualityCounts.TryGetValue(quality.Trim(), out var qualityModel))
                    {
                        qualityModel = new VideoQualityModel { Quality = quality.Trim(), Movies = 0, Episodes = 0 };
                        qualityCounts[quality.Trim()] = qualityModel;
                    }
                    qualityCounts[quality.Trim()].Movies++; // Use direct access
                    _logger.Debug($"CalculateMovieQualities {movie.Name} {quality}"); // String interpolation
                }
                catch (Exception ex)
                {
                    _logger.Debug($"CalculateMovieQualities-Error {movie.Name}: {ex.Message}"); // String interpolation
                }
            }

            // Optimization: Iterate pre-fetched episodes
            foreach (var episode in _allEpisodes.Where(w => w.Name != null).OrderBy(x => x.Name))
            {
                try
                {
                    var quality = GetMediaResolution(episode.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video));
                    if (!qualityCounts.TryGetValue(quality.Trim(), out var qualityModel))
                    {
                        qualityModel = new VideoQualityModel { Quality = quality.Trim(), Movies = 0, Episodes = 0 };
                        qualityCounts[quality.Trim()] = qualityModel;
                    }
                    qualityCounts[quality.Trim()].Episodes++; // Use direct access
                    _logger.Debug($"CalculateMovieCodecs-episode {(episode.Series?.Name ?? "invalid name")}: {episode.SortName} {quality}"); // String interpolation and null-conditional operator
                }
                catch (Exception ex)
                {
                    _logger.Debug($"CalculateMovieQualities-episode-Error {episode.Name}: {ex.Message}"); // String interpolation
                }
            }

            return new ValueGroup
            {
                Title = Constants.MediaQualities,
                ValueLineOne = $"<table><tr><td></td><td>Movies</td><td>Episodes</td></tr>{string.Join("", qualityCounts.Values)}</table>",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = Constants.HelpQualities,
                Size = "half"
            };
        }

        string GetMediaResolution(MediaStream typeInfo)
        {
            if (typeInfo == null || typeInfo.Width == null)
                return "Resolution Not Available";

            int width = typeInfo.Width.Value; // Directly access value after null check

            if (width >= 1281 && width <= 1920) return "1080p";
            if (width >= 3841 && width <= 7680) return "8K";
            if (width >= 1921 && width <= 3840) return "4K";
            if (width >= 1200 && width <= 1280) return "720p";
            if (width < 1200) return "SD";

            return "Resolution Not Available";
        }


        public ValueGroup CalculateMovieCodecs()
        {
            var codecCounts = new Dictionary<string, VideoCodecModel>();

            // Optimization: Iterate pre-fetched movies
            foreach (var movie in _allMovies.Where(w => w.SortName != null).OrderBy(x => x.SortName))
            {
                try
                {
                    var codec = movie.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video)?.Codec ?? "Unknown";
                    if (!codecCounts.TryGetValue(codec, out var codecModel))
                    {
                        codecModel = new VideoCodecModel { Codec = codec, Movies = 0, Episodes = 0 };
                        codecCounts[codec] = codecModel;
                    }
                    codecCounts[codec].Movies++; // Use direct access

                    _logger.Debug($"CalculateMovieCodecs {movie.SortName} {codec}"); // String interpolation
                }
                catch (Exception ex)
                {
                    _logger.Debug($"CalculateMovieCodecs-Error {movie.SortName}: {ex.Message}"); // String interpolation
                }
            }

            // Optimization: Iterate pre-fetched episodes
            foreach (var episode in _allEpisodes.Where(w => w.SortName != null).OrderBy(x => x.SortName))
            {
                try
                {
                    var codec = episode.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video)?.Codec ?? "Unknown";
                    if (!codecCounts.TryGetValue(codec, out var codecModel))
                    {
                        codecModel = new VideoCodecModel { Codec = codec, Movies = 0, Episodes = 0 };
                        codecCounts[codec] = codecModel;
                    }
                    codecCounts[codec].Episodes++; // Use direct access
                    _logger.Debug($"CalculateMovieCodecs-episode {(episode.Series?.SortName ?? "invalid name")}: {episode.SortName} {codec}"); // String interpolation and null-conditional operator
                }
                catch (Exception ex)
                {
                    _logger.Debug($"CalculateMovieCodecs-episode-Error {episode.SortName}: {ex.Message}"); // String interpolation
                }
            }

            return new ValueGroup
            {
                Title = Constants.MediaCodecs,
                ValueLineOne = $"<table><tr><td></td><td>Movies</td><td>Episodes</td></tr>{string.Join("", codecCounts.Values)}</table>",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = Constants.HelpCodec,
                Size = "half"
            };
        }

        public MovieQualityObj CalculateMovieQualityList()
        {
            var qualityMovieMap = new Dictionary<string, List<statistics.Models.Movie>>();

            // Optimization: Iterate pre-fetched movies
            foreach (var movie in _allMovies.Where(w => w.SortName != null).OrderBy(x => x.SortName))
            {
                _logger.Debug($"CalculateMovieQualityList {movie.Name}"); // String interpolation
                var quality = movie.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video)?.DisplayTitle?.Split(' ')[0]; //Null check for DisplayTitle

                if (!qualityMovieMap.TryGetValue(quality, out var movieList))
                {
                    movieList = new List<statistics.Models.Movie>();
                    qualityMovieMap[quality] = movieList;
                }
                movieList.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                _logger.Debug($"{quality} {qualityMovieMap.Count}"); // String interpolation - debug output is not directly helpful
            }

            var list = qualityMovieMap.Select(pair => new MovieQuality
            {
                Title = pair.Key,
                Movies = pair.Value
            }).ToList();


            return new MovieQualityObj()
            {
                Count = list.Count(),
                Movies = list
            };
        }
        #endregion

        #region Size

        public ValueGroup CalculateBiggestMovie()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";

            MediaBrowser.Controller.Entities.Movies.Movie biggestMovie = null;
            double maxSize = 0;

            // Optimization: Iterate pre-fetched movies
            foreach (var movie in _allMovies)
            {
                try
                {
                    var f = _fileSystem.GetFileSystemInfo(movie.Path);
                    if (f.Length > maxSize)
                    {
                        maxSize = f.Length;
                        biggestMovie = movie;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug($"CalculateBiggestMovie-Error: {ex.Message}"); // String interpolation and more informative log. Include exception for context.
                }
            }

            if (biggestMovie != null)
            {
                maxSize /= 1073741824; //Byte to Gb
                valueLineOne = CheckMaxLength($"{maxSize:F1} Gb");
                valueLineTwo = CheckMaxLength($"{biggestMovie.Name}");

                return new ValueGroup
                {
                    Title = Constants.BiggestMovie,
                    ValueLineOne = valueLineOne,
                    ValueLineTwo = valueLineTwo,
                    ValueLineThree = null,
                    Size = "half",
                    Id = biggestMovie.Id.ToString()
                };
            }
            else
            {
                return new ValueGroup
                {
                    Title = Constants.BiggestMovie,
                    ValueLineOne = Constants.NoData,
                    ValueLineTwo = "",
                    ValueLineThree = null,
                    Size = "half",
                    Id = null
                };
            }
        }


        public ValueGroup CalculateBiggestShow()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            Series biggestShow = null;
            double maxSize = 0;

            if (_allSeries.Any()) // Optimization: Check pre-fetched list
            {
                // Optimization: Calculate showSize directly within the _allSeries loop to avoid redundant episode filtering per show.
                foreach (var show in _allSeries)
                {
                    double showSize = 0;
                    //This is assuming the recommened folder structure for series/season/episode
                    //https://github.com/MediaBrowser/Emby/wiki/TV-Library
                    // Optimization: Iterate pre-fetched episodes and filter within loop.
                    foreach (var episode in _allEpisodes.Where(x => x.GetParent().GetParent().Id == show.Id && x.Path != null))
                    {
                        try
                        {
                            var f = _fileSystem.GetFileSystemInfo(episode.Path);
                            showSize += f.Length;
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"CalculateBiggestShow-Error getting file info for episode {episode.Name} in show {show.Name}: {e.Message}", e); //Include show name in log
                        }
                    }


                    if (showSize > maxSize)
                    {
                        maxSize = showSize;
                        biggestShow = show;
                    }
                }

                if (biggestShow != null)
                {
                    maxSize /= 1073741824; //Byte to Gb
                    valueLineOne = CheckMaxLength($"{maxSize:F1} Gb");
                    valueLineTwo = CheckMaxLength($"{biggestShow.Name}");
                    id = biggestShow.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.BiggestShow,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }


        public ValueGroup CalculateMostWatchedShows()
        {
            var showList = _allSeries.OrderBy(x => x.SortName); // Optimization: Use pre-fetched series
            var users = _allUsers; // Optimization: Use pre-fetched users
            var showProgress = new List<ShowProgress>();

            foreach (var user in users)
            {
                SetUser(user);
                foreach (var show in showList)
                {
                    if (_tvdbEpisodeCounts.TryGetValue(show.GetProviderId(MetadataProviders.Tvdb), out var totalEpisodesFromTvdb))
                    {
                        var totalEpisodes = totalEpisodesFromTvdb;

                        var collectedEpisodes = GetOwnedEpisodesCount(show);
                        var seenEpisodes = GetPlayedEpisodeCount(show);

                        if (collectedEpisodes > totalEpisodes)
                        {
                            totalEpisodes = collectedEpisodes;
                        }

                        decimal watched = 0;
                        decimal collected = 0;
                        if (totalEpisodes > 0)
                        {
                            collected = collectedEpisodes / (decimal)totalEpisodes * 100;
                        }

                        if (collectedEpisodes > 0)
                        {
                            watched = seenEpisodes / (decimal)collectedEpisodes * 100;
                        }

                        ShowProgress existingShowProgress = showProgress.FirstOrDefault(x => x.Name == show.Name);
                        if (existingShowProgress != null)
                        {
                            existingShowProgress.Watched += Math.Round(watched, 1);
                        }
                        else
                        {
                            showProgress.Add(new ShowProgress
                            {
                                Name = show.Name,
                                SortName = show.SortName,
                                Score = show.CommunityRating,
                                Status = show.Status,
                                StartYear = show.PremiereDate?.ToString("yyyy"),
                                Watched = Math.Round(watched, 1),
                                Episodes = collectedEpisodes,
                                SeenEpisodes = seenEpisodes,
                                Specials = GetOwnedSpecials(show), //No need to recalculate each time, use GetOwnedSpecials and GetPlayedSpecials directly
                                SeenSpecials = GetPlayedSpecials(show),
                                Collected = Math.Round(collected, 1),
                                Total = totalEpisodes
                            });
                        }
                    }
                }
            }


            foreach (var show in showProgress)
            {
                show.Watched = Math.Round(show.Watched / users.Count(), 1);
            }

            var sortedList = showProgress.OrderByDescending(o => o.Watched).ToList();

            foreach (var show in sortedList)
            {
                _logger.Debug($"CalculateMostWatchedShows {show.Name} {show.Watched}"); // String interpolation
            }

            string lineone = "", linetwo = "", linethree = "";

            if (sortedList.Count >= 1) lineone = sortedList[0].Name;
            if (sortedList.Count >= 2) linetwo = sortedList[1].Name;
            if (sortedList.Count >= 3) linethree = sortedList[2].Name;

            return new ValueGroup
            {
                Title = Constants.MostWatchedShows,
                ValueLineOne = lineone,
                ValueLineTwo = linetwo,
                ValueLineThree = linethree,
                ExtraInformation = Constants.HelpUserMostWatchedShows
            };
        }

        public ValueGroup CalculateLeastWatchedShows()
        {
            var showList = _allSeries.OrderBy(x => x.SortName); // Optimization: Use pre-fetched series
            var users = _allUsers; // Optimization: Use pre-fetched users
            var showProgress = new List<ShowProgress>();

            foreach (var user in users)
            {
                SetUser(user);
                foreach (var show in showList)
                {
                    if (_tvdbEpisodeCounts.TryGetValue(show.GetProviderId(MetadataProviders.Tvdb), out var totalEpisodesFromTvdb))
                    {
                        var totalEpisodes = totalEpisodesFromTvdb;

                        var collectedEpisodes = GetOwnedEpisodesCount(show);
                        var seenEpisodes = GetPlayedEpisodeCount(show);

                        if (collectedEpisodes > totalEpisodes)
                        {
                            totalEpisodes = collectedEpisodes;
                        }

                        decimal watched = 0;
                        decimal collected = 0;
                        if (totalEpisodes > 0)
                        {
                            collected = collectedEpisodes / (decimal)totalEpisodes * 100;
                        }

                        if (collectedEpisodes > 0)
                        {
                            watched = seenEpisodes / (decimal)collectedEpisodes * 100;
                        }
                        ShowProgress existingShowProgress = showProgress.FirstOrDefault(x => x.Name == show.Name);

                        if (existingShowProgress != null)
                        {
                            existingShowProgress.Watched += Math.Round(watched, 1);
                        }
                        else
                        {
                            showProgress.Add(new ShowProgress
                            {
                                Name = show.Name,
                                SortName = show.SortName,
                                Score = show.CommunityRating,
                                Status = show.Status,
                                StartYear = show.PremiereDate?.ToString("yyyy"),
                                Watched = Math.Round(watched, 1),
                                Episodes = collectedEpisodes,
                                SeenEpisodes = seenEpisodes,
                                Specials = GetOwnedSpecials(show), //No need to recalculate each time, use GetOwnedSpecials and GetPlayedSpecials directly
                                SeenSpecials = GetPlayedSpecials(show),
                                Collected = Math.Round(collected, 1),
                                Total = totalEpisodes
                            });
                        }
                    }
                }
            }


            foreach (var show in showProgress)
            {
                show.Watched = Math.Round(show.Watched / users.Count(), 1);
            }

            var sortedList = showProgress.OrderBy(o => o.Watched).ToList();

            foreach (var show in sortedList)
            {
                _logger.Debug($"CalculateLeastWatchedShows {show.Name} {show.Watched}"); // String interpolation
            }

            string lineone = "", linetwo = "", linethree = "";

            if (sortedList.Count >= 1) lineone = sortedList[0].Name;
            if (sortedList.Count >= 2) linetwo = sortedList[1].Name;
            if (sortedList.Count >= 3) linethree = sortedList[2].Name;


            return new ValueGroup
            {
                Title = Constants.LeastWatchedShows,
                ValueLineOne = lineone,
                ValueLineTwo = linetwo,
                ValueLineThree = linethree,
                ExtraInformation = Constants.HelpUserLeastWatchedShows
            };
        }

        public ValueGroup CalculateHighestBitrateMovie()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            if (_allMovies.Any()) // Optimization: Check pre-fetched list
            {
                // Optimization: Use LINQ MaxBy (if available in target framework, otherwise custom MaxBy implementation) or OrderByDescending.First()
                var largest = _allMovies.OrderByDescending(x => x.TotalBitrate).FirstOrDefault();

                if (largest != null)
                {
                    var bitrate = Math.Round((decimal)largest.TotalBitrate / 1000);
                    valueLineOne = CheckMaxLength($"{bitrate} Kbps");
                    valueLineTwo = CheckMaxLength($"{largest.Name}");
                    id = largest.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.HighestBitrate,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        public ValueGroup CalculateLowestBitrateMovie()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            if (_allMovies.Any()) // Optimization: Check pre-fetched list
            {
                // Optimization: Use LINQ MinBy (if available in target framework, otherwise custom MinBy implementation) or OrderBy.First with filter.
                var lowest = _allMovies.Where(x => x.TotalBitrate > 0).OrderBy(x => x.TotalBitrate).FirstOrDefault();


                if (lowest != null)
                {
                    var bitrate = Math.Round((decimal)lowest.TotalBitrate / 1000);
                    valueLineOne = CheckMaxLength($"{bitrate} Kbps");
                    valueLineTwo = CheckMaxLength($"{lowest.Name}");
                    id = lowest.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.LowestBitrate,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        #endregion

        #region Period

        public ValueGroup CalculateLongestMovie()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            // Optimization: Use LINQ MaxBy (if available) or OrderByDescending.First() with filter.
            var maxMovie = _allMovies.Where(x => x.RunTimeTicks.HasValue).OrderByDescending(x => x.RunTimeTicks).FirstOrDefault(); // Optimization: Use pre-fetched movies
            if (maxMovie != null)
            {
                valueLineOne = CheckMaxLength(new TimeSpan(maxMovie.RunTimeTicks.Value).ToString(@"hh\:mm\:ss"));
                valueLineTwo = CheckMaxLength($"{maxMovie.Name}");
                id = maxMovie.Id.ToString();
            }
            return new ValueGroup
            {
                Title = Constants.LongestMovie,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        public ValueGroup CalculateLongestShow()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            if (_allSeries.Any()) // Optimization: Check pre-fetched list
            {
                Series maxShow = null;
                long maxTime = 0;

                // Optimization: Calculate showTime directly within the _allSeries loop, same as CalculateBiggestShow.
                foreach (var show in _allSeries)
                {
                    long showTime = 0;
                    //This is assuming the recommened folder structure for series/season/episode
                    //https://github.com/MediaBrowser/Emby/wiki/TV-Library
                    // Optimization: Iterate pre-fetched episodes and filter within loop.
                    foreach (var episode in _allEpisodes.Where(x => x.GetParent().GetParent().Id == show.Id && x.Path != null))
                    {
                        showTime += episode.RunTimeTicks ?? 0;
                    }

                    if (showTime > maxTime)
                    {
                        maxTime = showTime;
                        maxShow = show;
                    }
                }

                if (maxShow != null)
                {
                    var time = new TimeSpan(maxTime).ToString(@"hh\:mm\:ss");
                    var days = CheckForPlural("day", new TimeSpan(maxTime).Days, "", "and");

                    valueLineOne = CheckMaxLength($"{days} {time}");
                    valueLineTwo = CheckMaxLength($"{maxShow.Name}");
                    id = maxShow.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.LongestShow,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        #endregion

        #region Release Date

        public ValueGroup CalculateOldestMovie()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            if (_allMovies.Any()) // Optimization: Check pre-fetched list
            {
                // Optimization: Use LINQ MinBy (if available) or OrderBy.First() with filter.
                var oldest = _allMovies
                    .Where(x => x.PremiereDate.HasValue && x.PremiereDate.Value.DateTime > DateTime.MinValue)
                    .OrderBy(x => x.PremiereDate?.DateTime)
                    .FirstOrDefault();


                if (oldest != null && oldest.PremiereDate.HasValue)
                {
                    var oldestDate = oldest.PremiereDate.Value.DateTime;
                    var numberOfTotalMonths = (DateTime.Now.Year - oldestDate.Year) * 12 + DateTime.Now.Month - oldestDate.Month;
                    var numberOfYears = Math.Floor(numberOfTotalMonths / (decimal)12);
                    var numberOfMonth = Math.Floor((numberOfTotalMonths / (decimal)12 - numberOfYears) * 12);

                    valueLineOne = CheckMaxLength($"{CheckForPlural("year", numberOfYears, "", "", false)} {CheckForPlural("month", numberOfMonth, "and")} ago");
                    valueLineTwo = CheckMaxLength($"{oldest.Name}");
                    id = oldest.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.OldesPremieredtMovie,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        public ValueGroup CalculateNewestMovie()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            if (_allMovies.Any()) // Optimization: Check pre-fetched list
            {
                // Optimization: Use LINQ MaxBy (if available) or OrderByDescending.First() with filter.
                var youngest = _allMovies
                    .Where(x => x.PremiereDate.HasValue)
                    .OrderByDescending(x => x.PremiereDate?.DateTime)
                    .FirstOrDefault();


                if (youngest != null)
                {
                    var numberOfTotalDays = DateTime.Now.Date - youngest.PremiereDate.Value.DateTime;
                    valueLineOne = CheckMaxLength(numberOfTotalDays.Days == 0
                            ? $"Today"
                            : $"{CheckForPlural("day", numberOfTotalDays.Days, "", "", false)} ago");

                    valueLineTwo = CheckMaxLength($"{youngest.Name}");
                    id = youngest.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.NewestPremieredMovie,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        public ValueGroup CalculateNewestAddedMovie()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            if (_allMovies.Any()) // Optimization: Check pre-fetched list
            {
                // Optimization: Use LINQ MaxBy (if available) or OrderByDescending.First() with filter.
                var youngest = _allMovies
                    .Where(x => x.DateCreated.DateTime != DateTime.MinValue)
                    .OrderByDescending(x => x.DateCreated.DateTime)
                    .FirstOrDefault();


                if (youngest != null)
                {
                    var numberOfTotalDays = DateTime.Now - youngest.DateCreated.DateTime;

                    valueLineOne =
                        CheckMaxLength(numberOfTotalDays.Days == 0
                            ? $"Today"
                            : $"{CheckForPlural("day", numberOfTotalDays.Days, "", "", false)} ago");

                    valueLineTwo = CheckMaxLength($"{youngest.Name}");
                    id = youngest.Id.ToString();
                }
            }


            return new ValueGroup
            {
                Title = Constants.NewestAddedMovie,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        public ValueGroup CalculateNewestAddedEpisode()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            if (_allEpisodes.Any()) // Optimization: Check pre-fetched list
            {
                // Optimization: Use LINQ MaxBy (if available) or OrderByDescending.First() with filter.
                var youngest = _allEpisodes
                    .Where(x => x.DateCreated.DateTime != DateTime.MinValue)
                    .OrderByDescending(x => x.DateCreated.DateTime)
                    .FirstOrDefault();

                if (youngest != null)
                {
                    var numberOfTotalDays = DateTime.Now.Date - youngest.DateCreated.DateTime;

                    valueLineOne =
                        CheckMaxLength(numberOfTotalDays.Days == 0
                            ? "Today"
                            : $"{CheckForPlural("day", numberOfTotalDays.Days, "", "", false)} ago");

                    valueLineTwo = CheckMaxLength($"{youngest.Series?.Name} S{youngest.Season?.IndexNumber} E{youngest.IndexNumber} "); //Null conditional operators
                    id = youngest.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.NewestAddedEpisode,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        public ValueGroup CalculateOldestShow()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            if (_allSeries.Any()) // Optimization: Check pre-fetched list
            {
                // Optimization: Use LINQ MinBy (if available) or OrderBy.First() with filter.
                var oldest = _allSeries
                    .Where(x => x.PremiereDate.HasValue && x.PremiereDate.Value.DateTime > DateTime.MinValue)
                    .OrderBy(x => x.PremiereDate?.DateTime)
                    .FirstOrDefault();


                if (oldest != null && oldest.PremiereDate.HasValue)
                {
                    var oldestDate = oldest.PremiereDate.Value.DateTime;
                    var numberOfTotalMonths = (DateTime.Now.Year - oldestDate.Year) * 12 + DateTime.Now.Month - oldestDate.Month;
                    var numberOfYears = Math.Floor(numberOfTotalMonths / (decimal)12);
                    var numberOfMonth = Math.Floor((numberOfTotalMonths / (decimal)12 - numberOfYears) * 12);

                    valueLineOne = CheckMaxLength($"{CheckForPlural("year", numberOfYears, "", "", false)} {CheckForPlural("month", numberOfMonth, "and")} ago");
                    valueLineTwo = CheckMaxLength($"{oldest.Name}");
                    id = oldest.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.OldestPremieredShow,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        public ValueGroup CalculateNewestShow()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            if (_allSeries.Any()) // Optimization: Check pre-fetched list
            {
                // Optimization: Use LINQ MaxBy (if available) or OrderByDescending.First() with filter.
                var youngest = _allSeries
                    .Where(x => x.PremiereDate.HasValue)
                    .OrderByDescending(x => x.PremiereDate?.DateTime)
                    .FirstOrDefault();


                if (youngest != null)
                {
                    var numberOfTotalDays = DateTime.Now.Date - youngest.PremiereDate.Value.DateTime;
                    valueLineOne = CheckMaxLength(numberOfTotalDays.Days == 0
                            ? $"Today"
                            : $"{CheckForPlural("day", numberOfTotalDays.Days, "", "", false)} ago");

                    valueLineTwo = CheckMaxLength($"{youngest.Name}");
                    id = youngest.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.NewestPremieredShow,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        #endregion

        #region Ratings

        public ValueGroup CalculateHighestRating()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            // Optimization: Use LINQ MaxBy (if available) or OrderByDescending.First() with filter.
            var highestRatedMovie = _allMovies
                .Where(x => x.CommunityRating.HasValue)
                .OrderByDescending(x => x.CommunityRating)
                .FirstOrDefault(); // Optimization: Use pre-fetched movies


            if (highestRatedMovie != null)
            {
                valueLineOne = CheckMaxLength($"{highestRatedMovie.CommunityRating} / 10");
                valueLineTwo = CheckMaxLength($"{highestRatedMovie.Name}");
                id = highestRatedMovie.Id.ToString();
            }

            return new ValueGroup
            {
                Title = Constants.HighestMovieRating,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        public ValueGroup CalculateLowestRating()
        {
            string valueLineOne = Constants.NoData;
            string valueLineTwo = "";
            string id = null;

            // Optimization: Use LINQ MinBy (if available) or OrderBy.First() with filter.
            var lowestRatedMovie = _allMovies
                .Where(x => x.CommunityRating.HasValue && x.CommunityRating != 0)
                .OrderBy(x => x.CommunityRating)
                .FirstOrDefault(); // Optimization: Use pre-fetched movies


            if (lowestRatedMovie != null)
            {
                valueLineOne = CheckMaxLength($"{lowestRatedMovie.CommunityRating} / 10");
                valueLineTwo = CheckMaxLength($"{lowestRatedMovie.Name}");
                id = lowestRatedMovie.Id.ToString();
            }

            return new ValueGroup
            {
                Title = Constants.LowestMovieRating,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id
            };
        }

        #endregion

        private string CheckMaxLength(string value)
        {
            return value.Length > 30 ? value.Substring(0, 27) + "..." : value;
        }

        private string CheckForPlural(string value, decimal number, string starting = "", string ending = "", bool removeZero = true)
        {
            if (number == 1)
                return $" {starting} {number} {value} {ending}";
            if (number == 0 && removeZero)
                return "";
            return $" {starting} {number} {value}s {ending}";
        }
    }
}