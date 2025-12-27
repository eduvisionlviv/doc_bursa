using doc_bursa.Infrastructure.Data;
using doc_bursa.Models;

namespace doc_bursa.Infrastructure.Repositories
{
    public class BudgetRepository : Repository<Budget>
    {
        public BudgetRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}

