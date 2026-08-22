using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AuthMicroservice.Repository
{
    public interface IWhatsappConfigRepository : IGenericRepository<WhatsappConfig>
    {
        Task<WhatsappConfig> GetByApplicationIdAsync(Guid applicationId);
    }

    public class WhatsappConfigRepository : GenericRepository<WhatsappConfig>, IWhatsappConfigRepository
    {
        public WhatsappConfigRepository(UserDbContext context)
            : base(context)
        {
        }

        public async Task<WhatsappConfig> GetByApplicationIdAsync(Guid applicationId)
        {
            return await _context.Set<WhatsappConfig>().FirstOrDefaultAsync(c => c.ApplicationId == applicationId);
        }
    }
}
