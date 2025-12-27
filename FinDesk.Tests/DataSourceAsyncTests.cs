using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests;

public class DataSourceAsyncTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseService _databaseService;

    public DataSourceAsyncTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        _databaseService = new DatabaseService(_dbPath);
    }

    [Fact]
    public async Task AddDataSourceAsync_PersistsItemWithoutBlocking()
    {
        var source = new DataSource
        {
            Name = "Mono Test",
            Type = "Monobank",
            ApiToken = "token",
            IsEnabled = true
        };

        await _databaseService.AddDataSourceAsync(source);
        var items = await _databaseService.GetDataSourcesAsync();

        Assert.Single(items);
        Assert.Equal("Mono Test", items[0].Name);
    }

    [Fact]
    public async Task AddDataSourceAsync_CanBeCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _databaseService.AddDataSourceAsync(new DataSource { Name = "Cancel", Type = "CSV Import" }, cts.Token));
    }

    [Fact]
    public async Task UpdateDataSourceAsync_UpdatesExistingRow()
    {
        var source = new DataSource
        {
            Name = "Privat",
            Type = "PrivatBank",
            ApiToken = "123",
            IsEnabled = true
        };

        await _databaseService.AddDataSourceAsync(source);
        var existing = (await _databaseService.GetDataSourcesAsync())[0];
        existing.ApiToken = "updated";
        existing.IsEnabled = false;

        await _databaseService.UpdateDataSourceAsync(existing);
        var updated = (await _databaseService.GetDataSourcesAsync())[0];

        Assert.Equal("updated", updated.ApiToken);
        Assert.False(updated.IsEnabled);
    }

    [Fact]
    public async Task DeleteDataSourceAsync_RemovesRow()
    {
        var source = new DataSource { Name = "ToDelete", Type = "CSV Import", IsEnabled = true };
        await _databaseService.AddDataSourceAsync(source);
        var created = (await _databaseService.GetDataSourcesAsync())[0];

        await _databaseService.DeleteDataSourceAsync(created.Id);
        var items = await _databaseService.GetDataSourcesAsync();

        Assert.Empty(items);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
