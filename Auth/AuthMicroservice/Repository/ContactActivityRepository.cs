using AuthMicroservice.Model;

namespace AuthMicroservice.Repository
{
    public interface IContactActivityRepository : IGenericRepository<ContactActivity>
    {
    }

    public class ContactActivityRepository : GenericRepository<ContactActivity>, IContactActivityRepository
    {
        public ContactActivityRepository(UserDbContext context)
            : base(context)
        {
        }
    }
}
