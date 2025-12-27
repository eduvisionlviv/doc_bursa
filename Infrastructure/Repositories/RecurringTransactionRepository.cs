using doc_bursa.Infrastructure.Data;
using doc_bursa.Models;

namespace doc_bursa.Infrastructure.Repositories
{
    public class RecurringTransactionRepository : Repository<RecurringTransaction>
    {
        public RecurringTransactionRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}

