using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using statistics.Models.Configuration;
using Statistics.Api;
using statistics.Calculators;

namespace Statistics.Helpers
{
    public class ShowProgressCalculator : BaseCalculator
    {
        private readonly IZipClient _zipClient;
        private readonly IHttpClient _httpClient;
        private readonly IFileSystem _fileSystem;
        private readonly IServerApplicationPaths _serverApplicationPaths;

        public bool IsCalculationFailed = false;
        public ShowProgressCalculator(IUserManager userManager, ILibraryManager libraryManager, IUserDataManager userDataManager, IZipClient zipClient, IHttpClient httpClient, IFileSystem fileSystem, IServerApplicationPaths serverApplicationPaths, User user = null)
            : base(userManager, libraryManager, userDataManager)
        {
            _zipClient = zipClient;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _serverApplicationPaths = serverApplicationPaths;
            User = user;
        }

        public string GetServerTime(CancellationToken cancellationToken)
        {
            try
            {
                var provider = new TheTvDbProvider(_zipClient, _httpClient, _fileSystem, _serverApplicationPaths);
                return provider.GetServerTime(cancellationToken).Result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public List<UpdateShowModel> CalculateTotalEpisodes(IEnumerable<string> showIds, CancellationToken cancellationToken)
        {
            var result = new List<UpdateShowModel>();
            var provider = new TheTvDbProvider(_zipClient, _httpClient, _fileSystem, _serverApplicationPaths);

            try
            {
                foreach (var showId in showIds)
                {
                    var total = provider.CalculateEpisodeCount(showId, "en", cancellationToken).Result;
                    result.Add(new UpdateShowModel(showId, total));
                }
            }
            catch (Exception)
            {
                IsCalculationFailed = true;
            }
            
            return result;
        }

        public IEnumerable<string> GetShowsToUpdate(IEnumerable<string> showIds, string time, CancellationToken cancellationToken)
        {
            try
            {
                var provider = new TheTvDbProvider(_zipClient, _httpClient, _fileSystem, _serverApplicationPaths);
                return provider.GetSeriesIdsToUpdate(showIds, time, cancellationToken).Result;
            }
            catch (Exception)
            {
                IsCalculationFailed = true;
                return null;
            }
        }

        public List<ShowProgress> CalculateShowProgress(UpdateModel tvdbData)
        {
            if (User == null)
                return null;

            var showList = GetAllSeries().OrderBy(x => x.SortName);
            var showProgress = new List<ShowProgress>();
            
            foreach (var show in showList)
            {
                var totalEpisodes = tvdbData.IdList.FirstOrDefault(x => x.ShowId == show.GetProviderId(MetadataProviders.Tvdb))?.Count ?? 0;

                var collectedEpisodes = GetOwnedEpisodesCount(show);
                var seenEpisodes = GetPlayedEpisodeCount(show);

                var totalSpecials = GetOwnedSpecials(show);
                var seenSpecials = GetPlayedSpecials(show);

                if (collectedEpisodes > totalEpisodes)
                    totalEpisodes = collectedEpisodes;

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

            return showProgress;
        }
    }
}
