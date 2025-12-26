using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using FinDesk.Models;

namespace FinDesk.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            var dbPath = Path.Combine(App.AppDataPath, "findesk.db");
            _connectionString = $"Data Source={dbPath}";
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
                INSERT OR IGNORE INTO Transactions 
                (TransactionId, Date, Amount, Description, Category, Source, Hash)
                VALUES ($tid, $date, $amount, $desc, $cat, $src, $hash)
            ";

            command.Parameters.AddWithValue("$tid", transaction.TransactionId);
            command.Parameters.AddWithValue("$date", transaction.Date.ToString("o"));
            command.Parameters.AddWithValue("$amount", transaction.Amount);
            command.Parameters.AddWithValue("$desc", transaction.Description ?? "");
            command.Parameters.AddWithValue("$cat", transaction.Category ?? "Інше");
            command.Parameters.AddWithValue("$src", transaction.Source ?? "");
            command.Parameters.AddWithValue("$hash", transaction.Hash ?? "");
            command.ExecuteNonQuery();
        }

        public bool AddTransaction(Transaction transaction)
        {
            try
            {
                SaveTransaction(transaction);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<Transaction> GetTransactions(DateTime? from = null, DateTime? to = null, string? category = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            var conditions = new List<string>();

            if (from.HasValue) conditions.Add($"Date >= '{from.Value:o}'");
            if (to.HasValue) conditions.Add($"Date <= '{to.Value:o}'");
            if (!string.IsNullOrEmpty(category)) conditions.Add($"Category = '{category}'");

            var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
            command.CommandText = $"SELECT * FROM Transactions {whereClause} ORDER BY Date DESC";

            var transactions = new List<Transaction>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                transactions.Add(new Transaction
                {
                    Id = reader.GetInt32(0),
                    TransactionId = reader.GetString(1),
                    Date = DateTime.Parse(reader.GetString(2)),
                    Amount = (decimal)reader.GetDouble(3),
                    Description = reader.GetString(4),
                    Category = reader.GetString(5),
                    Source = reader.GetString(6),
                    Hash = reader.GetString(7)
                });
            }

            return transactions;
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

        // Data Sources
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
            command.Parameters.AddWithValue("$token", source.ApiToken ?? "");
            command.Parameters.AddWithValue("$cid", source.ClientId ?? "");
            command.Parameters.AddWithValue("$secret", source.ClientSecret ?? "");
            command.Parameters.AddWithValue("$enabled", source.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$sync", source.LastSync?.ToString("o") ?? "");
            command.ExecuteNonQuery();
        }

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
            command.Parameters.AddWithValue("$token", source.ApiToken ?? "");
            command.Parameters.AddWithValue("$cid", source.ClientId ?? "");
            command.Parameters.AddWithValue("$secret", source.ClientSecret ?? "");
            command.Parameters.AddWithValue("$enabled", source.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("$sync", source.LastSync?.ToString("o") ?? "");
            command.Parameters.AddWithValue("$id", source.Id);
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
                sources.Add(new DataSource
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2),
                    ApiToken = reader.GetString(3),
                    ClientId = reader.GetString(4),
                    ClientSecret = reader.GetString(5),
                    IsEnabled = reader.GetInt32(6) == 1,
                    LastSync = string.IsNullOrEmpty(reader.GetString(7)) ? null : DateTime.Parse(reader.GetString(7))
                });
            }

            return sources;
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

                // Delete Transaction
        public void DeleteTransaction(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Transactions WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }
}
