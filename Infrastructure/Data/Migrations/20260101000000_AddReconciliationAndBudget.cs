using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace doc_bursa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationAndBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Додавання нових полів до Transaction
            migrationBuilder.AddColumn<string>(
                name: "TransferId",
                table: "Transactions",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Transactions",
                type: "TEXT",
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<decimal>(
                name: "TransferCommission",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            // Видалення старого поля TransferStatus (якщо існує)
            // migrationBuilder.DropColumn(
            //     name: "TransferStatus",
            //     table: "Transactions");

            // Створення таблиці ReconciliationRules
            migrationBuilder.CreateTable(
                name: "ReconciliationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SourceAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    CounterpartyPattern = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    AccountNumberPattern = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MaxDaysDifference = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 2),
                    MaxCommissionPercent = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 2.0m),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CommissionCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: "Банківська комісія")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationRules", x => x.Id);
                });

            // Створення таблиці PlannedTransactions
            migrationBuilder.CreateTable(
                name: "PlannedTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PlannedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Pending"),
                    ActualTransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    RecurringTransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsRecurring = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedTransactions", x => x.Id);
                });

            // Індекси
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransferId",
                table: "Transactions",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedTransactions_PlannedDate",
                table: "PlannedTransactions",
                column: "PlannedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedTransactions_AccountId",
                table: "PlannedTransactions",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationRules");

            migrationBuilder.DropTable(
                name: "PlannedTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransferId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransferId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransferCommission",
                table: "Transactions");
        }
    }
}
