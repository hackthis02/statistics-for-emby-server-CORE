using System.Collections.Generic;

namespace statistics.Models
{
    public class Movie
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public int? Year { get; set; }
    }

    public class MovieGroup
    {
        public List<Movie> Movies { get; set; }
        public string Title { get; set; }
    }

    public class MovieCollection
    {
        public int Count { get; set; }
        public List<MovieGroup> Movies { get; set; }
    }
}
