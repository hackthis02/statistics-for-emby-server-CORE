using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using statistics;
using statistics.Configuration;
using statistics.Models.Configuration;
using Statistics.Helpers;
using Statistics.ViewModel;

namespace Statistics.ScheduledTasks
{
    public class CalculateStatsTask : IScheduledTask
    {
        private readonly IFileSystem _fileSystem;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IServerApplicationPaths _serverApplicationPaths;
        private readonly IUserDataManager _userDataManager;
        private readonly IUserManager _userManager;
        private IApplicationHost _appHost;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IProviderManager _providerManager;

        public CalculateStatsTask(ILogManager logger,
            IUserManager userManager,
            IUserDataManager userDataManager,
            ILibraryManager libraryManager, IFileSystem fileSystem, IJsonSerializer jsonSerializer,
            IServerApplicationPaths serverApplicationPaths, IApplicationHost appHost, IProviderManager providerManager)
        {
            _logger = logger.GetLogger("Statistics");
            _libraryManager = libraryManager;
            _userManager = userManager;
            _userDataManager = userDataManager;
            _jsonSerializer = jsonSerializer;
            _fileSystem = fileSystem;
            _serverApplicationPaths = serverApplicationPaths;
            _appHost = appHost;
            _providerManager = providerManager;
        }

        private static PluginConfiguration PluginConfiguration => Plugin.Instance.Configuration;
        string IScheduledTask.Name => "Calculate statistics for all users";

        string IScheduledTask.Key => "StatisticsCalculateStatsTask";

        string IScheduledTask.Description => "Task that will calculate statistics needed for the statistics plugin for all users. (Ideal for weekly/non-daily schedule)";

        string IScheduledTask.Category => "Statistics";

        async Task IScheduledTask.Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var users = _userManager.GetUserList(new UserQuery() { EnableRemoteAccess = true }).ToList();

            // No users found, so stop the task
            if (users.Count == 0)
            {
                return;
            }

            // clear all previously saved stats
            PluginConfiguration.UserStats = new List<UserStat>();
            Plugin.Instance.SaveConfiguration();

            // purely for progress reporting
            var percentPerUser = 100 / (users.Count + 3);
            var numComplete = 0;

            PluginConfiguration.LastUpdated = DateTime.Now.ToString("g");
            PluginConfiguration.ServerId = _appHost.SystemId;

            numComplete++;
            progress.Report(percentPerUser * numComplete);

            numComplete++;
            progress.Report(percentPerUser * numComplete);

            var activeUsers = new Dictionary<string, RunTime>();

            var calculator = new Calculator(_userManager, _libraryManager, _userDataManager, _fileSystem, _logger, _providerManager, cancellationToken);
            var ShowProgresses = new ShowProgressCalculator(_userManager, _libraryManager, _userDataManager, _fileSystem, _serverApplicationPaths,
                                                            _logger, _jsonSerializer, _providerManager, cancellationToken);

            foreach (var user in users)
            {
                await Task.Run(() =>
                {
                    using (calculator)
                    {
                        calculator.User = user;
                        ShowProgresses.User = user;
                        var overallTime = calculator.CalculateOverallTime();
                        activeUsers.Add(user.Name, new RunTime(overallTime.Raw));
                        var stat = new UserStat
                        {
                            UserName = user.Name,
                            OverallStats = new List<ValueGroup>
                            {
                                overallTime,
                                calculator.CalculateOverallTime(false)
                            },
                            MovieStats = new List<ValueGroup>
                            {
                                calculator.CalculateTotalMovies(),
                                calculator.CalculateTotalBoxsets(),
                                calculator.CalculateTotalMoviesWatched(),
                                calculator.CalculateFavoriteYears(),
                                calculator.CalculateFavoriteMovieGenres(),
                                calculator.CalculateMovieTime(),
                                calculator.CalculateMovieTime(false),
                                calculator.CalculateLastSeenMovies()
                            },
                            ShowStats = new List<ValueGroup>
                            {
                                calculator.CalculateTotalShows(),
                                calculator.CalculateTotalOwnedEpisodes(),
                                calculator.CalculateTotalEpiosodesWatched(),
                                calculator.CalculateTotalFinishedShows(),
                                calculator.CalculateFavoriteShowGenres(),
                                calculator.CalculateShowTime(),
                                calculator.CalculateShowTime(false),
                                calculator.CalculateLastSeenShows()
                            },
                            ShowProgresses = ShowProgresses.CalculateShowProgress(cancellationToken)
                        };
                        PluginConfiguration.UserStats.Add(stat);
                    }
                }, cancellationToken);

                numComplete++;
                progress.Report(percentPerUser * numComplete);
            }

            using (calculator)
            {
                calculator.User = null;
                PluginConfiguration.MovieQualities = calculator.CalculateMovieQualities();
                PluginConfiguration.MovieCodecs = calculator.CalculateMovieCodecs();
                PluginConfiguration.MostActiveUsers = calculator.CalculateMostActiveUsers(activeUsers);
                PluginConfiguration.TotalUsers = calculator.CalculateTotalUsers();

                PluginConfiguration.TotalMovies = calculator.CalculateTotalMovies();
                PluginConfiguration.TotalBoxsets = calculator.CalculateTotalBoxsets();
                PluginConfiguration.TotalMovieStudios = calculator.CalculateTotalMovieStudios();
                PluginConfiguration.BiggestMovie = calculator.CalculateBiggestMovie();
                PluginConfiguration.LongestMovie = calculator.CalculateLongestMovie();
                PluginConfiguration.OldestMovie = calculator.CalculateOldestMovie();
                PluginConfiguration.NewestMovie = calculator.CalculateNewestMovie();
                PluginConfiguration.HighestRating = calculator.CalculateHighestRating();
                PluginConfiguration.LowestRating = calculator.CalculateLowestRating();
                PluginConfiguration.NewestAddedMovie = calculator.CalculateNewestAddedMovie();
                PluginConfiguration.HighestBitrateMovie = calculator.CalculateHighestBitrateMovie();
                PluginConfiguration.LowestBitrateMovie = calculator.CalculateLowestBitrateMovie();

                PluginConfiguration.TotalShows = calculator.CalculateTotalShows();
                PluginConfiguration.TotalShowStudios = calculator.CalculateTotalShowStudios();
                PluginConfiguration.MostWatchedShows = calculator.CalculateMostWatchedShows();
                PluginConfiguration.LeastWatchedShows = calculator.CalculateLeastWatchedShows();
                PluginConfiguration.BiggestShow = calculator.CalculateBiggestShow();
                PluginConfiguration.LongestShow = calculator.CalculateLongestShow();
                PluginConfiguration.OldestShow = calculator.CalculateOldestShow();
                PluginConfiguration.NewestShow = calculator.CalculateNewestShow();
                PluginConfiguration.NewestAddedEpisode = calculator.CalculateNewestAddedEpisode();

                PluginConfiguration.MovieQualityItems = calculator.CalculateMovieQualityList();
            }

            numComplete++;
            progress.Report(percentPerUser * numComplete);

            Plugin.Instance.SaveConfiguration();
        }

        IEnumerable<TaskTriggerInfo> IScheduledTask.GetDefaultTriggers()
        {
            throw new NotImplementedException();
        }
    }
}