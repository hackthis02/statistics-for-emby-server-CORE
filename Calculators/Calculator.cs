using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
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
        private IFileSystem _fileSystem;
        private ILogger _logger;
        public Calculator(User user, IUserManager userManager, ILibraryManager libraryManager, IUserDataManager userDataManager, IFileSystem fileSystem, ILogger logger)
            : base(userManager, libraryManager, userDataManager)
        {
            User = user;
            _fileSystem = fileSystem;
            _logger = logger;

        }

        #region TopYears

        public ValueGroup CalculateFavoriteYears()
        {
            var movieList = User == null
                ? GetAllMovies().Where(m => GetAllUser().Any(m.IsPlayed))
                : GetAllMovies().Where(m => m.IsPlayed(User)).ToList();
            var list = movieList.Select(m => m.ProductionYear ?? 0).Distinct().ToList();
            var source = new Dictionary<int, int>();
            foreach (var num1 in list)
            {
                var year = num1;
                var num2 = movieList.Count(m => (m.ProductionYear ?? 0) == year);
                source.Add(year, num2);
            }

            return new ValueGroup
            {
                Title = Constants.FavoriteYears,
                ValueLineOne = string.Join(", ", source.OrderByDescending(g => g.Value).Take(5).Select(g => g.Key).ToList()),
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserToMovieYears : null,
                Size = "half"
            };
        }

        #endregion

        #region LastSeen

        public ValueGroup CalculateLastSeenShows()
        {
            var viewedEpisodes = GetAllViewedEpisodesByUser()
                .OrderByDescending(
                    m =>
                        UserDataManager.GetUserData(
                                User ??
                                GetAllUser().FirstOrDefault<User>(u => m.IsPlayed(u) && UserDataManager.GetUserData(u, m).LastPlayedDate.HasValue), m)
                            .LastPlayedDate)
                .Take(8).ToList();

            var lastSeenList = viewedEpisodes
                .Select(item => new LastSeenModel
                {
                    Name = item.SeriesName + " - " + item.Name,
                    Played = UserDataManager.GetUserData(User, item).LastPlayedDate?.DateTime ?? DateTime.MinValue,
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
            var viewedMovies = GetAllViewedMoviesByUser()
                .OrderByDescending(
                    m =>
                        UserDataManager.GetUserData(
                                User ??
                                GetAllUser().FirstOrDefault<User>(u => m.IsPlayed(u) && UserDataManager.GetUserData(u, m).LastPlayedDate.HasValue), m)
                            .LastPlayedDate)
                .Take(8).ToList();

            var lastSeenList = viewedMovies
                .Select(item => new LastSeenModel
                {
                    Name = item.Name,
                    Played = UserDataManager.GetUserData(User, item).LastPlayedDate?.DateTime ?? DateTime.MinValue,
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

        public ValueGroup CalculateFavoriteMovieGenres()
        {
            var result = new Dictionary<string, int>();
            var genres = GetAllMovies().Where(m => m.IsVisible(User)).SelectMany(m => m.Genres).Distinct();

            foreach (var genre in genres)
            {
                var num = GetAllMovies().Count(m => m.Genres.Contains(genre));
                result.Add(genre, num);
            }

            return new ValueGroup
            {
                Title = Constants.FavoriteMovieGenres,
                ValueLineOne = string.Join(", ", result.OrderByDescending(g => g.Value).Take(3).Select(g => g.Key).ToList()),
                ExtraInformation = User != null ? Constants.HelpUserTopMovieGenres : null,
                ValueLineTwo = "",
                ValueLineThree = null,
                Size = "half"
            };
        }

        public ValueGroup CalculateFavoriteShowGenres()
        {
            var result = new Dictionary<string, int>();
            var genres = GetAllSeries().Where(m => m.IsVisible(User)).SelectMany(m => m.Genres).Distinct();

            foreach (var genre in genres)
            {
                var num = GetAllSeries().Count(m => m.Genres.Contains(genre));
                result.Add(genre, num);
            }

            return new ValueGroup
            {
                Title = Constants.favoriteShowGenres,
                ValueLineOne = string.Join(", ", result.OrderByDescending(g => g.Value).Take(3).Select(g => g.Key).ToList()),
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserTopShowGenres : null,
                Size = "mediumThin"
            };
        }

        #endregion

        #region PlayedViewTime

        public ValueGroup CalculateMovieTime(bool onlyPlayed = true)
        {
            var runTime = new RunTime();
            var movies = User == null
                ? GetAllMovies().Where(m => GetAllUser().Any(m.IsPlayed) || !onlyPlayed)
                : GetAllMovies().Where(m => (m.IsPlayed(User) || !onlyPlayed) && m.IsVisible(User));
            foreach (var movie in movies)
            {
                runTime.Add(movie.RunTimeTicks);
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

        public ValueGroup CalculateShowTime(bool onlyPlayed = true)
        {
            var runTime = new RunTime();
            var shows = User == null
                ? GetAllOwnedEpisodes().Where(m => GetAllUser().Any(m.IsPlayed) || !onlyPlayed)
                : GetAllOwnedEpisodes().Where(m => (m.IsPlayed(User) || !onlyPlayed) && m.IsVisible(User));
            foreach (var show in shows)
            {
                runTime.Add(show.RunTimeTicks);
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

        public ValueGroup CalculateOverallTime(bool onlyPlayed = true)
        {
            var runTime = new RunTime();
            var items = User == null
                ? GetAllBaseItems().Where(m => GetAllUser().Any(m.IsPlayed) || !onlyPlayed)
                : GetAllBaseItems().Where(m => (m.IsPlayed(User) || !onlyPlayed) && m.IsVisible(User));
            foreach (var item in items)
            {
                runTime.Add(item.RunTimeTicks);
            }

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

        public ValueGroup CalculateTotalMovies()
        {
            return new ValueGroup
            {
                Title = Constants.TotalMovies,
                ValueLineOne = $"{GetOwnedCount(typeof(MediaBrowser.Controller.Entities.Movies.Movie))}",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserTotalMovies : null
            };
        }

        public ValueGroup CalculateTotalShows()
        {
            return new ValueGroup
            {
                Title = Constants.TotalShows,
                ValueLineOne = $"{GetOwnedCount(typeof(Series))}",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserTotalShows : null
            };
        }

        public ValueGroup CalculateTotalOwnedEpisodes()
        {
            return new ValueGroup
            {
                Title = Constants.TotalEpisodes,
                ValueLineOne = $"{GetOwnedCount(typeof(Episode))}",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserTotalEpisode : null
            };
        }

        public ValueGroup CalculateTotalBoxsets()
        {
            return new ValueGroup
            {
                Title = Constants.TotalCollections,
                ValueLineOne = $"{GetBoxsets().Count()}",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserTotalCollections : null
            };
        }

        public ValueGroup CalculateTotalMoviesWatched()
        {
            var viewedMoviesCount = GetAllViewedMoviesByUser().Count();
            var totalMoviesCount = GetOwnedCount(typeof(MediaBrowser.Controller.Entities.Movies.Movie));

            var percentage = decimal.Zero;
            if (totalMoviesCount > 0)
                percentage = Math.Round(viewedMoviesCount / (decimal)totalMoviesCount * 100, 1);


            return new ValueGroup
            {
                Title = Constants.TotalMoviesWatched,
                ValueLineOne = $"{viewedMoviesCount} ({percentage}%)",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserTotalMoviesWatched : null
            };
        }

        public ValueGroup CalculateTotalEpiosodesWatched()
        {
            var seenEpisodesCount = GetAllSeries().ToList().Sum(GetPlayedEpisodeCount);
            var totalEpisodes = GetOwnedCount(typeof(Episode));

            var percentage = decimal.Zero;
            if (totalEpisodes > 0)
                percentage = Math.Round(seenEpisodesCount / (decimal)totalEpisodes * 100, 1);

            return new ValueGroup
            {
                Title = Constants.TotalEpisodesWatched,
                ValueLineOne = $"{seenEpisodesCount} ({percentage}%)",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = User != null ? Constants.HelpUserTotalEpisodesWatched : null
            };
        }

        public ValueGroup CalculateTotalFinishedShows(UpdateModel tvdbData)
        {
            var showList = GetAllSeries();
            var count = 0;

            foreach (var show in showList)
            {
                var totalEpisodes = tvdbData.IdList.FirstOrDefault(x => x.ShowId == show.GetProviderId(MetadataProviders.Tvdb))?.Count ?? 0;
                var seenEpisodes = GetPlayedEpisodeCount(show);

                if (seenEpisodes > totalEpisodes)
                    totalEpisodes = seenEpisodes;

                if (totalEpisodes > 0 && totalEpisodes == seenEpisodes)
                    count++;
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
            var movies = GetAllMovies();
            List<string> studios = new List<string>();

            foreach (var studio in movies.Where(x => x.Studios.Any()).Select(x => x.Studios).ToList())
            {
                for (int i = 0; i < studio.Count(); i++)
                    if (studios.IndexOf(studio[i]) == -1)
                        studios.Add(studio[i]);
            }

            var count = studios.Count();
            return new ValueGroup
            {
                Title = Constants.TotalStudios,
                ValueLineOne = $"{count}",
                ValueLineTwo = "",
                ValueLineThree = null,
            };
        }

        public ValueGroup CalculateTotalShowStudios() 
        {
            var series = GetAllSeries();
            List<string> networks = new List<string>();

            foreach (var network in series.Where(x => x.Studios.Any()).Select(x => x.Studios).ToList())
            {
                for (int i = 0; i < network.Count(); i++) 
                    if (networks.IndexOf(network[i]) == -1)
                        networks.Add(network[i]);
            }

            var count = networks.Count();
            return new ValueGroup
            {
                Title = Constants.TotalNetworks,
                ValueLineOne = $"{count}",
                ValueLineTwo = "",
                ValueLineThree = null,
            };
        }

        public ValueGroup CalculateTotalUsers()
        {
            var users = GetAllUser();

            return new ValueGroup
            {
                Title = Constants.TotalUsers,
                ValueLineOne = $"{users.Count()}",
                ValueLineTwo = "",
                ValueLineThree = null,
            };
        }

        #endregion

        #region MostActiveUsers

        public ValueGroup CalculateMostActiveUsers(Dictionary<string, RunTime> users)
        {
            var mostActiveUsers = users.OrderByDescending(x => x.Value).Take(6);

            var tempList = mostActiveUsers.Select(x => $"<tr><td>{x.Key}</td>{x.Value.ToString()}</tr>");

            return new ValueGroup
            {
                Title = Constants.MostActiveUsers,
                ValueLineOne = $"<table><tr><td></td><td>Days</td><td>Hours</td><td>Minutes</td></tr>{string.Join("", tempList)}</table>",
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
            var movies = GetAllMovies();
            var episodes = GetAllOwnedEpisodes();

            var qualityList = new List<VideoQualityModel>();

            foreach (var movie in movies.Where(w => w.Name != null).OrderBy(x => x.Name))
            {
                var quality = GetMediaResolution(movie.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video));
                var index = qualityList.FindIndex(p => p != null && p.Quality != null && p.Quality.Equals(quality.Trim()));
                _logger.Debug("CalculateMovieQualities " + movie.Name + ' ' + quality);

                if (index == -1)
                {
                    qualityList.Add(new VideoQualityModel
                    {
                        Quality = quality.Trim(),
                        Movies = 1,
                        Episodes = 0
                    });
                }
                else
                {
                    qualityList[index].Movies++;
                }
            }

            foreach (var episode in episodes.Where(w => w.Name != null).OrderBy(x => x.Name))
            {
                var quality = GetMediaResolution(episode.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video));
                var index = qualityList.FindIndex(p => p != null && p.Quality != null && p.Quality.Equals(quality.Trim()));
                _logger.Debug("CalculateMovieQualities-episode " + (episode.Series.Name ?? "invalid Series name") + ": " + episode.Name + ' ' + quality);

                if (index == -1)
                {
                    qualityList.Add(new VideoQualityModel
                    {
                        Quality = quality.Trim(),
                        Movies = 0,
                        Episodes = 1
                    });
                }
                else
                {
                    qualityList[index].Episodes++;
                }
            }

            return new ValueGroup
            {
                Title = Constants.MediaQualities,
                ValueLineOne = $"<table><tr><td></td><td>Movies</td><td>Episodes</td></tr>{string.Join("", qualityList)}</table>",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = Constants.HelpQualities,
                Size = "half"
            };
        }

        string GetMediaResolution(MediaStream typeInfo)
        {
            string resolution = "";

            if (typeInfo == null || typeInfo.Width == null)
                return "Resolution Not Available";

            if (Convert.ToInt32(typeInfo.Width) >= 1281 && Convert.ToInt32(typeInfo.Width) <= 1920)
            {
                resolution = "1080p";
            }
            else if (Convert.ToInt32(typeInfo.Width) >= 3841 && Convert.ToInt32(typeInfo.Width) <= 7680)
            {
                resolution = "8K";
            }
            else if (Convert.ToInt32(typeInfo.Width) >= 1921 && Convert.ToInt32(typeInfo.Width) <= 3840)
            {
                resolution = "4K";
            }
            else if (Convert.ToInt32(typeInfo.Width) >= 1200 && Convert.ToInt32(typeInfo.Width) <= 1280)
            {
                resolution = "720p";
            }
            else if (Convert.ToInt32(typeInfo.Width) < 1200)
            {
                resolution = "SD";
            }

            return resolution;
        }

        public ValueGroup CalculateMovieCodecs()
        {
            var movies = GetAllMovies();
            var episodes = GetAllOwnedEpisodes();

            var qualityList = new List<VideoCodecModel>();

            foreach (var movie in movies.Where(w => w.SortName != null).OrderBy(x => x.SortName))
            {
                var codec = movie.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video)?.Codec ?? "Unknown".Trim();
                var index = qualityList.FindIndex(p => p != null && p.Codec != null && p.Codec.Equals(codec));
                _logger.Debug("CalculateMovieCodecs " + movie.SortName + ' ' + codec);

                if (index == -1)
                {
                    qualityList.Add(new VideoCodecModel
                    {
                        Codec = codec,
                        Movies = 1,
                        Episodes = 0
                    });
                }
                else
                {
                    qualityList[index].Movies++;
                }
            }

            foreach (var episode in episodes.Where(w => w.SortName != null).OrderBy(x => x.SortName))
            {
                var codec = episode.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video)?.Codec ?? "Unknown".Trim();
                var index = qualityList.FindIndex(p => p != null && p.Codec != null && p.Codec.Equals(codec));
                _logger.Debug("CalculateMovieCodecs-episode " + ((episode.Series.SortName != null) ? (episode.Series.SortName) : ("invalid name")) + ": " + episode.SortName + ' ' + codec);

                if (index == -1)
                {
                    qualityList.Add(new VideoCodecModel
                    {
                        Codec = codec,
                        Movies = 0,
                        Episodes = 1
                    });
                }
                else
                {
                    qualityList[index].Episodes++;
                }
            }

            return new ValueGroup
            {
                Title = Constants.MediaCodecs,
                ValueLineOne = $"<table><tr><td></td><td>Movies</td><td>Episodes</td></tr>{string.Join("", qualityList)}</table>",
                ValueLineTwo = "",
                ValueLineThree = null,
                ExtraInformation = Constants.HelpCodec,
                Size = "half"
            };
        }

        public MovieQualityObj CalculateMovieQualityList()
        {
            var movies = GetAllMovies();
            var list = new List<MovieQuality>();

            foreach (var movie in movies.Where(w => w.SortName != null).OrderBy(x => x.SortName))
            {
                _logger.Debug("CalculateMovieQualityList " + movie.Name);
                var quality = movie.GetMediaStreams().FirstOrDefault(s => s != null && s.Type == MediaStreamType.Video)?.DisplayTitle.Split(' ')[0];
                var index = list.FindIndex(p => p != null && p.Title != null && p.Title.Equals(quality));
                _logger.Debug(quality + ' ' + index);

                if (index == -1)
                {
                    var temp = new List<statistics.Models.Movie>();
                    temp.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });

                    list.Add(new MovieQuality
                    {
                        Title = quality,
                        Movies = temp
                    });
                }
                else
                {
                    list[index].Movies.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                }
            }
            var mobj = new MovieQualityObj()
            {
                Count = list.Count(),
                Movies = list
            };
            return mobj;
        }
        #endregion

        #region Size

        public ValueGroup CalculateBiggestMovie()
        {
            var movies = GetAllMovies();

            var biggestMovie = new MediaBrowser.Controller.Entities.Movies.Movie();
            double maxSize = 0;
            foreach (var movie in movies)
            {
                try
                {
                    var f = _fileSystem.GetFileSystemInfo(movie.Path);
                    if (maxSize >= f.Length) continue;

                    maxSize = f.Length;
                    biggestMovie = movie;
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            if (biggestMovie.Id.ToString() != "00000000-0000-0000-0000-000000000000")
            {
                maxSize = maxSize / 1073741824; //Byte to Gb
                var valueLineOne = CheckMaxLength($"{maxSize:F1} Gb");
                var valueLineTwo = CheckMaxLength($"{biggestMovie.Name}");

                return new ValueGroup
                {
                    Title = Constants.BiggestMovie,
                    ValueLineOne = valueLineOne,
                    ValueLineTwo = valueLineTwo,
                    ValueLineThree = null,
                    Size = "half",
                    Id = biggestMovie.Id.ToString() != "" ? biggestMovie.Id.ToString() : null
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
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var shows = GetAllSeries();
            if (shows.Any())
            {
                var biggestShow = new Series();
                double maxSize = 0;
                foreach (var show in shows)
                {
                    //This is assuming the recommened folder structure for series/season/episode
                    //https://github.com/MediaBrowser/Emby/wiki/TV-Library
                    var episodes = GetAllEpisodes().Where(x => x.GetParent().GetParent().Id == show.Id && x.Path != null);
                    try
                    {
                        var showSize = episodes.Sum(x =>
                        {
                            var f = _fileSystem.GetFileSystemInfo(x.Path);
                            return f.Length;
                        });

                        if (maxSize >= showSize) continue;

                        maxSize = showSize;
                        biggestShow = show;
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e.Message.ToString(), e);
                    }
                }

                maxSize = maxSize / 1073741824; //Byte to Gb
                valueLineOne = CheckMaxLength($"{maxSize:F1} Gb");
                valueLineTwo = CheckMaxLength($"{biggestShow.Name}");
                id = biggestShow.Id.ToString();
            }

            return new ValueGroup
            {
                Title = Constants.BiggestShow,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id != "" ? id : null
            };
        }

        public ValueGroup CalculateMostWatchedShows(UpdateModel tvdbData)
        {
            var showList = GetAllSeries().OrderBy(x => x.SortName);
            var users = GetAllUser();
            var showProgress = new List<ShowProgress>();

            foreach (var user in users)
            {
                foreach (var show in showList)
                {

                    SetUser(user);
                    var totalEpisodes = tvdbData.IdList.FirstOrDefault(x => x.ShowId == show.GetProviderId(MetadataProviders.Tvdb))?.Count ?? 0;
                    var collectedEpisodes = GetOwnedEpisodesCount(show);
                    var seenEpisodes = GetPlayedEpisodeCount(show);
                    var totalSpecials = GetOwnedSpecials(show);
                    var seenSpecials = GetPlayedSpecials(show);

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

                    var index = showProgress.FindIndex(x => x.Name == show.Name);

                    if (index != -1)
                    {
                        showProgress[index].Watched += Math.Round(watched, 1);
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
                            Specials = totalSpecials,
                            SeenSpecials = seenSpecials,
                            Collected = Math.Round(collected, 1),
                            Total = totalEpisodes
                        });
                    }
                }
            }


            foreach (var show in showProgress)
            {
                show.Watched = Math.Round(show.Watched / users.Count(), 1);
            }

            List<ShowProgress> SortedList = showProgress.OrderByDescending(o => o.Watched).ToList();

            foreach (var show in SortedList)
            {
                _logger.Debug("CalculateMostWatchedShows " + show.Name + " " + show.Watched);
            }

            var lineone = "";
            var linetwo = "";
            var linethree = "";

            if(SortedList.Count >= 1)
                lineone = SortedList[0].Name;
            if (SortedList.Count >= 2)
                linetwo = SortedList[1].Name;
            if (SortedList.Count >= 3)
                linethree = SortedList[2].Name;

            return new ValueGroup
            {
                Title = Constants.MostWatchedShows,
                ValueLineOne = lineone,
                ValueLineTwo = linetwo,
                ValueLineThree = linethree,
                ExtraInformation = Constants.HelpUserMostWatchedShows
            };
        }

        public ValueGroup CalculateHighestBitrateMovie()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var movies = GetAllMovies().ToList();
            if (movies.Any())
            {
                var largest = movies.Aggregate((curMax, x) => curMax == null || x.TotalBitrate > curMax.TotalBitrate ? x : curMax);


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
                Id = id != "" ? id : null
            };
        }

        public ValueGroup CalculateLowestBitrateMovie()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var movies = GetAllMovies().ToList();
            if (movies.Any())
            {
                var lowest = movies.Aggregate((curMax, x) => curMax == null || x.TotalBitrate < curMax.TotalBitrate && x.TotalBitrate > 0 ? x : curMax);

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
                Id = id != "" ? id : null
            };
        }

        #endregion

        #region Period

        public ValueGroup CalculateLongestMovie()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";
            var movies = GetAllMovies();

            var maxMovie = movies.Where(x => x.RunTimeTicks != null).OrderByDescending(x => x.RunTimeTicks).FirstOrDefault();
            if (maxMovie != null)
            {
                valueLineOne = CheckMaxLength(new TimeSpan(maxMovie.RunTimeTicks ?? 0).ToString(@"hh\:mm\:ss"));
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
                Id = id != "" ? id : null
            };
        }

        public ValueGroup CalculateLongestShow()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var shows = GetAllSeries();

            if (shows.Any())
            {
                var maxShow = new Series();
                long maxTime = 0;
                foreach (var show in shows)
                {
                    try
                    {
                        //This is assuming the recommened folder structure for series/season/episode
                        //https://github.com/MediaBrowser/Emby/wiki/TV-Library
                        var episodes = GetAllEpisodes().Where(x => x.GetParent().GetParent().Id == show.Id && x.Path != null);
                        var showSize = episodes.Sum(x => x.RunTimeTicks ?? 0);

                        if (maxTime >= showSize) continue;

                        maxTime = showSize;
                        maxShow = show;
                    }
                    catch (Exception) { }

                }

                var time = new TimeSpan(maxTime).ToString(@"hh\:mm\:ss");

                var days = CheckForPlural("day", new TimeSpan(maxTime).Days, "", "and");

                valueLineOne = CheckMaxLength($"{days} {time}");
                valueLineTwo = CheckMaxLength($"{maxShow.Name}");
                id = maxShow.Id.ToString();
            }

            return new ValueGroup
            {
                Title = Constants.LongestShow,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                ValueLineThree = null,
                Size = "half",
                Id = id != "" ? id : null
            };
        }

        #endregion

        #region Release Date

        public ValueGroup CalculateOldestMovie()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var movies = GetAllMovies();
            if (movies.Any())
            {
                var oldest = movies.Where(x => x.PremiereDate.HasValue && x.PremiereDate.Value.DateTime > DateTime.MinValue).Aggregate((curMin, x) => (curMin == null || (x.PremiereDate?.DateTime ?? DateTime.MaxValue) < curMin.PremiereDate ? x : curMin));

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
                Id = id != "" ? id : null
            };
        }

        public ValueGroup CalculateNewestMovie()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var movies = GetAllMovies();
            if (movies.Any())
            {
                var youngest = movies.Where(x => x.PremiereDate.HasValue).Aggregate((curMax, x) => (curMax == null || (x.PremiereDate?.DateTime ?? DateTime.MinValue) > curMax.PremiereDate?.DateTime ? x : curMax));

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
                Id = id != "" ? id : null
            };
        }

        public ValueGroup CalculateNewestAddedMovie()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var movies = GetAllMovies().Where(x => x.DateCreated.DateTime != DateTime.MinValue).ToList();
            if (movies.Any())
            {
                var youngest = movies.Aggregate((curMax, x) => curMax == null || x.DateCreated.DateTime > curMax.DateCreated.DateTime ? x : curMax);

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
                Id = id != "" ? id : null
            };
        }

        public ValueGroup CalculateNewestAddedEpisode()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var episodes = GetAllOwnedEpisodes().Where(x => x.DateCreated.DateTime != DateTime.MinValue).ToList();
            if (episodes.Any())
            {
                var youngest = episodes.Aggregate((curMax, x) => (curMax == null || x.DateCreated.DateTime > curMax.DateCreated.DateTime ? x : curMax));
                if (youngest != null)
                {
                    var numberOfTotalDays = DateTime.Now.Date - youngest.DateCreated.DateTime;

                    valueLineOne =
                        CheckMaxLength(numberOfTotalDays.Days == 0
                            ? "Today"
                            : $"{CheckForPlural("day", numberOfTotalDays.Days, "", "", false)} ago");

                    valueLineTwo = CheckMaxLength($"{youngest.Series?.Name} S{youngest.Season.IndexNumber} E{youngest.IndexNumber} ");
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
                Id = id != "" ? id : null
            };
        }

        public ValueGroup CalculateOldestShow()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var shows = GetAllSeries();
            if (shows.Any())
            {
                var oldest = shows.Where(x => x.PremiereDate.HasValue && x.PremiereDate.Value.DateTime > DateTime.MinValue).Aggregate((curMin, x) => (curMin == null || (x.PremiereDate?.DateTime ?? DateTime.MaxValue) < curMin.PremiereDate ? x : curMin));

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
                Id = id != "" ? id : null
            };
        }

        public ValueGroup CalculateNewestShow()
        {
            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";

            var shows = GetAllSeries();
            if (shows.Any())
            {
                var youngest = shows.Where(x => x.PremiereDate.HasValue).Aggregate((curMax, x) => (curMax == null || (x.PremiereDate?.DateTime ?? DateTime.MinValue) > curMax.PremiereDate?.DateTime ? x : curMax));

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
                Id = id != "" ? id : null
            };
        }

        #endregion

        #region Ratings

        public ValueGroup CalculateHighestRating()
        {
            var movies = GetAllMovies();
            var highestRatedMovie = movies.Where(x => x.CommunityRating.HasValue).OrderByDescending(x => x.CommunityRating).FirstOrDefault();

            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";
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
                Id = id != "" ? id : null
            };
        }

        public ValueGroup CalculateLowestRating()
        {
            var movies = GetAllMovies();
            var lowestRatedMovie = movies.Where(x => x.CommunityRating.HasValue && x.CommunityRating != 0).OrderBy(x => x.CommunityRating).FirstOrDefault();

            var valueLineOne = Constants.NoData;
            var valueLineTwo = "";
            var id = "";
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
                Id = id != "" ? id : null
            };
        }

        #endregion

        private string CheckMaxLength(string value)
        {
            if (value.Length > 30)
                return value.Substring(0, 27) + "...";
            return value; ;
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