using System.Collections.Generic;

namespace Statistics.Api
{
    public class UpdateModel
    {
        public string LastUpdateTime { get; set; }
        public List<UpdateShowModel> IdList { get; set; }

        public UpdateModel()
        {
            IdList = new List<UpdateShowModel>();
        }
    }

    public class UpdateShowModel
    {
        public string ShowId { get; set; }
        public int Count { get; set; }

        public UpdateShowModel()
        {
            
        }
        public UpdateShowModel(string showId, int count)
        {
            ShowId = showId;
            Count = count;
        }
    }
}
