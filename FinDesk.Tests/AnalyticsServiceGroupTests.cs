using System;
using System.Collections.ObjectModel;
using System.IO;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests;

public class AnalyticsServiceGroupTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseService _databaseService;
    private readonly TransactionService _transactionService;
    private readonly AnalyticsService _analyticsService;

    public AnalyticsServiceGroupTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        _databaseService = new DatabaseService(_dbPath);
        _transactionService = new TransactionService(_databaseService, new DeduplicationService(_databaseService));
        _analyticsService = new AnalyticsService(_databaseService);
    }

    [Fact]
    public void GetGroupStatistics_SumsAccounts()
    {
        var group = new MasterGroup
        {
            Name = "Test Group",
            AccountNumbers = new ObservableCollection<string> { "A1", "A2" }
        };

        _transactionService.AddTransaction(new Transaction
        {
            TransactionId = "t1",
            Account = "A1",
            Amount = 200,
            Date = new DateTime(2025, 1, 10)
        });

        _transactionService.AddTransaction(new Transaction
        {
            TransactionId = "t2",
            Account = "A2",
            Amount = -50,
            Date = new DateTime(2025, 1, 11)
        });

        var stats = _analyticsService.GetGroupStatistics(group);

        Assert.Equal(2, stats.AccountCount);
        Assert.Equal(150, stats.Balance);
        Assert.Equal(200, stats.TotalDebit);
        Assert.Equal(50, stats.TotalCredit);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
