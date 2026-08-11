namespace Statistics.Models
{
    public class DVProfileModel
    {
        public string DVProfile { get; set; }
        public int Movies { get; set; }
        public int Episodes { get; set; }

        public override string ToString()
        {          
            return $"<tr><td>{DVProfile}</td><td>{Movies}</td><td>{Episodes}</td></tr>";
        }
    }
}