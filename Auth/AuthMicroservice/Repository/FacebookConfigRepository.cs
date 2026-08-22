using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AuthMicroservice.Repository
{
    public interface IFacebookConfigRepository : IGenericRepository<FacebookConfig>
    {
        Task<FacebookConfig> GetByApplicationIdAsync(Guid applicationId);
    }

    public class FacebookConfigRepository : GenericRepository<FacebookConfig>, IFacebookConfigRepository
    {
        public FacebookConfigRepository(UserDbContext context)
            : base(context)
        {
        }

        public async Task<FacebookConfig> GetByApplicationIdAsync(Guid applicationId)
        {
            return await _context.Set<FacebookConfig>().FirstOrDefaultAsync(c => c.ApplicationId == applicationId);
        }
    }
}
