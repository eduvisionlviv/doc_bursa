using FinDesk.Infrastructure.Data;
using FinDesk.Models;

namespace FinDesk.Infrastructure.Repositories
{
    public class RecurringTransactionRepository : Repository<RecurringTransaction>
    {
        public RecurringTransactionRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}

