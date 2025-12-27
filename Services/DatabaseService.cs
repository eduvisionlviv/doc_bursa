using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinDesk.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace FinDesk.Services
{
    /// <summary>
    /// Робота з локальною SQLite базою даних.
    /// </summary>
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public DatabaseService()
        {
            var dbPath = Path.Combine(App.AppDataPath, "findesk.db");
            _connectionString = $"Data Source={dbPath}";
            _logger = Log.ForContext<DatabaseService>();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Transactions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TransactionId TEXT UNIQUE NOT NULL,
                    Date TEXT NOT NULL,
                    Amount REAL NOT NULL,
                    Description TEXT,
                    Category TEXT,
                    Source TEXT,
                    Account TEXT,
                    Balance REAL,
                    Hash TEXT UNIQUE
                );

                CREATE TABLE IF NOT EXISTS DataSources (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    ApiToken TEXT,
                    ClientId TEXT,
                    ClientSecret TEXT,
                    IsEnabled INTEGER,
                    LastSync TEXT
                );

                CREATE TABLE IF NOT EXISTS CategoryRules (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Pattern TEXT NOT NULL,
                    Category TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS MasterGroups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    CreatedDate TEXT NOT NULL,
                    IsActive INTEGER DEFAULT 1,
                    Color TEXT,
                    AccountNumbers TEXT
                );
            ";
            command.ExecuteNonQuery();

            EnsureTransactionColumns(connection);
        }

        private static void EnsureTransactionColumns(SqliteConnection connection)
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA table_info(Transactions)";
            using (var reader = pragma.ExecuteReader())
            {
                while (reader.Read())
                {
                    existingColumns.Add(reader.GetString(1));
                }
            }

            void AddColumnIfMissing(string columnName, string definition)
            {
                if (existingColumns.Contains(columnName))
                {
                    return;
                }

                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = $"ALTER TABLE Transactions ADD COLUMN {columnName} {definition}";
                alterCommand.ExecuteNonQuery();
            }

            AddColumnIfMissing("Account", "TEXT");
            AddColumnIfMissing("Balance", "REAL");
        }

        // Transactions
        /// <summary>
        /// Зберегти транзакцію в базі даних (insert or replace).
        /// </summary>
        public void SaveTransaction(Transaction transaction)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Transactions 
                (TransactionId, Date, Amount, Description, Category, Source, Account, Balance, Hash)
                VALUES ($tid, $date, $amount, $desc, $cat, $src, $account, $balance, $hash)
            ";

            command.Parameters.AddWithValue("$tid", transaction.TransactionId);
            command.Parameters.AddWithValue("$date", transaction.Date.ToString("o"));
            command.Parameters.AddWithValue("$amount", transaction.Amount);
            command.Parameters.AddWithValue("$desc", transaction.Description ?? string.Empty);
            command.Parameters.AddWithValue("$cat", transaction.Category ?? "Інше");
            command.Parameters.AddWithValue("$src", transaction.Source ?? string.Empty);
            command.Parameters.AddWithValue("$account", transaction.Account ?? string.Empty);
            command.Parameters.AddWithValue("$balance", transaction.Balance);
            command.Parameters.AddWithValue("$hash", transaction.Hash ?? string.Empty);
            command.ExecuteNonQuery();

            _logger.Information("Transaction saved: {TransactionId}", transaction.TransactionId);
        }

        /// <summary>
        /// Додати транзакцію з обробкою помилок.
        /// </summary>
        public bool AddTransaction(Transaction transaction)
        {
            try
            {
                SaveTransaction(transaction);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to add transaction {TransactionId}", transaction.TransactionId);
                return false;
            }
        }

        /// <summary>
        /// Отримати транзакції з можливими фільтрами по датах, категорії та рахунку.
        /// </summary>
        public List<Transaction> GetTransactions(DateTime? from = null, DateTime? to = null, string? category = null, string? account = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var conditions = new List<string>();
            var command = connection.CreateCommand();

            if (from.HasValue)
            {
                conditions.Add("Date >= $from");
                command.Parameters.AddWithValue("$from", from.Value.ToString("o"));
            }

            if (to.HasValue)
            {
                conditions.Add("Date <= $to");
                command.Parameters.AddWithValue("$to", to.Value.ToString("o"));
            }

            if (!string.IsNullOrEmpty(category))
            {
                conditions.Add("Category = $category");
                command.Parameters.AddWithValue("$category", category);
            }

            if (!string.IsNullOrEmpty(account))
            {
                conditions.Add("Account = $account");
                command.Parameters.AddWithValue("$account", account);
            }

            var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
            command.CommandText = $"SELECT Id, TransactionId, Date, Amount, Description, Category, Source, Account, Balance, Hash FROM Transactions {whereClause} ORDER BY Date DESC";

            var transactions = new List<Transaction>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var transaction = new Transaction
                {
                    Id = reader.GetInt32(0),
                    TransactionId = reader.GetString(1),
                    Date = DateTime.Parse(reader.GetString(2)),
                    Amount = (decimal)reader.GetDouble(3),
                    Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Category = reader.IsDBNull(5) ? "Інше" : reader.GetString(5),
                    Source = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Account = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Balance = reader.IsDBNull(8) ? 0 : (decimal)reader.GetDouble(8),
                    Hash = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                };

                transactions.Add(transaction);
            }

            return transactions;
        }

        /// <summary>
        /// Отримати транзакції для конкретного рахунку.
        /// </summary>
        public List<Transaction> GetTransactionsByAccount(string account)
        {
            return GetTransactions(account: account);
        }

        /// <summary>
        /// Оновити категорію транзакції за ідентифікатором.
        /// </summary>
        public void UpdateTransactionCategory(int id, string category)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Transactions SET Category = $cat WHERE Id = $id";
            command.Parameters.AddWithValue("$cat", category);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Видалити транзакцію.
        /// </summary>
        public void DeleteTransaction(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Transactions WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Отримати унікальні назви рахунків з транзакцій.
        /// </summary>
        public List<string> GetUniqueAccounts()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT DISTINCT Account FROM Transactions WHERE Account IS NOT NULL AND Account <> '' ORDER BY Account";

            var accounts = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                accounts.Add(reader.GetString(0));
            }

            return accounts;
        }

        // Data Sources
        /// <summary>
        /// Додати нове джерело даних.
        /// </summary>
        public void AddDataSource(DataSource source)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO DataSources 
                (Name, Type, ApiToken, ClientId, ClientSecret, IsEnabled, LastSync)
                VALUES ($name, $type, $token, $cid, $secret, $enabled, $sync)
            ";

            command.Parameters.AddWithValue("$name", source.Name);
            command.Parameters.AddWithValue("$type", source.Type);
            command.Parameters.AddWithValue("$token", source.ApiToken ?? string.Empty);
            command.Parameters.AddWithValue("$cid", source.ClientId ?? string.Empty);
            command.Parameters.AddWithValue("$secret", source.ClientSecret ?? string.Empty);
            command.Parameters.AddWithValue("$enabled", source.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$sync", source.LastSync?.ToString("o") ?? string.Empty);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Оновити існуюче джерело даних.
        /// </summary>
        public void UpdateDataSource(DataSource source)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE DataSources 
                SET Type = $type, ApiToken = $token, ClientId = $cid, 
                    ClientSecret = $secret, IsEnabled = $enabled, LastSync = $sync
                WHERE Id = $id
            ";

            command.Parameters.AddWithValue("$type", source.Type);
            command.Parameters.AddWithValue("$token", source.ApiToken ?? string.Empty);
            command.Parameters.AddWithValue("$cid", source.ClientId ?? string.Empty);
            command.Parameters.AddWithValue("$secret", source.ClientSecret ?? string.Empty);
            command.Parameters.AddWithValue("$enabled", source.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$sync", source.LastSync?.ToString("o") ?? string.Empty);
            command.Parameters.AddWithValue("$id", source.Id);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Видалити джерело даних.
        /// </summary>
        public void DeleteDataSource(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM DataSources WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Отримати всі джерела даних.
        /// </summary>
        public List<DataSource> GetDataSources()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM DataSources";

            var sources = new List<DataSource>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                sources.Add(new DataSource
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2),
                    ApiToken = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ClientId = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    ClientSecret = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    IsEnabled = reader.GetInt32(6) == 1,
                    LastSync = string.IsNullOrEmpty(reader.GetString(7)) ? null : DateTime.Parse(reader.GetString(7))
                });
            }

            return sources;
        }

        // Category Rules
        /// <summary>
        /// Додати правило категоризації.
        /// </summary>
        public void SaveCategoryRule(string pattern, string category)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO CategoryRules (Pattern, Category) VALUES ($pattern, $cat)";
            command.Parameters.AddWithValue("$pattern", pattern);
            command.Parameters.AddWithValue("$cat", category);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Отримати всі правила категоризації.
        /// </summary>
        public Dictionary<string, string> GetCategoryRules()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Pattern, Category FROM CategoryRules";

            var rules = new Dictionary<string, string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rules[reader.GetString(0)] = reader.GetString(1);
            }

            return rules;
        }

        // MasterGroup CRUD operations
        /// <summary>
        /// Зберегти або оновити майстер-групу.
        /// </summary>
        public void SaveMasterGroup(MasterGroup group)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            if (group.Id == 0)
            {
                command.CommandText = @"
                    INSERT INTO MasterGroups (Name, Description, CreatedDate, IsActive, Color, AccountNumbers)
                    VALUES ($name, $description, $createdDate, $isActive, $color, $accountNumbers)";
            }
            else
            {
                command.CommandText = @"
                    UPDATE MasterGroups 
                    SET Name = $name, Description = $description, IsActive = $isActive, 
                        Color = $color, AccountNumbers = $accountNumbers
                    WHERE Id = $id";
                command.Parameters.AddWithValue("$id", group.Id);
            }

            command.Parameters.AddWithValue("$name", group.Name ?? string.Empty);
            command.Parameters.AddWithValue("$description", group.Description ?? string.Empty);
            command.Parameters.AddWithValue("$createdDate", group.CreatedDate.ToString("o"));
            command.Parameters.AddWithValue("$isActive", group.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("$color", group.Color ?? "#2196F3");
            command.Parameters.AddWithValue("$accountNumbers", string.Join(",", group.AccountNumbers));

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Отримати активні майстер-групи.
        /// </summary>
        public List<MasterGroup> GetMasterGroups()
        {
            var groups = new List<MasterGroup>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM MasterGroups WHERE IsActive = 1";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var group = new MasterGroup
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    CreatedDate = DateTime.Parse(reader.GetString(3)),
                    IsActive = reader.GetInt32(4) == 1,
                    Color = reader.IsDBNull(5) ? "#2196F3" : reader.GetString(5)
                };

                var accountNumbers = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                if (!string.IsNullOrWhiteSpace(accountNumbers))
                {
                    foreach (var account in accountNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        group.AccountNumbers.Add(account);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        /// <summary>
        /// Деактивувати майстер-групу (soft delete).
        /// </summary>
        public void DeleteMasterGroup(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE MasterGroups SET IsActive = 0 WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }
}
