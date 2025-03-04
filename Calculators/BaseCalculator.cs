using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;

namespace statistics.Calculators
{
    public abstract class BaseCalculator : IDisposable
    {
        private IEnumerable<Movie> _movieCache;
        private IEnumerable<Series> _seriesCache;
        private IEnumerable<Episode> _episodeCache;
        private IEnumerable<BoxSet> _boxsetCache;

        private IEnumerable<Movie> _viewedMovieCache;
        private IEnumerable<Episode> _ownedEpisodeCache;
        private IEnumerable<Episode> _viewedEpisodeCache;

        protected readonly Dictionary<Guid, int> _totalEpisodesPerSeries = new Dictionary<Guid, int>();
        protected readonly Dictionary<Guid, int> _collectedEpisodesPerSeries = new Dictionary<Guid, int>();
        protected readonly Dictionary<Guid, int> _tolalSpecialsPerSeries = new Dictionary<Guid, int>();
        protected readonly Dictionary<Guid, int> _collectedSpecialsPerSeries = new Dictionary<Guid, int>();

        protected readonly IUserManager UserManager;
        protected readonly ILibraryManager LibraryManager;
        protected readonly IUserDataManager UserDataManager;
        protected readonly IProviderManager ProviderManager;
        protected readonly ILogger _logger;
        protected User User;


        protected BaseCalculator(IUserManager userManager, ILibraryManager libraryManager,
            IUserDataManager userDataManager, IProviderManager providerManager, ILogger Logger, CancellationToken cancellationToken)
        {
            UserManager = userManager;
            LibraryManager = libraryManager;
            UserDataManager = userDataManager;
            ProviderManager = providerManager;
            _logger = Logger;

            foreach (var series in GetAllSeries())
            {
                if (!_collectedEpisodesPerSeries.ContainsKey(series.Id))
                {
                    _collectedEpisodesPerSeries.Add(series.Id, GetOwnedEpisodesCount(series));
                }
                else
                {
                    _collectedEpisodesPerSeries[series.Id] = (_collectedEpisodesPerSeries.TryGetValue(series.Id, out int collected) ? collected : 0) + GetOwnedEpisodesCount(series);
                }

                if (!_collectedSpecialsPerSeries.ContainsKey(series.Id))
                {
                    _collectedSpecialsPerSeries.Add(series.Id, GetOwnedSpecials(series));
                }
                else
                {
                    _collectedSpecialsPerSeries[series.Id] = (_collectedSpecialsPerSeries.TryGetValue(series.Id, out int collected) ? collected : 0) + GetOwnedSpecials(series);
                }

                if (!_totalEpisodesPerSeries.ContainsKey(series.Id))
                {
                    _totalEpisodesPerSeries.Add(series.Id, GetAllEpisodesCount(series, cancellationToken).Result);
                }
                else
                {
                    _totalEpisodesPerSeries[series.Id] = (_totalEpisodesPerSeries.TryGetValue(series.Id, out int collected) ? collected : 0) + GetAllEpisodesCount(series, cancellationToken).Result;
                }

                if (!_tolalSpecialsPerSeries.ContainsKey(series.Id))
                {
                    _tolalSpecialsPerSeries.Add(series.Id, GetAllSpeacialsCount(series, cancellationToken).Result);
                }
                else
                {
                    _tolalSpecialsPerSeries[series.Id] = (_tolalSpecialsPerSeries.TryGetValue(series.Id, out int collected) ? collected : 0) + GetAllSpeacialsCount(series, cancellationToken).Result;
                }
            }
        }

        #region Helpers

        protected IEnumerable<Movie> GetAllMovies()
        {
            return _movieCache ?? (_movieCache = GetItems<Movie>());
        }

        protected IEnumerable<Series> GetAllSeries()
        {
            return _seriesCache ?? (_seriesCache = GetItems<Series>());
        }

        protected IEnumerable<Episode> GetAllEpisodes()
        {
            return _episodeCache ?? (_episodeCache = GetItems<Episode>());
        }

        protected IEnumerable<Episode> GetAllOwnedEpisodes()
        {
            return _ownedEpisodeCache ?? (_ownedEpisodeCache = GetOwnedItems<Episode>());
        }

        protected IEnumerable<Episode> GetAllViewedEpisodesByUser()
        {
            return _viewedEpisodeCache ?? (_viewedEpisodeCache = GetOwnedItems<Episode>(true));
        }

        protected IEnumerable<Movie> GetAllViewedMoviesByUser()
        {
            return _viewedMovieCache ?? (_viewedMovieCache = GetOwnedItems<Movie>(true));
        }

        protected List<BaseItem> GetAllBaseItems()
        {
            return GetAllMovies().Union(GetAllEpisodes().Cast<BaseItem>()).ToList();
        }

        protected IEnumerable<User> GetAllUser()
        {
            return UserManager.GetUserList(new UserQuery() { HasConnectUserId = false })
                .Union(UserManager.GetUserList(new UserQuery() { HasConnectUserId = true }))
                .Union(UserManager.GetUserList(new UserQuery() { HasConnectUserId = null })).ToList();
        }

        protected int GetOwnedCount(Type type)
        {
            var query = new InternalItemsQuery(User)
            {
                IncludeItemTypes = new[] { type.Name },
                Recursive = true,
                IsVirtualItem = false
            };

            return LibraryManager.GetItemList(query).Count();
        }

        protected IEnumerable<BoxSet> GetBoxsets()
        {
            return _boxsetCache ?? (_boxsetCache = GetItems<BoxSet>());
        }


        #region Queries
        protected int GetOwnedEpisodesCount(Series show)
        {
            var query = new InternalItemsQuery(User)
            {
                IncludeItemTypes = new[] { typeof(Episode).Name },
                Recursive = true,
                Parent = show,
                IsSpecialSeason = false,
                IsVirtualItem = false,
            };

            var episodes = LibraryManager.GetItemList(query).OfType<Episode>().Where(e => ((e.SortParentIndexNumber ?? e.ParentIndexNumber) != 0) && (e.PremiereDate <= DateTime.Now || e.PremiereDate != null));
            return episodes.Sum(r => (r.IndexNumberEnd == null || r.IndexNumberEnd < r.IndexNumber ? r.IndexNumber : r.IndexNumberEnd) - r.IndexNumber + 1) ?? 0;
        }

        protected async Task<int> GetAllEpisodesCount(Series show, CancellationToken cancellationToken)
        {
            var libraryOptions = LibraryManager.GetLibraryOptions(show);
            var allEpisodes = await ProviderManager.GetAllEpisodes(show, libraryOptions, cancellationToken).ConfigureAwait(false);
            return allEpisodes.Where(e => ((e.SortParentIndexNumber ?? e.ParentIndexNumber) != 0) && e.PremiereDate <= DateTime.Now).Count();
        }

        protected async Task<int> GetAllSpeacialsCount(Series show, CancellationToken cancellationToken)
        {
            var libraryOptions = LibraryManager.GetLibraryOptions(show);
            var allEpisodes = await ProviderManager.GetAllEpisodes(show, libraryOptions, cancellationToken).ConfigureAwait(false);
            return allEpisodes.Where(e => (e.SortParentIndexNumber ?? e.ParentIndexNumber) == 0).Count();
        }

        protected int GetPlayedEpisodeCount(Series show)
        {
            var query = new InternalItemsQuery(User)
            {
                IncludeItemTypes = new[] { typeof(Episode).Name },
                Recursive = true,
                Parent = show,
                IsSpecialSeason = false,
                MaxPremiereDate = DateTime.Now,
                IsVirtualItem = false,
                IsPlayed = true
            };

            var episodes = LibraryManager.GetItemList(query).OfType<Episode>().Where(e => (e.SortParentIndexNumber ?? e.ParentIndexNumber) != 0);
            return episodes.Sum(r => (r.IndexNumberEnd ?? r.IndexNumber) - r.IndexNumber + 1) ?? 0;
        }

        protected int GetOwnedSpecials(Series show)
        {
            var query = new InternalItemsQuery(User)
            {
                IncludeItemTypes = new[] { typeof(Season).Name },
                Recursive = true,
                Parent = show,
                IsSpecialSeason = true,
                MaxPremiereDate = DateTime.Now,
                IsVirtualItem = false
            };

            var seasons = LibraryManager.GetItemList(query).OfType<Season>();
            return seasons.Sum(x => x.GetChildren(User).OfType<Episode>().Sum(r => (r.IndexNumberEnd ?? r.IndexNumber) - r.IndexNumber + 1) ?? 0);
        }

        protected int GetPlayedSpecials(Series show)
        {
            var query = new InternalItemsQuery(User)
            {
                IncludeItemTypes = new[] { typeof(Season).Name },
                Recursive = true,
                Parent = show,
                IsSpecialSeason = true,
                MaxPremiereDate = DateTime.Now,
                IsVirtualItem = false,
            };

            var seasons = LibraryManager.GetItemList(query).OfType<Season>();
            return seasons.Sum(x => x.GetChildren(User).Count(e => e.IsPlayed(User)));
        }

        #endregion

        private IEnumerable<T> GetItems<T>()
        {
            var query = new InternalItemsQuery(User)
            {
                IncludeItemTypes = new[] { typeof(T).Name },
                Recursive = true,
                IsVirtualItem = false,
                DtoOptions = new DtoOptions(true)
                {
                    EnableImages = false
                }
            };

            return LibraryManager.GetItemList(query).OfType<T>();
        }

        private IEnumerable<T> GetOwnedItems<T>(bool? isPLayed = null)
        {
            var query = new InternalItemsQuery(User)
            {
                IncludeItemTypes = new[] { typeof(T).Name },
                IsPlayed = isPLayed,
                Recursive = true,
                IsVirtualItem = false,
                DtoOptions = new DtoOptions(true)
                {
                    EnableImages = false
                }
            };

            return LibraryManager.GetItemsResult(query).Items.OfType<T>().ToList();
        }

        #endregion

        public void Dispose()
        {
            ClearCache();
        }

        public void SetUser(User user)
        {
            User = user;
        }

        public void ClearCache()
        {
            try
            {
                User = null;
                _episodeCache = null;
                _movieCache = null;
                _boxsetCache = null;
                _ownedEpisodeCache = null;
                _viewedEpisodeCache = null;
                _viewedMovieCache = null;
                _seriesCache = null;
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }
        }
    }
}