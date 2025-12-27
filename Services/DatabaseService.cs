using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models;
using Microsoft.Data.Sqlite;
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

        public DatabaseService(string? databasePath = null)
        {
            var dbPath = string.IsNullOrWhiteSpace(databasePath)
                ? Path.Combine(App.AppDataPath, "findesk.db")
                : databasePath;
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
                    Counterparty TEXT,
                    Account TEXT,
                    Balance REAL,
                    Hash TEXT UNIQUE,
                    IsDuplicate INTEGER DEFAULT 0,
                    OriginalTransactionId TEXT
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
            EnsureBudgetTable(connection);
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
                (TransactionId, Date, Amount, Description, Category, Source, Counterparty, Account, Balance, Hash, IsDuplicate, OriginalTransactionId)
                VALUES ($tid, $date, $amount, $desc, $cat, $src, $counterparty, $account, $balance, $hash, $isDuplicate, $originalTid)
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
            command.ExecuteNonQuery();

            _logger.Information("Transaction saved: {TransactionId}", transaction.TransactionId);
        }

        /// <summary>
        /// Масово зберігає транзакції в одній транзакції SQLite для підвищення продуктивності.
        /// </summary>
        public void SaveTransactions(IEnumerable<Transaction> transactions)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var dbTransaction = connection.BeginTransaction();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Transactions 
                (TransactionId, Date, Amount, Description, Category, Source, Counterparty, Account, Balance, Hash, IsDuplicate, OriginalTransactionId)
                VALUES ($tid, $date, $amount, $desc, $cat, $src, $counterparty, $account, $balance, $hash, $isDuplicate, $originalTid)
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

                command.ExecuteNonQuery();
            }

            dbTransaction.Commit();
            _logger.Information("Batch saved: {Count} transactions", transactions.Count());
        }

        public Task SaveTransactionsAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
            => Task.Run(() => SaveTransactions(transactions), cancellationToken);

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
            command.CommandText = $"SELECT Id, TransactionId, Date, Amount, Description, Category, Source, Counterparty, Account, Balance, Hash, IsDuplicate, OriginalTransactionId FROM Transactions {whereClause} ORDER BY Date DESC";

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
                    Counterparty = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Account = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Balance = reader.IsDBNull(9) ? 0 : (decimal)reader.GetDouble(9),
                    Hash = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    IsDuplicate = !reader.IsDBNull(11) && reader.GetInt32(11) == 1,
                    OriginalTransactionId = reader.IsDBNull(12) ? string.Empty : reader.GetString(12)
                };

                transactions.Add(transaction);
            }

            return transactions;
        }

        public Task<List<Transaction>> GetTransactionsAsync(DateTime? from = null, DateTime? to = null, string? category = null, string? account = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => GetTransactions(from, to, category, account), cancellationToken);
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
        /// Отримати транзакцію за її TransactionId.
        /// </summary>
        public Transaction? GetTransactionByTransactionId(string transactionId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT Id, TransactionId, Date, Amount, Description, Category, Source, Counterparty, Account, Balance, Hash, IsDuplicate, OriginalTransactionId 
                                    FROM Transactions WHERE TransactionId = $tid LIMIT 1";
            command.Parameters.AddWithValue("$tid", transactionId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new Transaction
                {
                    Id = reader.GetInt32(0),
                    TransactionId = reader.GetString(1),
                    Date = DateTime.Parse(reader.GetString(2)),
                    Amount = (decimal)reader.GetDouble(3),
                    Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Category = reader.IsDBNull(5) ? "Інше" : reader.GetString(5),
                    Source = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Counterparty = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Account = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Balance = reader.IsDBNull(9) ? 0 : (decimal)reader.GetDouble(9),
                    Hash = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    IsDuplicate = !reader.IsDBNull(11) && reader.GetInt32(11) == 1,
                    OriginalTransactionId = reader.IsDBNull(12) ? string.Empty : reader.GetString(12)
                };
            }

            return null;
        }

        /// <summary>
        /// Оновити дублікатну інформацію транзакції.
        /// </summary>
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

        // Budgets
        /// <summary>
        /// Зберегти або оновити бюджет.
        /// </summary>
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

        /// <summary>
        /// Отримати всі бюджети.
        /// </summary>
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

        /// <summary>
        /// Отримати бюджет за ідентифікатором.
        /// </summary>
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

        /// <summary>
        /// Видалити бюджет.
        /// </summary>
        public void DeleteBudget(Guid id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Budgets WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id.ToString());
            command.ExecuteNonQuery();
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

        public async Task AddDataSourceAsync(DataSource source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

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
            return new DataSource
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                ApiToken = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                ClientId = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                ClientSecret = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                IsEnabled = reader.GetInt32(6) == 1,
                LastSync = string.IsNullOrEmpty(reader.GetString(7)) ? null : DateTime.Parse(reader.GetString(7))
            };
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

        public Task SaveMasterGroupAsync(MasterGroup group, CancellationToken cancellationToken = default)
            => Task.Run(() => SaveMasterGroup(group), cancellationToken);

        public Task<List<MasterGroup>> GetMasterGroupsAsync(CancellationToken cancellationToken = default)
            => Task.Run(GetMasterGroups, cancellationToken);

        public Task DeleteMasterGroupAsync(int id, CancellationToken cancellationToken = default)
            => Task.Run(() => DeleteMasterGroup(id), cancellationToken);
    }
}
