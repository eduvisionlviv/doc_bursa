using doc_bursa.Infrastructure.Data;
using doc_bursa.Models;

namespace doc_bursa.Infrastructure.Repositories
{
    public class AccountRepository : Repository<Account>
    {
        public AccountRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}
