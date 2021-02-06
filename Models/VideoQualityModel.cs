using Statistics.Enum;

namespace Statistics.Models
{
    public class VideoQualityModel
    {
        public VideoQuality Quality { get; set; }
        public int Movies { get; set; }
        public int Episodes { get; set; }

        public override string ToString()
        {
            string quality;
            switch (Quality)
            {
                case VideoQuality.DVD:
                    quality = "< 480P";
                    break;
                case VideoQuality.Q700:
                    quality = "480P";
                    break;
                case VideoQuality.Q1260:
                    quality = "720P";
                    break;
                case VideoQuality.Q1900:
                    quality = "1080P";
                    break;
                case VideoQuality.Q2500:
                    quality = "1440P";
                    break;
                case VideoQuality.Q3800:
                    quality = "4K";
                    break;
                default:
                    quality = "Unknown";
                    break;
            }

            return $"<tr><td>{quality}</td><td>{Movies}</td><td>{Episodes}</td></tr>";
        }
    }
}
