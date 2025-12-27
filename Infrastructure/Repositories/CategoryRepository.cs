using doc_bursa.Infrastructure.Data;
using doc_bursa.Models;

namespace doc_bursa.Infrastructure.Repositories
{
    public class CategoryRepository : Repository<Category>
    {
        public CategoryRepository(FinDeskDbContext context) : base(context)
        {
        }
    }
}

