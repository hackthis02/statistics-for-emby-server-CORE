using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using statistics.Calculators;
using statistics.Models;
using statistics.Models.Configuration;
using Statistics.Models;
using Statistics.ViewModel;

namespace Statistics.Helpers
{
    public class Calculator : BaseCalculator
    {
        private readonly IFileSystem _fileSystem;
        private readonly List<MediaBrowser.Controller.Entities.Movies.Movie> _allMovies;
        private readonly List<Series> _allSeries;
        private readonly List<Episode> _allEpisodes;
        private readonly List<User> _allUsers;
        private readonly IUserDataManager _userDataManager;

        public Calculator(IUserManager userManager, ILibraryManager libraryManager,
            IUserDataManager userDataManager, IFileSystem fileSystem, ILogger logger, 
            IProviderManager providerManager, CancellationToken cancellationToken)
            : base(userManager, libraryManager, userDataManager, providerManager, logger, cancellationToken)
        {
            _fileSystem = fileSystem;
            _userDataManager = userDataManager;

            _allMovies = GetAllMovies().ToList();
            _allSeries = GetAllSeries().ToList();
            _allEpisodes = GetAllOwnedEpisodes().ToList();
            _allUsers = GetAllUser().ToList();           
        }

        #region TopYears

        public ValueGroup CalculateFavoriteYears()
        {
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
                ValueLineOne = string.Join(", ", source.OrderByDescending(g => g.Value).Select(g => g.Key)),
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
                    userToUse = _allUsers.FirstOrDefault(u => _userDataManager.GetUserData(u, m).Played && _userDataManager.GetUserData(u, m).LastPlayedDate.HasValue); 
                }
                return userToUse != null ? _userDataManager.GetUserData(userToUse, m).LastPlayedDate : null;
            });
        }

        public ValueGroup CalculateLastSeenShows()
        {
            var viewedEpisodes = OrderViewedItemsByLastPlayedDate(GetAllViewedEpisodesByUser())
                .Take(8);

            var lastSeenList = viewedEpisodes
                .Select(item => new LastSeenModel
                {
                    Name = $"{item.Series?.Name} - S{item.Season?.IndexNumber:00}:E{item.IndexNumber:00} - {item.Name}",
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
                ValueLineOne = string.Join(", ", result.Select(g => g.Key)),
                ExtraInformation = User != null ? helpConstant : null,
                ValueLineTwo = "",
                ValueLineThree = null,
                Size = "half"
            };
        }

        public ValueGroup CalculateFavoriteMovieGenres()
        {
            return CalculateFavoriteGenres(_allMovies, Constants.FavoriteMovieGenres, Constants.HelpUserTopMovieGenres);
        }

        public ValueGroup CalculateFavoriteShowGenres()
        {
            return CalculateFavoriteGenres(_allSeries, Constants.favoriteShowGenres, Constants.HelpUserTopShowGenres);
        }

        #endregion

        #region PlayedViewTime

        private ValueGroup CalculateTime(IEnumerable<BaseItem> items, bool onlyPlayed, string titleConstant)
        {
            var filteredItems = User == null
                ? items.Where(m => _allUsers.Any(u => _userDataManager.GetUserData(u, m).Played) || !onlyPlayed)
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
            return CalculateTime(_allMovies, onlyPlayed, onlyPlayed ? Constants.TotalWatched : Constants.TotalWatchableTime);
        }

        public ValueGroup CalculateShowTime(bool onlyPlayed = true)
        {
            return CalculateTime(_allEpisodes, onlyPlayed, onlyPlayed ? Constants.TotalWatched : Constants.TotalWatchableTime);
        }

        public ValueGroup CalculateOverallTime(bool onlyPlayed = true)
        {
            var totalTicks = (User == null
                    ? GetAllBaseItems().Where(m => _allUsers.Any(u => _userDataManager.GetUserData(u, m).Played) || !onlyPlayed)
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
                ValueLineThree = lineTwoTitle != null ? $"{GetOwnedCount(typeof(Episode))}" : null,
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
            int viewedMediaCount = 0;
            if (typeof(T) == typeof(MediaBrowser.Controller.Entities.Movies.Movie))
            {
                viewedMediaCount = GetAllViewedMoviesByUser().Count();
            }
            else if (typeof(T) == typeof(Episode))
            {
                viewedMediaCount = _allSeries.Sum(GetPlayedEpisodeCount);
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
            return CalculateTotalMediaWatched<MediaBrowser.Controller.Entities.Movies.Movie>(Constants.TotalMoviesWatched, Constants.HelpUserTotalMoviesWatched, totalMoviesCount => Math.Round(GetAllViewedMoviesByUser().Count() / (decimal)totalMoviesCount * 100m, 1));
        }

        public ValueGroup CalculateTotalEpiosodesWatched()
        {
            return CalculateTotalMediaWatched<Episode>(Constants.TotalEpisodesWatched, Constants.HelpUserTotalEpisodesWatched, totalEpisodesCount => Math.Round(_allSeries.Sum(GetPlayedEpisodeCount) / (decimal)totalEpisodesCount * 100m, 1));
        }

        public ValueGroup CalculateTotalFinishedShows()
        {
            int count = 0;

            foreach (var show in _allSeries)
            {
                if (_totalEpisodesPerSeries.TryGetValue(show.Id, out var totalEpisodesFromTvdb))
                {
                    var totalEpisodes = totalEpisodesFromTvdb;
                    var seenEpisodes = GetPlayedEpisodeCount(show);

                    if (seenEpisodes > totalEpisodes)
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
            var studioSet = new HashSet<string>(_allMovies 
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
            var networkSet = new HashSet<string>(_allSeries
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
                ValueLineOne = $"{_allUsers.Count}",
                ValueLineTwo = "",
                ValueLineThree = null,
            };
        }

        #endregion

        #region MostActiveUsers

        public ValueGroup CalculateMostActiveUsers(Dictionary<string, RunTime> users)
        {
            var mostActiveUsers = users.OrderByDescending(x => x.Value).Take(6);
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
                    qualityCounts[quality.Trim()].Movies++;
                    _logger.Debug($"CalculateMovieQualities {movie.Name} {quality}");
                }
                catch (Exception ex)
                {
                    _logger.Debug($"CalculateMovieQualities-Error {movie.Name}: {ex.Message}");
                }
            }

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
                    qualityCounts[quality.Trim()].Episodes++;
                    _logger.Debug($"CalculateMovieCodecs-episode {(episode.Series?.Name ?? "invalid name")}: {episode.SortName} {quality}");
                }
                catch (Exception ex)
                {
                    _logger.Debug($"CalculateMovieQualities-episode-Error {episode.Name}: {ex.Message}");
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

            int width = typeInfo.Width.Value;

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
                    codecCounts[codec].Movies++;

                    _logger.Debug($"CalculateMovieCodecs {movie.SortName} {codec}");
                }
                catch (Exception ex)
                {
                    _logger.Debug($"CalculateMovieCodecs-Error {movie.SortName}: {ex.Message}");
                }
            }

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
                    codecCounts[codec].Episodes++;
                    _logger.Debug($"CalculateMovieCodecs-episode {(episode.Series?.SortName ?? "invalid name")}: {episode.SortName} {codec}"); 
                }
                catch (Exception ex)
                {
                    _logger.Debug($"CalculateMovieCodecs-episode-Error {episode.SortName}: {ex.Message}");
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

            foreach (var movie in _allMovies.Where(w => w.SortName != null).OrderBy(x => x.SortName))
            {
                _logger.Debug($"CalculateMovieQualityList {movie.Name}");
                var quality = movie.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video)?.DisplayTitle?.Split(' ')[0];

                if (!qualityMovieMap.TryGetValue(quality, out var movieList))
                {
                    movieList = new List<statistics.Models.Movie>();
                    qualityMovieMap[quality] = movieList;
                }
                movieList.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                _logger.Debug($"{quality} {qualityMovieMap.Count}");
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
                    _logger.Debug($"CalculateBiggestMovie-Error: {ex.Message}");
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

            if (_allSeries.Any())
            {
                
                foreach (var show in _allSeries)
                {
                    double showSize = 0;
                    //This is assuming the recommened folder structure for series/season/episode
                    //https://github.com/MediaBrowser/Emby/wiki/TV-Library
                    foreach (var episode in _allEpisodes.Where(x => x.GetParent().GetParent().Id == show.Id && x.Path != null))
                    {
                        try
                        {
                            var f = _fileSystem.GetFileSystemInfo(episode.Path);
                            showSize += f.Length;
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"CalculateBiggestShow-Error getting file info for episode {episode.Name} in show {show.Name}: {e.Message}", e);
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
            var showList = _allSeries.OrderBy(x => x.SortName); 
            var users = _allUsers;
            var showProgress = new List<ShowProgress>();

            foreach (var user in users)
            {
                SetUser(user);
                foreach (var show in showList)
                {
                    if (_totalEpisodesPerSeries.TryGetValue(show.Id, out var totalEpisodesFromTvdb))
                    {
                        var totalEpisodes = totalEpisodesFromTvdb;
                        var collectedEpisodes = _collectedEpisodesPerSeries.TryGetValue(show.Id, out var _collected) ? _collected : 0;
                        var seenEpisodes = GetPlayedEpisodeCount(show);

                        if (collectedEpisodes > totalEpisodes && totalEpisodes > 0)
                        {
                            collectedEpisodes = totalEpisodes;
                        }

                        if (seenEpisodes > collectedEpisodes && collectedEpisodes > 0)
                        {
                            seenEpisodes = collectedEpisodes;
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
                                PercentSeen = Math.Round(Math.Min(watched, 100), 0),
                                CollectedEpisodes = collectedEpisodes,
                                SeenEpisodes = seenEpisodes,
                                PercentCollected = Math.Round(Math.Min(collected, 100), 0),
                                TotalEpisodes = totalEpisodes
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
                _logger.Debug($"CalculateMostWatchedShows {show.Name} {show.Watched}");
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
            var showList = _allSeries.OrderBy(x => x.SortName);
            var users = _allUsers;
            var showProgress = new List<ShowProgress>();

            foreach (var user in users)
            {
                SetUser(user);
                foreach (var show in showList)
                {
                    if (_totalEpisodesPerSeries.TryGetValue(show.Id, out var totalEpisodesFromTvdb))
                    {
                        var totalEpisodes = totalEpisodesFromTvdb;
                        var collectedEpisodes = _collectedEpisodesPerSeries.TryGetValue(show.Id, out var episodes) ? episodes : 0;
                        var seenEpisodes = GetPlayedEpisodeCount(show);

                        if (collectedEpisodes > totalEpisodes && totalEpisodes > 0)
                        {
                            collectedEpisodes = totalEpisodes;
                        }

                        if (seenEpisodes > collectedEpisodes && collectedEpisodes > 0)
                        {
                            seenEpisodes = collectedEpisodes;
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
                                CollectedEpisodes = collectedEpisodes,
                                SeenEpisodes = seenEpisodes,
                                CollectedSpecials = _collectedSpecialsPerSeries.TryGetValue(show.Id, out var specials) ? specials : 0,
                                SeenSpecials = GetPlayedSpecials(show),
                                PercentCollected = Math.Round(collected, 1),
                                TotalEpisodes = totalEpisodes
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
                _logger.Debug($"CalculateLeastWatchedShows {show.Name} {show.Watched}");
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

            if (_allMovies.Any())             {
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

            if (_allMovies.Any())
            {
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

            var maxMovie = _allMovies.Where(x => x.RunTimeTicks.HasValue).OrderByDescending(x => x.RunTimeTicks).FirstOrDefault();
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

            if (_allSeries.Any())
            {
                Series maxShow = null;
                long maxTime = 0;

                foreach (var show in _allSeries)
                {
                    long showTime = 0;
                    //This is assuming the recommened folder structure for series/season/episode
                    //https://github.com/MediaBrowser/Emby/wiki/TV-Library
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

            if (_allMovies.Any())
            {
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

            if (_allMovies.Any())
            {
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

            if (_allMovies.Any())
            {
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

            if (_allEpisodes.Any())
            {
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

                    valueLineTwo = CheckMaxLength($"{youngest.Series?.Name} S{youngest.Season?.IndexNumber} E{youngest.IndexNumber} ");
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

            if (_allSeries.Any())
            {
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

            if (_allSeries.Any())
            {
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

            var highestRatedMovie = _allMovies
                .Where(x => x.CommunityRating.HasValue)
                .OrderByDescending(x => x.CommunityRating)
                .FirstOrDefault();


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

            
            var lowestRatedMovie = _allMovies
                .Where(x => x.CommunityRating.HasValue && x.CommunityRating != 0)
                .OrderBy(x => x.CommunityRating)
                .FirstOrDefault();


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