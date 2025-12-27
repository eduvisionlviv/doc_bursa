using System;
using System.Collections.Generic;
using System.IO;
using FinDesk;
using FinDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace FinDesk.Infrastructure.Data
{
    public class FinDeskDbContext : DbContext
    {
        public FinDeskDbContext(DbContextOptions<FinDeskDbContext> options) : base(options)
        {
        }

        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Budget> Budgets => Set<Budget>();
        public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
            {
                return;
            }

            var basePath = !string.IsNullOrWhiteSpace(App.AppDataPath)
                ? App.AppDataPath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinDesk");

            Directory.CreateDirectory(basePath);
            var dbPath = Path.Combine(basePath, "findesk.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureTransaction(modelBuilder);
            ConfigureAccount(modelBuilder);
            ConfigureCategory(modelBuilder);
            ConfigureBudget(modelBuilder);
            ConfigureRecurringTransaction(modelBuilder);

            modelBuilder.Entity<Category>().HasData(GetDefaultCategories());
        }

        private static void ConfigureTransaction(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.Property(e => e.TransactionId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Date)
                    .IsRequired();

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Description)
                    .HasMaxLength(500);

                entity.Property(e => e.Category)
                    .HasMaxLength(200);

                entity.Property(e => e.Source)
                    .HasMaxLength(200);

                entity.Property(e => e.Hash)
                    .HasMaxLength(256);

                entity.HasIndex(e => e.TransactionId)
                    .IsUnique();

                entity.HasIndex(e => e.Hash);
            });
        }

        private static void ConfigureAccount(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>(entity =>
            {
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Source)
                    .HasMaxLength(200);

                entity.Property(e => e.Balance)
                    .HasColumnType("decimal(18,2)");
            });
        }

        private static void ConfigureCategory(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(18,2)");

                entity.HasIndex(e => e.Name)
                    .IsUnique();
            });
        }

        private static void ConfigureBudget(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Budget>(entity =>
            {
                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Category)
                    .HasMaxLength(100);

                entity.Property(e => e.Limit)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Spent)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Period)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.Description)
                    .HasMaxLength(500);
            });
        }

        private static void ConfigureRecurringTransaction(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RecurringTransaction>(entity =>
            {
                entity.Property(e => e.Description)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Category)
                    .HasMaxLength(100);

                entity.Property(e => e.Account)
                    .HasMaxLength(100);

                entity.Property(e => e.Frequency)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.Notes)
                    .HasMaxLength(500);
            });
        }

        private static IEnumerable<Category> GetDefaultCategories()
        {
            return new[]
            {
                new Category { Id = 1, Name = "Продукти" },
                new Category { Id = 2, Name = "Транспорт" },
                new Category { Id = 3, Name = "Ресторани" },
                new Category { Id = 4, Name = "Здоров'я" },
                new Category { Id = 5, Name = "Розваги" },
                new Category { Id = 6, Name = "Комунальні" },
                new Category { Id = 7, Name = "Житло" },
                new Category { Id = 8, Name = "Зарплата" },
                new Category { Id = 9, Name = "Подарунки" },
                new Category { Id = 10, Name = "Подорожі" },
                new Category { Id = 11, Name = "Одяг" },
                new Category { Id = 12, Name = "Освіта" },
                new Category { Id = 13, Name = "Діти" },
                new Category { Id = 14, Name = "Техніка" },
                new Category { Id = 15, Name = "Інше" }
            };
        }
    }
}
