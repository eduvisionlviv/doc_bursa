using FinDesk.Infrastructure.Data;
using FinDesk.Models;

namespace FinDesk.Infrastructure.Repositories
{
    public class CategoryRepository : Repository<Category>
    {
        public CategoryRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}

