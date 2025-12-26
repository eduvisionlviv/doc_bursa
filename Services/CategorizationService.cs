using System;
using System.Collections.Generic;
using System.Linq;
using FinDesk.Models;

namespace FinDesk.Services
{
    public class CategorizationService
    {
        private readonly DatabaseService _db;
        private Dictionary<string, string> _rules;

        public CategorizationService(DatabaseService db)
        {
            _db = db;
            _rules = _db.GetCategoryRules();
            InitializeDefaultRules();
        }

        private void InitializeDefaultRules()
        {
            var defaults = new Dictionary<string, string>
            {
                { "АТБ", "Продукти" },
                { "Сільпо", "Продукти" },
                { "Ашан", "Продукти" },
                { "McDonald", "Ресторани" },
                { "KFC", "Ресторани" },
                { "АЗС", "Транспорт" },
                { "WOG", "Транспорт" },
                { "OKKO", "Транспорт" },
                { "Uber", "Транспорт" },
                { "Bolt", "Транспорт" },
                { "Аптека", "Здоров'я" },
                { "Pharmacy", "Здоров'я" },
                { "Netflix", "Розваги" },
                { "YouTube", "Розваги" },
                { "Steam", "Розваги" }
            };

            foreach (var rule in defaults.Where(r => !_rules.ContainsKey(r.Key)))
            {
                _rules[rule.Key] = rule.Value;
            }
        }

        public string CategorizeTransaction(Transaction transaction)
        {
            foreach (var rule in _rules)
            {
                if (transaction.Description.Contains(rule.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return rule.Value;
                }
            }

            return transaction.Amount >= 0 ? "Дохід" : "Інше";
        }

        public void LearnFromUserCorrection(string description, string category)
        {
            var keywords = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var keyword = keywords.FirstOrDefault(k => k.Length > 3);

            if (keyword != null && !_rules.ContainsKey(keyword))
            {
                _rules[keyword] = category;
                _db.SaveCategoryRule(keyword, category);
            }
        }
    }
}
