using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinDesk.Infrastructure.Data
{
    public class FinDeskDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FinDeskDbContext>
    {
        public FinDeskDbContext CreateDbContext(string[] args)
        {
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FinDesk");

            Directory.CreateDirectory(basePath);
            var dbPath = Path.Combine(basePath, "findesk.db");

            var optionsBuilder = new DbContextOptionsBuilder<FinDeskDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new FinDeskDbContext(optionsBuilder.Options);
        }
    }
}
