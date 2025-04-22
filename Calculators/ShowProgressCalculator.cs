using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using statistics.Models.Configuration;
using statistics.Calculators;
using MediaBrowser.Model.Logging;
using System.Threading;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Controller.Providers;

namespace Statistics.Helpers
{
    public class ShowProgressCalculator : BaseCalculator
    {
        private readonly IFileSystem _fileSystem;
        private readonly IServerApplicationPaths _serverApplicationPaths;
        private readonly IProviderManager _providerManager;
        private readonly IJsonSerializer _jsonSerializer;

        public bool IsCalculationFailed = false;
        public ShowProgressCalculator(IUserManager userManager, ILibraryManager libraryManager, 
                                        IUserDataManager userDataManager, IFileSystem fileSystem, 
                                        IServerApplicationPaths serverApplicationPaths, ILogger logger, 
                                        IJsonSerializer jsonSerializer, IProviderManager providerManager,
                                        CancellationToken cancellationToken)
            : base(userManager, libraryManager, userDataManager, providerManager, logger, cancellationToken)
        {
            _fileSystem = fileSystem;
            _serverApplicationPaths = serverApplicationPaths;
            _providerManager = providerManager;
            _jsonSerializer = jsonSerializer;
         }

        public List<ShowProgress> CalculateShowProgress(CancellationToken cancellationToken)
        {
            if (User == null)
                return null;

            var showList = GetAllSeries().OrderBy(x => x.SortName);
            var showProgress = new List<ShowProgress>();

            foreach (var show in showList)
            {
                var totalSpecials = _tolalSpecialsPerSeries.TryGetValue(show.Id, out int specials) ? specials : 0;
                var collectedSpecials = _collectedSpecialsPerSeries.TryGetValue(show.Id, out int _collectedSpecials) ? _collectedSpecials : 0;
                var totalEpisodes = _totalEpisodesPerSeries.TryGetValue(show.Id, out int episodes) ? episodes : 0; 
                var collectedEpisodes = _collectedEpisodesPerSeries.TryGetValue(show.Id, out int collected) ? collected : 0;
                var seenEpisodes = GetPlayedEpisodeCount(show);

                if( collectedEpisodes > totalEpisodes && totalEpisodes > 0)
                {
                    collectedEpisodes = totalEpisodes;
                }

                if(seenEpisodes > collectedEpisodes && collectedEpisodes > 0)
                {
                    seenEpisodes = collectedEpisodes;
                }

                decimal watched = 0;
                decimal collectedPercent = 0;
                if (totalEpisodes > 0)
                {
                    collectedPercent = collectedEpisodes / (decimal)totalEpisodes * 100;
                }

                if (seenEpisodes > 0)
                {
                    watched = seenEpisodes / (decimal)collectedEpisodes * 100;
                }

                showProgress.Add(new ShowProgress
                {
                    Name = show.Name,
                    SortName = show.SortName,
                    Score = show.CommunityRating,
                    Status = show.Status,
                    StartYear = show.PremiereDate?.ToString("yyyy"),
                    TotalEpisodes = totalEpisodes,
                    CollectedEpisodes = collectedEpisodes,
                    SeenEpisodes = seenEpisodes,
                    PercentSeen = Math.Floor(Math.Min(watched, 100)),
                    TotalSpecials = totalSpecials,
                    CollectedSpecials = collectedSpecials,
                    SeenSpecials = GetPlayedSpecials(show),
                    PercentCollected = Math.Floor(Math.Min(collectedPercent, 100)),
                    Id = show.Id.ToString()
                });
            }

            return showProgress;
        }
    }
}
