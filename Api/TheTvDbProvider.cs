using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Model.IO;

namespace Statistics.Api
{
    public class TheTvDbProvider
    {
        private const string SeriesGetZip = "https://www.thetvdb.com/api/{0}/series/{1}/all/{2}.zip";
        private const string ServerTimeUrl = "https://thetvdb.com/api/Updates.php?type=none";
        private const string ApiKey = "82a72ddf3d6a98d04c6b9a6ba94d84de";
        private readonly IFileSystem _fileSystem;
        private readonly IHttpClient _httpClient;
        private readonly IServerApplicationPaths _serverApplicationPaths;
        private readonly IZipClient _zipClient;

        public TheTvDbProvider(IZipClient zipClient, IHttpClient httpClient, IFileSystem fileSystem, IServerApplicationPaths serverApplicationPaths)
        {
            _zipClient = zipClient;
            _httpClient = httpClient;
            _fileSystem = fileSystem;
            _serverApplicationPaths = serverApplicationPaths;
        }

        public async Task<string> GetServerTime(CancellationToken cancellationToken)
        {
            using (var stream = await _httpClient.Get(new HttpRequestOptions
            {
                Url = ServerTimeUrl,
                CancellationToken = cancellationToken,
                EnableHttpCompression = true
            }).ConfigureAwait(false))
            {
                return GetUpdateTime(stream);
            }
        }

        public async Task<int> CalculateEpisodeCount(string seriesId, string preferredMetadataLanguage, CancellationToken cancellationToken)
        {
            var fullPath = _serverApplicationPaths.PluginsPath + "\\\\Statistics";

            _fileSystem.CreateDirectory(fullPath);

            var url = string.Format(SeriesGetZip, ApiKey, seriesId, NormalizeLanguage(preferredMetadataLanguage));

            using (var zipStream = await _httpClient.Get(new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken
            }).ConfigureAwait(false))
            {
                DeleteXmlFiles(fullPath);

                using (var ms = new MemoryStream())
                {
                    await zipStream.CopyToAsync(ms).ConfigureAwait(false);

                    ms.Position = 0;
                    _zipClient.ExtractAllFromZip(ms, fullPath, true);
                }
            }

            var downloadLangaugeXmlFile = Path.Combine(fullPath, NormalizeLanguage(preferredMetadataLanguage) + ".xml");

            var result = ExtractEpisodes(downloadLangaugeXmlFile);
            _fileSystem.DeleteDirectory(fullPath, true);
            return result;
        }

        private int ExtractEpisodes(string xmlFile)
        {
            using (
                var element = _fileSystem.GetFileStream(xmlFile, FileOpenMode.Open, FileAccessMode.Read, FileShareMode.Read))
            {
                var el = XElement.Load(element);

                return el.Descendants("Episode")
                    .Where(
                        x =>
                            DateTime.Now.Date >=
                            Convert.ToDateTime(StringToDateTime(x.Descendants("FirstAired").FirstOrDefault()?.Value)))
                    .Sum(x => x.Descendants("SeasonNumber").Count(y => y.Value != "0"));
            }
        }

        private string GetUpdateTime(Stream response)
        {
            using (var streamReader = new StreamReader(response, Encoding.UTF8))
            {
                return XElement
                    .Load(streamReader)
                    .Descendants("Time").FirstOrDefault()?.Value;
            }
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

        private void DeleteXmlFiles(string path)
        {
            try
            {
                foreach (var file in _fileSystem.GetFilePaths(path, true)
                    .ToList())
                    _fileSystem.DeleteFile(file);
            }
            catch (IOException)
            {
            }
        }
    }
}