using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using doc_bursa.Infrastructure.Data;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    /// <summary>
    /// Сервіс управління ієрархією MasterGroup -> AccountGroup -> Account
    /// </summary>
    public class HierarchyService
    {
        private readonly DatabaseService _db;

        public HierarchyService(DatabaseService db)
        {
            _db = db;
        }

        // === MASTER GROUPS ===
        public async Task<List<MasterGroup>> GetAllMasterGroupsAsync()
        {
            return await _db.GetMasterGroupsAsync();
        }

        public async Task<MasterGroup?> GetMasterGroupByIdAsync(int id)
        {
            var groups = await _db.GetMasterGroupsAsync();
            return groups.FirstOrDefault(g => g.Id == id);
        }

        public async Task AddMasterGroupAsync(MasterGroup masterGroup)
        {
            await _db.SaveMasterGroupAsync(masterGroup);
        }

        // === ACCOUNT GROUPS ===
        public async Task<List<AccountGroup>> GetAccountGroupsByMasterGroupAsync(int masterGroupId)
        {
            var allGroups = await _db.GetAccountGroupsAsync();
            var links = await _db.GetMasterGroupAccountGroupsAsync();
            
            var groupIds = links
                .Where(l => l.MasterGroupId == masterGroupId)
                .Select(l => l.AccountGroupId)
                .ToHashSet();

            return allGroups.Where(g => groupIds.Contains(g.Id)).ToList();
        }

        public async Task AddAccountGroupToMasterGroupAsync(int accountGroupId, int masterGroupId)
        {
            var link = new MasterGroupAccountGroup
            {
                MasterGroupId = masterGroupId,
                AccountGroupId = accountGroupId
            };
            await _db.SaveMasterGroupAccountGroupAsync(link);
        }

        // === ACCOUNTS ===
        public async Task<List<Account>> GetAccountsByGroupAsync(int accountGroupId)
        {
            var allAccounts = await _db.GetAccountsAsync();
            return allAccounts.Where(a => a.AccountGroupId == accountGroupId).ToList();
        }

        public async Task<List<Account>> GetAccountsByMasterGroupAsync(int masterGroupId)
        {
            var accountGroups = await GetAccountGroupsByMasterGroupAsync(masterGroupId);
            var groupIds = accountGroups.Select(g => g.Id).ToHashSet();
            
            var allAccounts = await _db.GetAccountsAsync();
            return allAccounts.Where(a => groupIds.Contains(a.AccountGroupId)).ToList();
        }

        /// <summary>
        /// Валідація: Один рахунок може належати лише одній групі
        /// </summary>
        public async Task<bool> ValidateAccountUniqueGroupAsync(int accountId)
        {
            var accounts = await _db.GetAccountsAsync();
            var targetAccount = accounts.FirstOrDefault(a => a.Id == accountId);
            if (targetAccount == null) return false;

            // Перевіряємо що рахунок не зустрічається в інших групах
            var duplicates = accounts.Count(a => a.ExternalId == targetAccount.ExternalId && a.AccountGroupId != targetAccount.AccountGroupId);
            return duplicates == 0;
        }

        /// <summary>
        /// Розрахунок Net Worth для MasterGroup
        /// </summary>
        public async Task<decimal> CalculateNetWorthAsync(int masterGroupId)
        {
            var accounts = await GetAccountsByMasterGroupAsync(masterGroupId);
            return accounts.Sum(a => a.Balance);
        }
    }
}
