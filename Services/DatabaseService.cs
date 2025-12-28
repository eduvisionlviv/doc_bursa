using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Serilog;

namespace doc_bursa.Services
{
    /// <summary>
    /// Робота з локальною SQLite базою даних.
    /// </summary>
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly EncryptionService _encryption;

        public DatabaseService(string? databasePath = null)
        {
            var dbPath = string.IsNullOrWhiteSpace(databasePath)
                ? Path.Combine(App.AppDataPath, "findesk.db")
                : databasePath;
            _connectionString = $"Data Source={dbPath}";
            _logger = Log.ForContext<DatabaseService>();
            _encryption = new EncryptionService();
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
                    Counterparty TEXT,
                    Account TEXT,
                    Balance REAL,
                    Hash TEXT UNIQUE,
                    IsDuplicate INTEGER DEFAULT 0,
                    OriginalTransactionId TEXT,
                    ParentTransactionId TEXT,
                    IsSplit INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS DataSources (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    ApiToken TEXT,
                    ClientId TEXT,
                    ClientSecret TEXT,
                    IsEnabled INTEGER,
                    LastSync TEXT,
                    PingStatus TEXT,
                    DiscoveredAccounts TEXT
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

                CREATE TABLE IF NOT EXISTS AccountGroups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    Color TEXT NOT NULL,
                    Icon TEXT NOT NULL,
                    CreatedDate TEXT NOT NULL,
                    IsActive INTEGER DEFAULT 1,
                    DisplayOrder INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS MasterGroupAccountGroups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MasterGroupId INTEGER NOT NULL,
                    AccountGroupId INTEGER NOT NULL,
                    UNIQUE(MasterGroupId, AccountGroupId),
                    FOREIGN KEY(MasterGroupId) REFERENCES MasterGroups(Id) ON DELETE CASCADE,
                    FOREIGN KEY(AccountGroupId) REFERENCES AccountGroups(Id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS Accounts (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    AccountNumber TEXT,
                    Institution TEXT,
                    Currency TEXT NOT NULL,
                    Balance REAL NOT NULL DEFAULT 0,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    AccountGroupId INTEGER,
                    FOREIGN KEY(AccountGroupId) REFERENCES AccountGroups(Id) ON DELETE SET NULL
                );

                CREATE TABLE IF NOT EXISTS RecurringTransactions (
                    Id TEXT PRIMARY KEY,
                    Description TEXT NOT NULL,
                    Amount REAL NOT NULL,
                    Category TEXT,
                    AccountId TEXT,
                    Frequency TEXT NOT NULL,
                    Interval INTEGER NOT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT,
                    NextOccurrence TEXT NOT NULL,
                    LastOccurrence TEXT,
                    OccurrenceCount INTEGER,
                    IsActive INTEGER,
                    AutoExecute INTEGER,
                    ReminderDays INTEGER,
                    Notes TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT
                );
            ";
            command.ExecuteNonQuery();

            EnsureTransactionColumns(connection);
            EnsureDataSourceColumns(connection);
            EnsureBudgetTable(connection);
            EnsureRecurringTransactionsTable(connection);
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
            AddColumnIfMissing("IsDuplicate", "INTEGER DEFAULT 0");
            AddColumnIfMissing("OriginalTransactionId", "TEXT");
            AddColumnIfMissing("Counterparty", "TEXT");
            AddColumnIfMissing("ParentTransactionId", "TEXT");
            AddColumnIfMissing("IsSplit", "INTEGER DEFAULT 0");
        }

        private static void EnsureDataSourceColumns(SqliteConnection connection)
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA table_info(DataSources)";
            using (var reader = pragma.ExecuteReader())
            {
                while (reader.Read())
                {
                    existingColumns.Add(reader.GetString(1));
                }
            }

            void AddColumn(string column, string definition)
            {
                if (existingColumns.Contains(column))
                {
                    return;
                }

                var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE DataSources ADD COLUMN {column} {definition}";
                alter.ExecuteNonQuery();
            }

            AddColumn("PingStatus", "TEXT");
            AddColumn("DiscoveredAccounts", "TEXT");
        }

        private static void EnsureBudgetTable(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Budgets (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Category TEXT,
                    MonthlyLimit REAL NOT NULL,
                    Spent REAL NOT NULL,
                    Frequency TEXT NOT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT,
                    IsActive INTEGER NOT NULL,
                    AlertThreshold INTEGER NOT NULL,
                    Description TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT
                );
            ";

            command.ExecuteNonQuery();
        }

        private static void EnsureRecurringTransactionsTable(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS RecurringTransactions (
                    Id TEXT PRIMARY KEY,
                    Description TEXT NOT NULL,
                    Amount REAL NOT NULL,
                    Category TEXT,
                    AccountId TEXT,
                    Frequency TEXT NOT NULL,
                    Interval INTEGER NOT NULL,
                    StartDate TEXT NOT NULL,
                    EndDate TEXT,
                    NextOccurrence TEXT NOT NULL,
                    LastOccurrence TEXT,
                    OccurrenceCount INTEGER,
                    IsActive INTEGER,
                    AutoExecute INTEGER,
                    ReminderDays INTEGER,
                    Notes TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT
                );
            ";

            command.ExecuteNonQuery();
        }

        // Transactions
        public void SaveTransaction(Transaction transaction)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Transactions 
                (TransactionId, Date, Amount, Description, Category, Source, Counterparty, Account, Balance, Hash, IsDuplicate, OriginalTransactionId, ParentTransactionId, IsSplit)
                VALUES ($tid, $date, $amount, $desc, $cat, $src, $counterparty, $account, $balance, $hash, $isDuplicate, $originalTid, $parentTid, $isSplit)
            ";

            command.Parameters.AddWithValue("$tid", transaction.TransactionId);
            command.Parameters.AddWithValue("$date", transaction.Date.ToString("o"));
            command.Parameters.AddWithValue("$amount", transaction.Amount);
            command.Parameters.AddWithValue("$desc", transaction.Description ?? string.Empty);
            command.Parameters.AddWithValue("$cat", transaction.Category ?? "Інше");
            command.Parameters.AddWithValue("$src", transaction.Source ?? string.Empty);
            command.Parameters.AddWithValue("$counterparty", transaction.Counterparty ?? string.Empty);
            command.Parameters.AddWithValue("$account", transaction.Account ?? string.Empty);
            command.Parameters.AddWithValue("$balance", transaction.Balance);
            command.Parameters.AddWithValue("$hash", transaction.Hash ?? string.Empty);
            command.Parameters.AddWithValue("$isDuplicate", transaction.IsDuplicate ? 1 : 0);
            command.Parameters.AddWithValue("$originalTid", transaction.OriginalTransactionId ?? string.Empty);
            command.Parameters.AddWithValue("$parentTid", transaction.ParentTransactionId ?? string.Empty);
            command.Parameters.AddWithValue("$isSplit", transaction.IsSplit ? 1 : 0);
            command.ExecuteNonQuery();

            _logger.Information("Transaction saved: {TransactionId}", transaction.TransactionId);
        }

        public void SaveTransactions(IEnumerable<Transaction> transactions)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var dbTransaction = connection.BeginTransaction();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Transactions 
                (TransactionId, Date, Amount, Description, Category, Source, Counterparty, Account, Balance, Hash, IsDuplicate, OriginalTransactionId, ParentTransactionId, IsSplit)
                VALUES ($tid, $date, $amount, $desc, $cat, $src, $counterparty, $account, $balance, $hash, $isDuplicate, $originalTid, $parentTid, $isSplit)
            ";

            var tidParam = command.Parameters.Add("$tid", SqliteType.Text);
            var dateParam = command.Parameters.Add("$date", SqliteType.Text);
            var amountParam = command.Parameters.Add("$amount", SqliteType.Real);
            var descParam = command.Parameters.Add("$desc", SqliteType.Text);
            var catParam = command.Parameters.Add("$cat", SqliteType.Text);
            var srcParam = command.Parameters.Add("$src", SqliteType.Text);
            var counterpartyParam = command.Parameters.Add("$counterparty", SqliteType.Text);
            var accountParam = command.Parameters.Add("$account", SqliteType.Text);
            var balanceParam = command.Parameters.Add("$balance", SqliteType.Real);
            var hashParam = command.Parameters.Add("$hash", SqliteType.Text);
            var isDuplicateParam = command.Parameters.Add("$isDuplicate", SqliteType.Integer);
            var originalTidParam = command.Parameters.Add("$originalTid", SqliteType.Text);
            var parentTidParam = command.Parameters.Add("$parentTid", SqliteType.Text);
            var isSplitParam = command.Parameters.Add("$isSplit", SqliteType.Integer);

            foreach (var transaction in transactions)
            {
                tidParam.Value = transaction.TransactionId;
                dateParam.Value = transaction.Date.ToString("o");
                amountParam.Value = transaction.Amount;
                descParam.Value = transaction.Description ?? string.Empty;
                catParam.Value = transaction.Category ?? "Інше";
                srcParam.Value = transaction.Source ?? string.Empty;
                counterpartyParam.Value = transaction.Counterparty ?? string.Empty;
                accountParam.Value = transaction.Account ?? string.Empty;
                balanceParam.Value = transaction.Balance;
                hashParam.Value = transaction.Hash ?? string.Empty;
                isDuplicateParam.Value = transaction.IsDuplicate ? 1 : 0;
                originalTidParam.Value = transaction.OriginalTransactionId ?? string.Empty;
                parentTidParam.Value = transaction.ParentTransactionId ?? string.Empty;
                isSplitParam.Value = transaction.IsSplit ? 1 : 0;

                command.ExecuteNonQuery();
            }

            dbTransaction.Commit();
            _logger.Information("Batch saved: {Count} transactions", transactions.Count());
        }

        public Task SaveTransactionsAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
            => Task.Run(() => SaveTransactions(transactions), cancellationToken);

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

        public List<Transaction> GetTransactions(DateTime? from = null, DateTime? to = null, string? category = null, string? account = null, IEnumerable<string>? accounts = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var conditions = new List<string>();
            var command = connection.CreateCommand();
            var accountList = accounts?.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList() ?? new List<string>();

            if (!string.IsNullOrEmpty(account))
            {
                accountList.Add(account);
            }

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

            if (accountList.Count > 0)
            {
                var accountParameters = new List<string>();
                for (var i = 0; i < accountList.Count; i++)
                {
                    var parameterName = $"$acc{i}";
                    accountParameters.Add(parameterName);
                    command.Parameters.AddWithValue(parameterName, accountList[i]);
                }

                conditions.Add($"Account IN ({string.Join(",", accountParameters)})");
            }
            else if (accountsScope.HasValue)
            {
                var accountParameters = accountsScope.Accounts
                    .Select((acc, index) => new { Parameter = $"$acc{index}", Value = acc })
                    .ToList();

                conditions.Add($"Account IN ({string.Join(",", accountParameters.Select(p => p.Parameter))})");

                foreach (var parameter in accountParameters)
                {
                    command.Parameters.AddWithValue(parameter.Parameter, parameter.Value);
                }
            }

            var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
            command.CommandText = $"SELECT Id, TransactionId, Date, Amount, Description, Category, Source, Counterparty, Account, Balance, Hash, IsDuplicate, OriginalTransactionId, ParentTransactionId, IsSplit FROM Transactions {whereClause} ORDER BY Date DESC";

            var transactions = new List<Transaction>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                DateTime dateVal;
                var dateStr = reader.GetString(2);
                if (!DateTime.TryParse(dateStr, out dateVal))
                {
                    dateVal = DateTime.MinValue;
                }

                var transaction = new Transaction
                {
                    Id = reader.GetInt32(0),
                    TransactionId = reader.GetString(1),
                    Date = dateVal,
                    Amount = (decimal)reader.GetDouble(3),
                    Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Category = reader.IsDBNull(5) ? "Інше" : reader.GetString(5),
                    Source = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Counterparty = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Account = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Balance = reader.IsDBNull(9) ? 0 : (decimal)reader.GetDouble(9),
                    Hash = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    IsDuplicate = !reader.IsDBNull(11) && reader.GetInt32(11) == 1,
                    OriginalTransactionId = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                    ParentTransactionId = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                    IsSplit = !reader.IsDBNull(14) && reader.GetInt32(14) == 1
                };

                transactions.Add(transaction);
            }

            return transactions;
        }

        public Task<List<Transaction>> GetTransactionsAsync(DateTime? from = null, DateTime? to = null, string? category = null, string? account = null, IEnumerable<string>? accounts = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => GetTransactions(from, to, category, account, accounts), cancellationToken);
        }

        public List<Transaction> GetTransactionsByAccount(string account, int? masterGroupId = null)
        {
            return GetTransactions(account: account, masterGroupId: masterGroupId);
        }

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

        public void DeleteTransaction(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Transactions WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public void DeleteChildTransactions(string parentTransactionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Transactions WHERE ParentTransactionId = $parentTid";
            command.Parameters.AddWithValue("$parentTid", parentTransactionId);
            command.ExecuteNonQuery();
        }

        public Transaction? GetTransactionByTransactionId(string transactionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT Id, TransactionId, Date, Amount, Description, Category, Source, Counterparty, Account, Balance, Hash, IsDuplicate, OriginalTransactionId, ParentTransactionId, IsSplit 
                                    FROM Transactions WHERE TransactionId = $tid LIMIT 1";
            command.Parameters.AddWithValue("$tid", transactionId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                DateTime dateVal;
                var dateStr = reader.GetString(2);
                if (!DateTime.TryParse(dateStr, out dateVal)) dateVal = DateTime.MinValue;

                return new Transaction
                {
                    Id = reader.GetInt32(0),
                    TransactionId = reader.GetString(1),
                    Date = dateVal,
                    Amount = (decimal)reader.GetDouble(3),
                    Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Category = reader.IsDBNull(5) ? "Інше" : reader.GetString(5),
                    Source = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Counterparty = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Account = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Balance = reader.IsDBNull(9) ? 0 : (decimal)reader.GetDouble(9),
                    Hash = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    IsDuplicate = !reader.IsDBNull(11) && reader.GetInt32(11) == 1,
                    OriginalTransactionId = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                    ParentTransactionId = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                    IsSplit = !reader.IsDBNull(14) && reader.GetInt32(14) == 1
                };
            }

            return null;
        }

        public void UpdateDuplicateInfo(int id, bool isDuplicate, string? originalTransactionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"UPDATE Transactions 
                                    SET IsDuplicate = $isDuplicate, OriginalTransactionId = $originalTid 
                                    WHERE Id = $id";
            command.Parameters.AddWithValue("$isDuplicate", isDuplicate ? 1 : 0);
            command.Parameters.AddWithValue("$originalTid", originalTransactionId ?? string.Empty);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

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

        // Budgets
        public void SaveBudget(Budget budget)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Budgets
                (Id, Name, Category, MonthlyLimit, Spent, Frequency, StartDate, EndDate, IsActive, AlertThreshold, Description, CreatedAt, UpdatedAt)
                VALUES ($id, $name, $category, $limit, $spent, $frequency, $start, $end, $active, $threshold, $description, $created, $updated)";

            command.Parameters.AddWithValue("$id", budget.Id.ToString());
            command.Parameters.AddWithValue("$name", budget.Name);
            command.Parameters.AddWithValue("$category", budget.Category ?? string.Empty);
            command.Parameters.AddWithValue("$limit", budget.Limit);
            command.Parameters.AddWithValue("$spent", budget.Spent);
            command.Parameters.AddWithValue("$frequency", budget.Frequency.ToString());
            command.Parameters.AddWithValue("$start", budget.StartDate.ToString("o"));
            command.Parameters.AddWithValue("$end", budget.EndDate?.ToString("o") ?? string.Empty);
            command.Parameters.AddWithValue("$active", budget.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("$threshold", budget.AlertThreshold);
            command.Parameters.AddWithValue("$description", budget.Description ?? string.Empty);
            command.Parameters.AddWithValue("$created", budget.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("$updated", budget.UpdatedAt?.ToString("o") ?? string.Empty);

            command.ExecuteNonQuery();
        }

        public List<Budget> GetBudgets()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Category, MonthlyLimit, Spent, Frequency, StartDate, EndDate, IsActive, AlertThreshold, Description, CreatedAt, UpdatedAt FROM Budgets";

            var result = new List<Budget>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var budget = new Budget
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    Category = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Limit = (decimal)reader.GetDouble(3),
                    Spent = (decimal)reader.GetDouble(4),
                    Frequency = Enum.TryParse<BudgetFrequency>(reader.GetString(5), true, out var freq) ? freq : BudgetFrequency.Monthly,
                    StartDate = DateTime.Parse(reader.GetString(6)),
                    EndDate = reader.IsDBNull(7) || string.IsNullOrWhiteSpace(reader.GetString(7))
                        ? null
                        : DateTime.Parse(reader.GetString(7)),
                    IsActive = !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
                    AlertThreshold = reader.GetInt32(9),
                    Description = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    CreatedAt = DateTime.Parse(reader.GetString(11)),
                    UpdatedAt = reader.IsDBNull(12) || string.IsNullOrWhiteSpace(reader.GetString(12))
                        ? null
                        : DateTime.Parse(reader.GetString(12))
                };

                result.Add(budget);
            }

            return result;
        }

        public Budget? GetBudget(Guid id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT Id, Name, Category, MonthlyLimit, Spent, Frequency, StartDate, EndDate, IsActive, AlertThreshold, Description, CreatedAt, UpdatedAt 
                                    FROM Budgets WHERE Id = $id LIMIT 1";
            command.Parameters.AddWithValue("$id", id.ToString());

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new Budget
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Category = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Limit = (decimal)reader.GetDouble(3),
                Spent = (decimal)reader.GetDouble(4),
                Frequency = Enum.TryParse<BudgetFrequency>(reader.GetString(5), true, out var freq) ? freq : BudgetFrequency.Monthly,
                StartDate = DateTime.Parse(reader.GetString(6)),
                EndDate = reader.IsDBNull(7) || string.IsNullOrWhiteSpace(reader.GetString(7))
                    ? null
                    : DateTime.Parse(reader.GetString(7)),
                IsActive = !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
                AlertThreshold = reader.GetInt32(9),
                Description = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                CreatedAt = DateTime.Parse(reader.GetString(11)),
                UpdatedAt = reader.IsDBNull(12) || string.IsNullOrWhiteSpace(reader.GetString(12))
                    ? null
                    : DateTime.Parse(reader.GetString(12))
            };
        }

        public void DeleteBudget(Guid id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Budgets WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id.ToString());
            command.ExecuteNonQuery();
        }

        // Recurring Transactions
        public void SaveRecurringTransaction(RecurringTransaction recurring)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO RecurringTransactions
                (Id, Description, Amount, Category, AccountId, Frequency, Interval, StartDate, EndDate, NextOccurrence, LastOccurrence, OccurrenceCount, IsActive, AutoExecute, ReminderDays, Notes, CreatedAt, UpdatedAt)
                VALUES ($id, $desc, $amount, $cat, $accountId, $freq, $interval, $start, $end, $next, $last, $count, $active, $auto, $reminder, $notes, $created, $updated)";

            command.Parameters.AddWithValue("$id", recurring.Id.ToString());
            command.Parameters.AddWithValue("$desc", recurring.Description);
            command.Parameters.AddWithValue("$amount", recurring.Amount);
            command.Parameters.AddWithValue("$cat", recurring.Category ?? string.Empty);
            command.Parameters.AddWithValue("$accountId", recurring.AccountId?.ToString() ?? string.Empty);
            command.Parameters.AddWithValue("$freq", recurring.Frequency.ToString());
            command.Parameters.AddWithValue("$interval", recurring.Interval);
            command.Parameters.AddWithValue("$start", recurring.StartDate.ToString("o"));
            command.Parameters.AddWithValue("$end", recurring.EndDate?.ToString("o") ?? string.Empty);
            command.Parameters.AddWithValue("$next", recurring.NextOccurrence.ToString("o"));
            command.Parameters.AddWithValue("$last", recurring.LastOccurrence?.ToString("o") ?? string.Empty);
            command.Parameters.AddWithValue("$count", recurring.OccurrenceCount);
            command.Parameters.AddWithValue("$active", recurring.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("$auto", recurring.AutoExecute ? 1 : 0);
            command.Parameters.AddWithValue("$reminder", recurring.ReminderDays);
            command.Parameters.AddWithValue("$notes", recurring.Notes ?? string.Empty);
            command.Parameters.AddWithValue("$created", recurring.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("$updated", recurring.UpdatedAt?.ToString("o") ?? string.Empty);

            command.ExecuteNonQuery();
        }

        public List<RecurringTransaction> GetRecurringTransactions(bool onlyActive = false)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT Id, Description, Amount, Category, AccountId, Frequency, Interval, StartDate, EndDate, NextOccurrence, LastOccurrence, OccurrenceCount, IsActive, AutoExecute, ReminderDays, Notes, CreatedAt, UpdatedAt 
                                    FROM RecurringTransactions" + (onlyActive ? " WHERE IsActive = 1" : string.Empty);

            var result = new List<RecurringTransaction>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var recurring = new RecurringTransaction
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Description = reader.GetString(1),
                    Amount = (decimal)reader.GetDouble(2),
                    Category = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    AccountId = Guid.TryParse(reader.IsDBNull(4) ? null : reader.GetString(4), out var accountId) ? accountId : null,
                    Frequency = Enum.TryParse<RecurrenceFrequency>(reader.GetString(5), true, out var freq) ? freq : RecurrenceFrequency.Monthly,
                    Interval = reader.GetInt32(6),
                    StartDate = DateTime.Parse(reader.GetString(7)),
                    EndDate = reader.IsDBNull(8) || string.IsNullOrWhiteSpace(reader.GetString(8)) ? null : DateTime.Parse(reader.GetString(8)),
                    NextOccurrence = DateTime.Parse(reader.GetString(9)),
                    LastOccurrence = reader.IsDBNull(10) || string.IsNullOrWhiteSpace(reader.GetString(10)) ? null : DateTime.Parse(reader.GetString(10)),
                    OccurrenceCount = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                    IsActive = !reader.IsDBNull(12) && reader.GetInt32(12) == 1,
                    AutoExecute = !reader.IsDBNull(13) && reader.GetInt32(13) == 1,
                    ReminderDays = reader.GetInt32(14),
                    Notes = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                    CreatedAt = DateTime.Parse(reader.GetString(16)),
                    UpdatedAt = reader.IsDBNull(17) || string.IsNullOrWhiteSpace(reader.GetString(17)) ? null : DateTime.Parse(reader.GetString(17))
                };

                result.Add(recurring);
            }

            return result;
        }

        public RecurringTransaction? GetRecurringTransaction(Guid id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT Id, Description, Amount, Category, AccountId, Frequency, Interval, StartDate, EndDate, NextOccurrence, LastOccurrence, OccurrenceCount, IsActive, AutoExecute, ReminderDays, Notes, CreatedAt, UpdatedAt 
                                    FROM RecurringTransactions WHERE Id = $id LIMIT 1";
            command.Parameters.AddWithValue("$id", id.ToString());

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new RecurringTransaction
            {
                Id = Guid.Parse(reader.GetString(0)),
                Description = reader.GetString(1),
                Amount = (decimal)reader.GetDouble(2),
                Category = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                AccountId = Guid.TryParse(reader.IsDBNull(4) ? null : reader.GetString(4), out var accountId) ? accountId : null,
                Frequency = Enum.TryParse<RecurrenceFrequency>(reader.GetString(5), true, out var freq) ? freq : RecurrenceFrequency.Monthly,
                Interval = reader.GetInt32(6),
                StartDate = DateTime.Parse(reader.GetString(7)),
                EndDate = reader.IsDBNull(8) || string.IsNullOrWhiteSpace(reader.GetString(8)) ? null : DateTime.Parse(reader.GetString(8)),
                NextOccurrence = DateTime.Parse(reader.GetString(9)),
                LastOccurrence = reader.IsDBNull(10) || string.IsNullOrWhiteSpace(reader.GetString(10)) ? null : DateTime.Parse(reader.GetString(10)),
                OccurrenceCount = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                IsActive = !reader.IsDBNull(12) && reader.GetInt32(12) == 1,
                AutoExecute = !reader.IsDBNull(13) && reader.GetInt32(13) == 1,
                ReminderDays = reader.GetInt32(14),
                Notes = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                CreatedAt = DateTime.Parse(reader.GetString(16)),
                UpdatedAt = reader.IsDBNull(17) || string.IsNullOrWhiteSpace(reader.GetString(17)) ? null : DateTime.Parse(reader.GetString(17))
            };
        }

        public void DeleteRecurringTransaction(Guid id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM RecurringTransactions WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id.ToString());
            command.ExecuteNonQuery();
        }

        // Data Sources
        public void AddDataSource(DataSource source)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO DataSources 
                (Name, Type, ApiToken, ClientId, ClientSecret, IsEnabled, LastSync, PingStatus, DiscoveredAccounts)
                VALUES ($name, $type, $token, $cid, $secret, $enabled, $sync, $ping, $discovered)
            ";

            command.Parameters.AddWithValue("$name", source.Name);
            command.Parameters.AddWithValue("$type", source.Type);
            command.Parameters.AddWithValue("$token", EncryptSensitive(source.ApiToken));
            command.Parameters.AddWithValue("$cid", source.ClientId ?? string.Empty);
            command.Parameters.AddWithValue("$secret", EncryptSensitive(source.ClientSecret));
            command.Parameters.AddWithValue("$enabled", source.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$sync", source.LastSync?.ToString("o") ?? string.Empty);
            command.Parameters.AddWithValue("$ping", source.PingStatus ?? string.Empty);
            command.Parameters.AddWithValue("$discovered", SerializeAccounts(source.DiscoveredAccounts));
            command.ExecuteNonQuery();
        }

        public async Task AddDataSourceAsync(DataSource source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO DataSources 
                (Name, Type, ApiToken, ClientId, ClientSecret, IsEnabled, LastSync, PingStatus, DiscoveredAccounts)
                VALUES ($name, $type, $token, $cid, $secret, $enabled, $sync, $ping, $discovered)
            ";

            command.Parameters.AddWithValue("$name", source.Name);
            command.Parameters.AddWithValue("$type", source.Type);
            command.Parameters.AddWithValue("$token", EncryptSensitive(source.ApiToken));
            command.Parameters.AddWithValue("$cid", source.ClientId ?? string.Empty);
            command.Parameters.AddWithValue("$secret", EncryptSensitive(source.ClientSecret));
            command.Parameters.AddWithValue("$enabled", source.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$sync", source.LastSync?.ToString("o") ?? string.Empty);
            command.Parameters.AddWithValue("$ping", source.PingStatus ?? string.Empty);
            command.Parameters.AddWithValue("$discovered", SerializeAccounts(source.DiscoveredAccounts));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task UpdateDataSourceAsync(DataSource source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE DataSources 
                SET Name = $name, Type = $type, ApiToken = $token, ClientId = $cid, 
                    ClientSecret = $secret, IsEnabled = $enabled, LastSync = $sync, 
                    PingStatus = $ping, DiscoveredAccounts = $discovered
                WHERE Id = $id
            ";

            command.Parameters.AddWithValue("$name", source.Name);
            command.Parameters.AddWithValue("$type", source.Type);
            command.Parameters.AddWithValue("$token", EncryptSensitive(source.ApiToken));
            command.Parameters.AddWithValue("$cid", source.ClientId ?? string.Empty);
            command.Parameters.AddWithValue("$secret", EncryptSensitive(source.ClientSecret));
            command.Parameters.AddWithValue("$enabled", source.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$sync", source.LastSync?.ToString("o") ?? string.Empty);
            command.Parameters.AddWithValue("$id", source.Id);
            command.Parameters.AddWithValue("$ping", source.PingStatus ?? string.Empty);
            command.Parameters.AddWithValue("$discovered", SerializeAccounts(source.DiscoveredAccounts));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task DeleteDataSourceAsync(int id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM DataSources WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public void UpdateDataSource(DataSource source)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE DataSources 
                SET Name = $name, Type = $type, ApiToken = $token, ClientId = $cid, 
                    ClientSecret = $secret, IsEnabled = $enabled, LastSync = $sync, 
                    PingStatus = $ping, DiscoveredAccounts = $discovered
                WHERE Id = $id
            ";

            command.Parameters.AddWithValue("$name", source.Name);
            command.Parameters.AddWithValue("$type", source.Type);
            command.Parameters.AddWithValue("$token", EncryptSensitive(source.ApiToken));
            command.Parameters.AddWithValue("$cid", source.ClientId ?? string.Empty);
            command.Parameters.AddWithValue("$secret", EncryptSensitive(source.ClientSecret));
            command.Parameters.AddWithValue("$enabled", source.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$sync", source.LastSync?.ToString("o") ?? string.Empty);
            command.Parameters.AddWithValue("$id", source.Id);
            command.Parameters.AddWithValue("$ping", source.PingStatus ?? string.Empty);
            command.Parameters.AddWithValue("$discovered", SerializeAccounts(source.DiscoveredAccounts));
            command.ExecuteNonQuery();
        }

        public void DeleteDataSource(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM DataSources WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

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
                sources.Add(ReadDataSource(reader));
            }

            return sources;
        }

        public async Task<List<DataSource>> GetDataSourcesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM DataSources";

            var sources = new List<DataSource>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                sources.Add(ReadDataSource(reader));
            }

            return sources;
        }

        private static DataSource ReadDataSource(SqliteDataReader reader)
        {
            var lastSyncRaw = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
            var pingStatus = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetString(8) : string.Empty;
            var discoveredRaw = reader.FieldCount > 9 && !reader.IsDBNull(9) ? reader.GetString(9) : string.Empty;
            return new DataSource
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                ApiToken = DecryptSensitive(reader.IsDBNull(3) ? string.Empty : reader.GetString(3)),
                ClientId = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                ClientSecret = DecryptSensitive(reader.IsDBNull(5) ? string.Empty : reader.GetString(5)),
                IsEnabled = reader.GetInt32(6) == 1,
                LastSync = string.IsNullOrEmpty(lastSyncRaw) ? null : DateTime.Parse(lastSyncRaw),
                PingStatus = pingStatus,
                DiscoveredAccounts = DeserializeAccounts(discoveredRaw)
            };
        }

        private string EncryptSensitive(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return _encryption.IsEncrypted(value) ? value : _encryption.Encrypt(value);
        }

        private string DecryptSensitive(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return _encryption.Decrypt(value);
        }

        // Category Rules
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

        // AccountGroup CRUD operations
        public void SaveAccountGroup(AccountGroup group)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            long accountGroupId = group.Id;

            var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (group.Id == 0)
            {
                command.CommandText = @"
                    INSERT INTO AccountGroups (Name, Description, Color, Icon, CreatedDate, IsActive, DisplayOrder)
                    VALUES ($name, $description, $color, $icon, $createdDate, $isActive, $displayOrder)";
            }
            else
            {
                command.CommandText = @"
                    UPDATE AccountGroups
                    SET Name = $name, Description = $description, Color = $color, Icon = $icon,
                        IsActive = $isActive, DisplayOrder = $displayOrder
                    WHERE Id = $id";
                command.Parameters.AddWithValue("$id", group.Id);
            }

            command.Parameters.AddWithValue("$name", group.Name ?? string.Empty);
            command.Parameters.AddWithValue("$description", group.Description ?? string.Empty);
            command.Parameters.AddWithValue("$color", group.Color ?? "#2196F3");
            command.Parameters.AddWithValue("$icon", group.Icon ?? "AccountMultiple");
            command.Parameters.AddWithValue("$createdDate", group.CreatedDate.ToString("o"));
            command.Parameters.AddWithValue("$isActive", group.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("$displayOrder", group.DisplayOrder);

            command.ExecuteNonQuery();

            if (group.Id == 0)
            {
                accountGroupId = connection.LastInsertRowId;
                group.Id = (int)accountGroupId;
            }

            var clearLinks = connection.CreateCommand();
            clearLinks.Transaction = transaction;
            clearLinks.CommandText = "DELETE FROM MasterGroupAccountGroups WHERE AccountGroupId = $agid";
            clearLinks.Parameters.AddWithValue("$agid", accountGroupId);
            clearLinks.ExecuteNonQuery();

            var distinctLinks = group.MasterGroupLinks
                .Where(l => l.MasterGroupId > 0)
                .GroupBy(l => l.MasterGroupId)
                .Select(g => g.First());

            foreach (var link in distinctLinks)
            {
                var linkCommand = connection.CreateCommand();
                linkCommand.Transaction = transaction;
                linkCommand.CommandText = @"
                    INSERT OR IGNORE INTO MasterGroupAccountGroups (MasterGroupId, AccountGroupId)
                    VALUES ($mid, $agid)";
                linkCommand.Parameters.AddWithValue("$mid", link.MasterGroupId);
                linkCommand.Parameters.AddWithValue("$agid", accountGroupId);
                linkCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public List<AccountGroup> GetAccountGroups()
        {
            var groups = new List<AccountGroup>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Description, Color, Icon, CreatedDate, IsActive, DisplayOrder FROM AccountGroups WHERE IsActive = 1";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var group = new AccountGroup
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Color = reader.GetString(3),
                    Icon = reader.GetString(4),
                    CreatedDate = DateTime.Parse(reader.GetString(5)),
                    IsActive = reader.GetInt32(6) == 1,
                    DisplayOrder = reader.GetInt32(7)
                };

                groups.Add(group);
            }

            var groupMap = groups.ToDictionary(g => g.Id);
            var linksCommand = connection.CreateCommand();
            linksCommand.CommandText = "SELECT Id, MasterGroupId, AccountGroupId FROM MasterGroupAccountGroups";

            using var linksReader = linksCommand.ExecuteReader();
            while (linksReader.Read())
            {
                var link = new MasterGroupAccountGroup
                {
                    Id = linksReader.GetInt32(0),
                    MasterGroupId = linksReader.GetInt32(1),
                    AccountGroupId = linksReader.GetInt32(2)
                };

                if (groupMap.TryGetValue(link.AccountGroupId, out var accountGroup))
                {
                    link.AccountGroup = accountGroup;
                    accountGroup.MasterGroupLinks.Add(link);
                }
            }

            return groups;
        }

        public void DeleteAccountGroup(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var transaction = connection.BeginTransaction();

            var deleteLinks = connection.CreateCommand();
            deleteLinks.Transaction = transaction;
            deleteLinks.CommandText = "DELETE FROM MasterGroupAccountGroups WHERE AccountGroupId = $id";
            deleteLinks.Parameters.AddWithValue("$id", id);
            deleteLinks.ExecuteNonQuery();

            var unbindAccounts = connection.CreateCommand();
            unbindAccounts.Transaction = transaction;
            unbindAccounts.CommandText = "UPDATE Accounts SET AccountGroupId = NULL WHERE AccountGroupId = $id";
            unbindAccounts.Parameters.AddWithValue("$id", id);
            unbindAccounts.ExecuteNonQuery();

            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE AccountGroups SET IsActive = 0 WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();

            transaction.Commit();
        }

        // MasterGroup CRUD operations
        public HashSet<string> GetMasterGroupAccounts(int masterGroupId)
        {
            var group = GetMasterGroups().FirstOrDefault(g => g.Id == masterGroupId);
            if (group?.AccountNumbers == null || group.AccountNumbers.Count == 0)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(group.AccountNumbers.Where(a => !string.IsNullOrWhiteSpace(a)), StringComparer.OrdinalIgnoreCase);
        }

        public string? GetMasterGroupName(int masterGroupId)
        {
            return GetMasterGroups().FirstOrDefault(g => g.Id == masterGroupId)?.Name;
        }

        public void SaveMasterGroup(MasterGroup group)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            long masterGroupId = group.Id;

            var command = connection.CreateCommand();
            command.Transaction = transaction;
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

            if (group.Id == 0)
            {
                masterGroupId = connection.LastInsertRowId;
                group.Id = (int)masterGroupId;
            }

            var clearLinks = connection.CreateCommand();
            clearLinks.Transaction = transaction;
            clearLinks.CommandText = "DELETE FROM MasterGroupAccountGroups WHERE MasterGroupId = $mid";
            clearLinks.Parameters.AddWithValue("$mid", masterGroupId);
            clearLinks.ExecuteNonQuery();

            var distinctLinks = group.AccountGroupLinks
                .Where(l => l.AccountGroupId > 0)
                .GroupBy(l => l.AccountGroupId)
                .Select(g => g.First());

            foreach (var link in distinctLinks)
            {
                var linkCommand = connection.CreateCommand();
                linkCommand.Transaction = transaction;
                linkCommand.CommandText = @"
                    INSERT OR IGNORE INTO MasterGroupAccountGroups (MasterGroupId, AccountGroupId)
                    VALUES ($mid, $agid)";
                linkCommand.Parameters.AddWithValue("$mid", masterGroupId);
                linkCommand.Parameters.AddWithValue("$agid", link.AccountGroupId);
                linkCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public List<MasterGroup> GetMasterGroups()
        {
            var groups = new List<MasterGroup>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM MasterGroups WHERE IsActive = 1";

            using (var reader = command.ExecuteReader())
            {
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
            }

            var masterGroupMap = groups.ToDictionary(g => g.Id);
            var accountGroupMap = GetAccountGroups().ToDictionary(g => g.Id);

            var linksCommand = connection.CreateCommand();
            linksCommand.CommandText = "SELECT Id, MasterGroupId, AccountGroupId FROM MasterGroupAccountGroups";

            using (var linksReader = linksCommand.ExecuteReader())
            {
                while (linksReader.Read())
                {
                    var link = new MasterGroupAccountGroup
                    {
                        Id = linksReader.GetInt32(0),
                        MasterGroupId = linksReader.GetInt32(1),
                        AccountGroupId = linksReader.GetInt32(2)
                    };

                    if (masterGroupMap.TryGetValue(link.MasterGroupId, out var masterGroup))
                    {
                        link.MasterGroup = masterGroup;

                        if (accountGroupMap.TryGetValue(link.AccountGroupId, out var accountGroup))
                        {
                            link.AccountGroup = accountGroup;
                        }

                        masterGroup.AccountGroupLinks.Add(link);
                    }
                }
            }

            return groups;
        }

        public void DeleteMasterGroup(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            var deleteLinks = connection.CreateCommand();
            deleteLinks.Transaction = transaction;
            deleteLinks.CommandText = "DELETE FROM MasterGroupAccountGroups WHERE MasterGroupId = $id";
            deleteLinks.Parameters.AddWithValue("$id", id);
            deleteLinks.ExecuteNonQuery();

            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE MasterGroups SET IsActive = 0 WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();

            transaction.Commit();
        }

        public Task SaveMasterGroupAsync(MasterGroup group, CancellationToken cancellationToken = default)
            => Task.Run(() => SaveMasterGroup(group), cancellationToken);

        public Task<List<MasterGroup>> GetMasterGroupsAsync(CancellationToken cancellationToken = default)
            => Task.Run(GetMasterGroups, cancellationToken);

        public Task DeleteMasterGroupAsync(int id, CancellationToken cancellationToken = default)
            => Task.Run(() => DeleteMasterGroup(id), cancellationToken);

        public string EnsureVirtualAccountForGroup(int? groupId, string? preferredName = null)
        {
            if (!groupId.HasValue)
            {
                return preferredName ?? string.Empty;
            }

            var accountNumber = string.IsNullOrWhiteSpace(preferredName)
                ? $"virtual-group-{groupId.Value}"
                : preferredName;

            var groups = GetMasterGroups();
            var targetGroup = groups.FirstOrDefault(g => g.Id == groupId.Value);
            if (targetGroup != null && !targetGroup.AccountNumbers.Contains(accountNumber))
            {
                targetGroup.AccountNumbers.Add(accountNumber);
                SaveMasterGroup(targetGroup);
            }

            return accountNumber;
        }
    }
}
