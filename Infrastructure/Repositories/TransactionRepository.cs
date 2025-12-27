using FinDesk.Infrastructure.Data;
using FinDesk.Models;

namespace FinDesk.Infrastructure.Repositories
{
    public class TransactionRepository : Repository<Transaction>
    {
        public TransactionRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}

