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
using Statistics.Enum;
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
                ? GetAllMovies().Where(m => UserManager.Users.Any(m.IsPlayed))
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
                                UserManager.Users.FirstOrDefault<User>(u => m.IsPlayed(u) && UserDataManager.GetUserData(u, m).LastPlayedDate.HasValue), m)
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
                                UserManager.Users.FirstOrDefault<User>(u => m.IsPlayed(u) && UserDataManager.GetUserData(u, m).LastPlayedDate.HasValue), m)
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
                ? GetAllMovies().Where(m => UserManager.Users.Any(m.IsPlayed) || !onlyPlayed)
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
                Size = "half"
            };
        }

        public ValueGroup CalculateShowTime(bool onlyPlayed = true)
        {
            var runTime = new RunTime();
            var shows = User == null
                ? GetAllOwnedEpisodes().Where(m => UserManager.Users.Any(m.IsPlayed) || !onlyPlayed)
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
                Size = "half"
            };
        }

        public ValueGroup CalculateOverallTime(bool onlyPlayed = true)
        {
            var runTime = new RunTime();
            var items = User == null
                ? GetAllBaseItems().Where(m => UserManager.Users.Any(m.IsPlayed) || !onlyPlayed)
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
                ExtraInformation = User != null ? Constants.HelpUserTotalShowsFinished : null
            };
        }

        public ValueGroup CalculateTotalMovieStudios()
        {
            var movies = GetAllMovies();
            var studios = movies.Where(x => x.Studios.Any()).Select(x => x.Studios).ToList();

            var count = studios.Distinct().Count();
            return new ValueGroup
            {
                Title = Constants.TotalStudios,
                ValueLineOne = $"{count}",
                ValueLineTwo = ""
            };
        }

        public ValueGroup CalculateTotalShowStudios()
        {
            var series = GetAllSeries();
            var studios = series.Where(x => x.Studios.Any()).Select(x => x.Studios).ToList();

            var count = studios.Distinct().Count();
            return new ValueGroup
            {
                Title = Constants.TotalStudios,
                ValueLineOne = $"{count}",
                ValueLineTwo = ""
            };
        }

        public ValueGroup CalculateTotalUsers()
        {
            var users = GetAllUser();

            return new ValueGroup
            {
                Title = Constants.TotalUsers,
                ValueLineOne = $"{users.Count()}",
                ValueLineTwo = ""
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
                Size = "medium",
                ExtraInformation = Constants.HelpMostActiveUsers
            };
        }

        #endregion

        #region Quality

        public ValueGroup CalculateMovieQualities()
        {
            var movies = GetAllMovies();
            var episodes = GetAllOwnedEpisodes();

            var moWidths = movies
                    .Select(x => x.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video))
                    .Select(x => x != null ? x.Width : 0)
                    .ToList();
            var epWidths = episodes
                    .Select(x => x.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video))
                    .Select(x => x != null ? x.Width : 0)
                    .ToList();

            var ceilings = new[] { 3800, 2500, 1900, 1260, 700, 10 };
            var moGroupings = moWidths.GroupBy(item => ceilings.FirstOrDefault(ceiling => ceiling < item)).ToList();
            var epGroupings = epWidths.GroupBy(item => ceilings.FirstOrDefault(ceiling => ceiling < item)).ToList();

            var qualityList = new List<VideoQualityModel>
            {
                new VideoQualityModel
                {
                    Quality = VideoQuality.UNKNOWN,
                    Movies = moGroupings.FirstOrDefault(x => x.Key == 0)?.Count() ?? 0,
                    Episodes = epGroupings.FirstOrDefault(x => x.Key == 0)?.Count() ?? 0
                },
                new VideoQualityModel
                {
                    Quality = VideoQuality.DVD,
                    Movies = moGroupings.FirstOrDefault(x => x.Key == 10)?.Count() ?? 0,
                    Episodes = epGroupings.FirstOrDefault(x => x.Key == 10)?.Count() ?? 0
                },
                new VideoQualityModel
                {
                    Quality = VideoQuality.Q700,
                    Movies = moGroupings.FirstOrDefault(x => x.Key == 700)?.Count() ?? 0,
                    Episodes = epGroupings.FirstOrDefault(x => x.Key == 700)?.Count() ?? 0
                },
                new VideoQualityModel
                {
                    Quality = VideoQuality.Q1260,
                    Movies = moGroupings.FirstOrDefault(x => x.Key == 1260)?.Count() ?? 0,
                    Episodes = epGroupings.FirstOrDefault(x => x.Key == 1260)?.Count() ?? 0
                },
                new VideoQualityModel
                {
                    Quality = VideoQuality.Q1900,
                    Movies = moGroupings.FirstOrDefault(x => x.Key == 1900)?.Count() ?? 0,
                    Episodes = epGroupings.FirstOrDefault(x => x.Key == 1900)?.Count() ?? 0
                },
                new VideoQualityModel
                {
                    Quality = VideoQuality.Q2500,
                    Movies = moGroupings.FirstOrDefault(x => x.Key == 2500)?.Count() ?? 0,
                    Episodes = epGroupings.FirstOrDefault(x => x.Key == 2500)?.Count() ?? 0
                },
                new VideoQualityModel
                {
                    Quality = VideoQuality.Q3800,
                    Movies = moGroupings.FirstOrDefault(x => x.Key == 3800)?.Count() ?? 0,
                    Episodes = epGroupings.FirstOrDefault(x => x.Key == 3800)?.Count() ?? 0
                }
            };

            return new ValueGroup
            {
                Title = Constants.MediaQualities,
                ValueLineOne = $"<table><tr><td></td><td>Movies</td><td>Episodes</td></tr>{string.Join("", qualityList)}</table>",
                ValueLineTwo = "",
                Size = "medium"
            };
        }

        public List<MovieQuality> CalculateMovieQualityList()
        {
            var movies = GetAllMovies();
            var list = new List<MovieQuality>
            {

                new MovieQuality { Quality = VideoQuality.UNKNOWN, Movies = new List<statistics.Models.Movie>() },
                new MovieQuality { Quality = VideoQuality.DVD, Movies = new List<statistics.Models.Movie>() },
                new MovieQuality { Quality = VideoQuality.Q700, Movies = new List<statistics.Models.Movie>() },
                new MovieQuality { Quality = VideoQuality.Q1260, Movies = new List<statistics.Models.Movie>() },
                new MovieQuality { Quality = VideoQuality.Q1900, Movies = new List<statistics.Models.Movie>() },
                new MovieQuality { Quality = VideoQuality.Q2500, Movies = new List<statistics.Models.Movie>() },
                new MovieQuality { Quality = VideoQuality.Q3800, Movies = new List<statistics.Models.Movie>() }
            };
            var listDVD = new List<statistics.Models.Movie>();
            foreach (var movie in movies.OrderBy(x => x.SortName))
            {
                if ((movie.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video)?.Width ?? 0) == 0)
                {
                    list[0].Movies.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                }
                else if (movie.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video)?.Width < 700)
                {
                    list[1].Movies.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                }
                else if (movie.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video)?.Width < 1260)
                {
                    list[2].Movies.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                }
                else if (movie.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video)?.Width < 1900)
                {
                    list[3].Movies.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                }
                else if (movie.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video)?.Width < 2500)
                {
                    list[4].Movies.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                }
                else if (movie.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video)?.Width < 3800)
                {
                    list[5].Movies.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                }
                else if (movie.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video)?.Width >= 3800)
                {
                    list[6].Movies.Add(new statistics.Models.Movie { Id = movie.Id.ToString(), Name = movie.Name, Year = movie.ProductionYear });
                }
            }

            return list;
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

            maxSize = maxSize / 1073741824; //Byte to Gb
            var valueLineOne = CheckMaxLength($"{maxSize:F1} Gb");
            var valueLineTwo = CheckMaxLength($"{biggestMovie.Name}");

            return new ValueGroup
            {
                Title = Constants.BiggestMovie,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                Size = "half",
                Id = biggestMovie.Id.ToString()
            };
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
                    catch (Exception)
                    {
                        // ignored
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
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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
                    //This is assuming the recommened folder structure for series/season/episode
                    //https://github.com/MediaBrowser/Emby/wiki/TV-Library
                    var episodes = GetAllEpisodes().Where(x => x.GetParent().GetParent().Id == show.Id && x.Path != null);
                    var showSize = episodes.Sum(x => x.RunTimeTicks ?? 0);

                    if (maxTime >= showSize) continue;

                    maxTime = showSize;
                    maxShow = show;

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
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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

                    valueLineTwo = CheckMaxLength($"{youngest.Series?.Name} S{youngest.AiredSeasonNumber} E{youngest.IndexNumber} ");
                    id = youngest.Id.ToString();
                }
            }

            return new ValueGroup
            {
                Title = Constants.NewestAddedEpisode,
                ValueLineOne = valueLineOne,
                ValueLineTwo = valueLineTwo,
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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
                Size = "half",
                Id = id
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