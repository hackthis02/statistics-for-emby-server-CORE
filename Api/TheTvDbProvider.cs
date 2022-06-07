using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace Statistics.Api
{
    public class TVDBEpisode
    {
        public int id { get; set; }
        public int seriesId { get; set; }
        public string name { get; set; }
        public int runtime { get; set; }
        public string overview { get; set; }
        public string image { get; set; }
        public int imageType { get; set; }
        public int isMovie { get; set; }
        public int number { get; set; }
        public int seasonNumber { get; set; }
        public string lastUpdated { get; set; }
        public string aired { get; set; }
        public string finaleType { get; set; }
        public int? airsAfterSeason { get; set; }
        public int? airsBeforeSeason { get; set; }
        public int? airsBeforeEpisode { get; set; }
    }

    public class TVDBjson
    {
        public List<object> characters { get; set; }
        public List<TVDBEpisode> episodes { get; set; }
    }

    public class TheTvDbProvider
    {
        private readonly IFileSystem _fileSystem;
        private readonly IServerApplicationPaths _serverApplicationPaths;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonserializer;
        public TheTvDbProvider(IFileSystem fileSystem, IServerApplicationPaths serverApplicationPaths, ILogger logger, IJsonSerializer jsonserializer)
        {
            _fileSystem = fileSystem;
            _serverApplicationPaths = serverApplicationPaths;
            _logger = logger;
            _jsonserializer = jsonserializer;
        }


        public async Task<int> CalculateEpisodeCount(string seriesId, CancellationToken cancellationToken)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var downloadLangaugeXmlFile = Path.Combine(_serverApplicationPaths.CachePath, "tvdb", seriesId, "episodes-official.json");
                    _logger.Debug(downloadLangaugeXmlFile);

                    return ExtractEpisodes(downloadLangaugeXmlFile);
                }, cancellationToken);
            }
            catch (Exception x)
            {
                _logger.ErrorException(x.Message, x);
                return 0;
            }
        }

        private int ExtractEpisodes(string jsonFile)
        {
            TVDBjson temp = _jsonserializer.DeserializeFromFile<TVDBjson>(jsonFile);
            var count = 0;

            foreach (var e in temp.episodes)
            {
                _logger.Debug(e.name);
                if ((e.aired != null) && (DateTime.Now.Date >= StringToDateTime(e.aired) && e.seasonNumber != 0))
                        count++;
            }

            return count;
        }

        private DateTime StringToDateTime(string date)
        {
            if (date.Length == 10)
            {
                var year = int.Parse(date.Substring(0, 4));
                var month = int.Parse(date.Substring(5, 2));
                var day = int.Parse(date.Substring(8, 2));
                return new DateTime(year, month, day);
            }

            return DateTime.MaxValue;
        }

        private string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return language;

            return language.Split('-')[0].ToLower();
        }
    }
}