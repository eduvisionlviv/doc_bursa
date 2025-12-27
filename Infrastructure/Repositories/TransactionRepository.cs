using doc_bursa.Infrastructure.Data;
using doc_bursa.Models;

namespace doc_bursa.Infrastructure.Repositories
{
    public class TransactionRepository : Repository<Transaction>
    {
        public TransactionRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}
