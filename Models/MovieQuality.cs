using System.Collections.Generic;
using Statistics.Enum;

namespace statistics.Models
{
    public class MovieQuality
    {
        public List<Movie> Movies { get; set; }
        public VideoQuality Quality { get; set; }
    }
}
