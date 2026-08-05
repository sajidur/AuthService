using AuthMicroservice.Model;

namespace AuthMicroservice.Repository
{
    public interface ICampaignRepository : IGenericRepository<Campaign>
    {
    }

    public class CampaignRepository : GenericRepository<Campaign>, ICampaignRepository
    {
        public CampaignRepository(UserDbContext context)
            : base(context)
        {
        }
    }
}
