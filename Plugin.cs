using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using statistics.Configuration;

namespace statistics
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "Statistics",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
                    EnableInMainMenu = true
                },
                new PluginPageInfo
                {
                    Name = "StatisticsMovieList",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.moviePage.html"
                },
                new PluginPageInfo
                {
                    Name = "StatisticsMovieListText",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.movieTextPage.html"
                },
                new PluginPageInfo
                {
                    Name = "StatisticsShowOverview",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.showOverview.html"
                },
                new PluginPageInfo
                {
                    Name = "StatisticsUserBased",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.userBased.html"
                }
            };
        }

        public override Guid Id => new Guid("291d866f-baad-464a-aed6-a4a8b95a8fd7");

        public static Plugin Instance { get; private set; }

        public override string Name => "Statistics";

        public override string Description => "Get statistics from your collection";

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".Images.statistics-thumb.png");
        }

        public ImageFormat ThumbImageFormat
        {
            get
            {
                return ImageFormat.Png;
            }
        }
    }
}
