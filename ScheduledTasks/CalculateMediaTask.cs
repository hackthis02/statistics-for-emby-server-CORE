using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Controller;
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
    public class CalculateMediaTask : IScheduledTask
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

        public CalculateMediaTask(ILogManager logger,
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
        string IScheduledTask.Name => "Calculate statistics all library media";

        string IScheduledTask.Key => "StatisticsCalculateStatsTask";

        string IScheduledTask.Description => "Task that will calculate statistics of all media in liberay. (Ideal for weekly/non-daily schedule)";

        string IScheduledTask.Category => "Statistics";

        Task IScheduledTask.Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            PluginConfiguration.UserStats = Plugin.Instance.Configuration.UserStats ?? new List<UserStat>();

            // purely for progress reporting
            var percentPerUser = 100 / 4;
            var numComplete = 0;

            PluginConfiguration.LastUpdated = DateTime.Now.ToString("g");
            PluginConfiguration.ServerId = _appHost.SystemId;

            numComplete++;
            progress.Report(percentPerUser * numComplete);


            var calculator = new Calculator(_userManager, _libraryManager, _userDataManager, _fileSystem, _logger, _providerManager, cancellationToken);
           
            using (calculator)
            {
                calculator.SetUser(null);
                PluginConfiguration.MovieQualities = calculator.CalculateMovieQualities();
                PluginConfiguration.MovieCodecs = calculator.CalculateMovieCodecs();
                PluginConfiguration.TotalUsers = calculator.CalculateTotalUsers();

                numComplete++;
                progress.Report(percentPerUser * numComplete);

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

                numComplete++;
                progress.Report(percentPerUser * numComplete);

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
            return Task.CompletedTask;
        }

        IEnumerable<TaskTriggerInfo> IScheduledTask.GetDefaultTriggers()
        {
            return new[] {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerWeekly,
                    DayOfWeek = DayOfWeek.Sunday,
                    TimeOfDayTicks = TimeSpan.FromMinutes(30).Ticks
                }
            };
        }
    }
}