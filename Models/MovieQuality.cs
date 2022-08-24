using System.Collections.Generic;

namespace statistics.Models
{
    public class MovieQuality
    {
        public List<Movie> Movies { get; set; }
        public string Title { get; set; }
    }

    public class MovieQualityObj
    {
        public int Count { get; set; }
        public List<MovieQuality> Movies { get; set; }
    }
}
