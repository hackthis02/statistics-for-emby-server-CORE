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
using statistics.Configuration;
using statistics.Models.Configuration;
using Statistics.Helpers;
using Statistics.ViewModel;

namespace statistics.ScheduledTasks
{
    public class CalculateTVTask : IScheduledTask
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

        public CalculateTVTask(ILogManager logger,
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
        string IScheduledTask.Name => "Calculate statistics for TV Shows";

        string IScheduledTask.Key => "StatisticsCalculateTVTask";

        string IScheduledTask.Description => "Task that will calculate statistics needed display new TV shows and users watch percent.(Ideal for daily schedule)";

        string IScheduledTask.Category => "Statistics";

        async Task IScheduledTask.Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var users = _userManager.GetUserList(new UserQuery() { EnableRemoteAccess = true }).ToList();

            // No users found, so stop the task
            if (users.Count == 0)
            {
                return;
            }

            PluginConfiguration.UserStats = Plugin.Instance.Configuration.UserStats ?? new List<UserStat>();

            // purely for progress reporting
            var percentPerUser = 100 / (users.Count + 2);
            var numComplete = 0;

            PluginConfiguration.LastUpdated = DateTime.Now.ToString("g");
            PluginConfiguration.ServerId = _appHost.SystemId;

            numComplete++;
            progress.Report(percentPerUser * numComplete);

            var activeUsers = new Dictionary<string, RunTime>();
            var ShowProgresses = new ShowProgressCalculator(_userManager, _libraryManager, _userDataManager, _fileSystem, _serverApplicationPaths,
                                                            _logger, _jsonSerializer, _providerManager, cancellationToken);

            foreach (var user in users)
            {
                await Task.Run(() =>
                {
                    using (ShowProgresses)
                    {
                        ShowProgresses.SetUser(user);
                        var stat = new UserStat
                        {
                            UserName = user.Name,
                            ShowProgresses = ShowProgresses.CalculateShowProgress(cancellationToken)
                        };
                        var existingUserStat = PluginConfiguration.UserStats.Find(x => x.UserName == user.Name);
                        if (existingUserStat != null)
                        {
                            existingUserStat.ShowProgresses = stat.ShowProgresses;
                        }
                        else
                        {
                            PluginConfiguration.UserStats.Add(stat);
                        }
                    }
                }, cancellationToken);

                numComplete++;
                progress.Report(percentPerUser * numComplete);
            }

            numComplete++;
            progress.Report(percentPerUser * numComplete);

            Plugin.Instance.SaveConfiguration();
        }

        IEnumerable<TaskTriggerInfo> IScheduledTask.GetDefaultTriggers()
        {
            return new[] {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromMinutes(15).Ticks
                }
            };
        }
    }
}
