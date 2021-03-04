using Statistics.Enum;

namespace Statistics.Models
{
    public class VideoCodecModel
    {
        public string Codec { get; set; }
        public int Movies { get; set; }
        public int Episodes { get; set; }

        public override string ToString()
        {          
            return $"<tr><td>{Codec}</td><td>{Movies}</td><td>{Episodes}</td></tr>";
        }
    }
}