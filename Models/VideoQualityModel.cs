namespace Statistics.Models
{
    public class VideoQualityModel
    {
        public string Quality { get; set; }
        public int Movies { get; set; }
        public int Episodes { get; set; }

        public override string ToString()
        {
            return $"<tr><td>{Quality}</td><td>{Movies}</td><td>{Episodes}</td></tr>";
        }
    }
}
