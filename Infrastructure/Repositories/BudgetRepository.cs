using FinDesk.Infrastructure.Data;
using FinDesk.Models;

namespace FinDesk.Infrastructure.Repositories
{
    public class BudgetRepository : Repository<Budget>
    {
        public BudgetRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}

