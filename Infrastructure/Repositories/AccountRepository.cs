using FinDesk.Infrastructure.Data;
using FinDesk.Models;

namespace FinDesk.Infrastructure.Repositories
{
    public class AccountRepository : Repository<Account>
    {
        public AccountRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}

