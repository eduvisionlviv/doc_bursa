using System.IO;
using FinDesk.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;

namespace FinDesk.Infrastructure.Data
{
    /// <summary>
    /// EF Core контекст додатку FinDesk.
    /// </summary>
    public class FinDeskDbContext : DbContext
    {
        private static readonly ILoggerFactory LoggerFactoryInstance = new LoggerFactory(new[] { new SerilogLoggerProvider() });

        public FinDeskDbContext(DbContextOptions<FinDeskDbContext> options)
            : base(options)
        {
        }

        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Budget> Budgets => Set<Budget>();
        public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<AccountGroup> AccountGroups => Set<AccountGroup>();
        public DbSet<MasterGroup> MasterGroups => Set<MasterGroup>();
        public DbSet<MasterGroupAccountGroup> MasterGroupAccountGroups => Set<MasterGroupAccountGroup>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>()
                .Property(a => a.Name)
                .HasMaxLength(120);

            modelBuilder.Entity<Account>()
                .Property(a => a.AccountNumber)
                .HasMaxLength(34);

            modelBuilder.Entity<Account>()
                .Property(a => a.Institution)
                .HasMaxLength(64);

            modelBuilder.Entity<Account>()
                .Property(a => a.Currency)
                .HasMaxLength(3);

            modelBuilder.Entity<Account>()
                .HasMany(a => a.RecurringTransactions)
                .WithOne(r => r.Account)
                .HasForeignKey(r => r.AccountId);

            modelBuilder.Entity<Budget>()
                .Property(b => b.Name)
                .HasMaxLength(120);

            modelBuilder.Entity<Budget>()
                .Property(b => b.Category)
                .HasMaxLength(100);

            modelBuilder.Entity<Budget>()
                .Property(b => b.Frequency)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<RecurringTransaction>()
                .Property(r => r.Frequency)
                .HasConversion<string>()
                .HasMaxLength(20);

            modelBuilder.Entity<RecurringTransaction>()
                .Property(r => r.Description)
                .HasMaxLength(200);

            modelBuilder.Entity<RecurringTransaction>()
                .Property(r => r.Category)
                .HasMaxLength(100);

            modelBuilder.Entity<RecurringTransaction>()
                .Property(r => r.Notes)
                .HasMaxLength(500);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Category)
                .HasMaxLength(100);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Source)
                .HasMaxLength(120);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Account)
                .HasMaxLength(120);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.TransactionId)
                .HasMaxLength(128);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Description)
                .HasMaxLength(256);

            modelBuilder.Entity<Transaction>()
                .Property(t => t.Hash)
                .HasMaxLength(128);

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.TransactionId)
                .IsUnique();

            modelBuilder.Entity<Budget>()
                .Property(b => b.Description)
                .HasMaxLength(500);

            modelBuilder.Entity<MasterGroupAccountGroup>()
                .HasKey(mg => new { mg.MasterGroupId, mg.AccountGroupId });

            modelBuilder.Entity<MasterGroupAccountGroup>()
                .HasOne(mg => mg.MasterGroup)
                .WithMany(g => g.AccountGroupLinks)
                .HasForeignKey(mg => mg.MasterGroupId);

            modelBuilder.Entity<MasterGroupAccountGroup>()
                .HasOne(mg => mg.AccountGroup)
                .WithMany(g => g.MasterGroupLinks)
                .HasForeignKey(mg => mg.AccountGroupId);

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = Path.Combine(App.AppDataPath, "findesk.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
                optionsBuilder.UseLoggerFactory(LoggerFactoryInstance);
            }
        }
    }
}

