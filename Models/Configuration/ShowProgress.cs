using MediaBrowser.Model.Entities;

namespace statistics.Models.Configuration
{
    public class ShowProgress
    {
        public string Name { get; set; }
        public string SortName { get; set; }
        public string StartYear { get; set; }
        public decimal Watched { get; set; }
        public float? Score { get; set; }
        public SeriesStatus? Status { get; set; }
        public int TotalEpisodes { get; set; }
        public int CollectedEpisodes { get; set; }
        public int SeenEpisodes { get; set; }
        public int TotalSpecials { get; set; }
        public int CollectedSpecials { get; set; }
        public int SeenSpecials { get; set; }
        public decimal PercentSeen { get; set; }
        public decimal PercentCollected { get; set; }
        public string Id { get; set; }
    }
}
