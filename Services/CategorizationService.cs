using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using doc_bursa.Models;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace doc_bursa.Services
{
    /// <summary>
    /// Гібридна категоризація транзакцій: спершу regex-правила, далі ML.NET text classification.
    /// </summary>
    public class CategorizationService
    {
        private readonly DatabaseService _db;
        private readonly MLContext _mlContext;
        private readonly LruCache<string, string> _predictionCache;
        private readonly List<(Regex pattern, string category)> _regexRules;
        private ITransformer? _model;
        private PredictionEngine<TransactionTextData, TransactionCategoryPrediction>? _predictionEngine;

        public CategorizationService(DatabaseService db)
        {
            _db = db;
            _mlContext = new MLContext(seed: 42);
            _predictionCache = new LruCache<string, string>(capacity: 1024);
            _regexRules = BuildRegexRules();
            TrainModel();
        }

        /// <summary>
        /// Категоризує транзакцію за гібридним підходом: regex → ML.
        /// </summary>
        public string CategorizeTransaction(Transaction transaction)
        {
            if (TryRegex(transaction.Description, out var category))
            {
                return category;
            }

            var cacheKey = transaction.Description.ToLowerInvariant();
            if (_predictionCache.TryGet(cacheKey, out var cachedCategory))
            {
                return cachedCategory;
            }

            var predicted = Predict(transaction.Description, transaction.Amount);
            _predictionCache.AddOrUpdate(cacheKey, predicted);
            return predicted;
        }

        /// <summary>
        /// Навчання ML.NET моделі на історичних транзакціях (опис + сума → категорія).
        /// </summary>
        public void TrainModel()
        {
            var trainingSet = BuildTrainingSet();
            if (trainingSet.Count == 0)
            {
                _model = null;
                _predictionEngine = null;
                return;
            }

            var data = _mlContext.Data.LoadFromEnumerable(trainingSet);

            var pipeline = _mlContext.Transforms.Text.FeaturizeText("DescriptionFeaturized", nameof(TransactionTextData.Description))
                .Append(_mlContext.Transforms.NormalizeMinMax("AmountScaled", nameof(TransactionTextData.Amount)))
                .Append(_mlContext.Transforms.Concatenate("Features", "DescriptionFeaturized", "AmountScaled"))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TransactionTextData.Category)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            _model = pipeline.Fit(data);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<TransactionTextData, TransactionCategoryPrediction>(_model);
        }

        /// <summary>
        /// Додає нове правило на основі виправлення користувача та розширює кеш.
        /// </summary>
        public void LearnFromUserCorrection(string description, string category)
        {
            if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(category))
            {
                return;
            }

            var escaped = Regex.Escape(description.Trim());
            var regex = new Regex($"{escaped}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            _regexRules.Add((regex, category));
            _db.SaveCategoryRule(description.Trim(), category);
            _predictionCache.AddOrUpdate(description.ToLowerInvariant(), category);
        }

        private bool TryRegex(string description, out string category)
        {
            foreach (var (pattern, cat) in _regexRules)
            {
                if (pattern.IsMatch(description))
                {
                    category = cat;
                    return true;
                }
            }

            category = string.Empty;
            return false;
        }

        private string Predict(string description, decimal amount)
        {
            if (_predictionEngine == null)
            {
                return amount >= 0 ? "Дохід" : "Інше";
            }

            var input = new TransactionTextData
            {
                Description = description,
                Amount = (float)amount
            };

            var prediction = _predictionEngine.Predict(input);
            return prediction?.Category ?? (amount >= 0 ? "Дохід" : "Інше");
        }

        private List<(Regex pattern, string category)> BuildRegexRules()
        {
            var rules = new List<(string pattern, string category)>
            {
                ("АТБ|ATB", "Продукти"),
                ("Сільпо|Silpo", "Продукти"),
                ("Ашан|Ashan", "Продукти"),
                ("Novus", "Продукти"),
                ("Фора", "Продукти"),
                ("Metro Cash", "Продукти"),
                ("Rozetka|Розетка", "Техніка"),
                ("Comfy", "Техніка"),
                ("Епіцентр|Epicentr", "Дім"),
                ("OLX|Prom", "Шопінг"),
                ("Amazon|Ebay", "Шопінг"),
                ("IKEA", "Дім"),
                ("McDonald|KFC|Burger King", "Ресторани"),
                ("Starbucks|Coffee|Кава", "Кава"),
                ("OKKO|WOG|UPG|ANP|АЗС", "Транспорт"),
                ("Uber|Bolt|Uklon|Taxi|Таксі", "Транспорт"),
                ("Metro\\s*\\d+|Subway", "Транспорт"),
                ("Railway|Укрзалізниця|Потяг", "Транспорт"),
                ("Аеропорт|Airport|Airlines|Wizz|Ryanair", "Подорожі"),
                ("Аптека|Pharmacy|Аптек", "Здоров'я"),
                ("Doctor|Clinic|Клінік", "Здоров'я"),
                ("Dent|Стоматолог", "Здоров'я"),
                ("Sport|Gym|Фітнес|Спортзал", "Спорт"),
                ("Nike|Adidas|Decathlon", "Спорт"),
                ("Cinema|Кіно|IMAX|Multiplex", "Розваги"),
                ("Netflix|YouTube|Spotify|Apple Music|Steam", "Розваги"),
                ("Concert|Концерт", "Розваги"),
                ("School|Університет|Course|Курс|Udemy|Coursera", "Освіта"),
                ("Kindergarten|Дитсадок", "Діти"),
                ("Zoo|Зоопарк", "Розваги"),
                ("Utility|Комунал|Газ|Світло|Вода|Обленерго", "Комунальні"),
                ("Інтернет|Internet|ISP|Fiber", "Зв'язок"),
                ("Mobile|Київстар|Vodafone|Lifecell", "Зв'язок"),
                ("Insurance|Страхов", "Страхування"),
                ("Hotel|Booking|Готель", "Подорожі"),
                ("Parking|Паркінг|Парковка", "Транспорт"),
                ("Carwash|Мийка", "Авто"),
                ("Service|СТО|Автосервіс", "Авто"),
                ("Gift|Подарунок", "Подарунки"),
                ("Charity|Благодійність|Donat", "Благодійність"),
                ("Bank fee|Комісія|Fee", "Банківські комісії"),
                ("Salary|Зарплата|Payroll", "Дохід"),
                ("Bonus|Бонус", "Дохід"),
                ("Transfer|Переказ", "Перекази"),
                ("Crypto|BTC|ETH|Binance", "Крипто"),
                ("Repair|Ремонт", "Дім"),
                ("Furniture|Меблі", "Дім"),
                ("Garden|Сад|City\\s*Market", "Дім"),
                ("Taxi\\s*way|Shuttle", "Транспорт"),
                ("Fuel|Дизель|Бензин", "Транспорт"),
                ("Beauty|Салон|Манікюр|Перукарня", "Краса"),
                ("Pets|Zoo|Ветклініка|Корм", "Тварини"),
                ("Hosting|Domain|AWS|Azure|GCP", "Онлайн сервіси"),
                ("Hardware|Software|ПЗ|Ліцензія", "Техніка"),
                ("Courier|Delivery|Доставка|Нова Пошта|Укрпошта", "Доставка"),
                ("Loan|Credit|Кредит", "Кредити"),
                ("Tax|Податок", "Податки"),
                ("Restaurant|Ресторан|Bar|Бар|Pub", "Ресторани"),
                ("Cafe|Кафе|Bistro", "Ресторани")
            };

            var storedRules = _db.GetCategoryRules();
            foreach (var rule in storedRules)
            {
                rules.Add((Regex.Escape(rule.Key), rule.Value));
            }

            return rules.Select(r => (new Regex(r.pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), r.category)).ToList();
        }

        private sealed class TransactionTextData
        {
            public string Description { get; set; } = string.Empty;
            public float Amount { get; set; }
            public string Category { get; set; } = string.Empty;
        }

        private sealed class TransactionCategoryPrediction
        {
            [ColumnName("PredictedLabel")]
            public string Category { get; set; } = string.Empty;
        }

        private List<TransactionTextData> BuildTrainingSet()
        {
            var transactions = _db.GetTransactions();
            var dataset = transactions.Select(t => new TransactionTextData
            {
                Description = t.Description,
                Amount = (float)t.Amount,
                Category = string.IsNullOrWhiteSpace(t.Category) ? "Інше" : t.Category
            }).ToList();

            if (dataset.Count >= 500)
            {
                return dataset;
            }

            var syntheticSeeds = new List<(string desc, float amount, string category)>
            {
                ("АТБ покупка продуктів", -250, "Продукти"),
                ("OKKO паливо", -1200, "Транспорт"),
                ("Netflix subscription", -299, "Розваги"),
                ("Зарплата компанія", 30000, "Дохід"),
                ("Оренда квартири", -10000, "Дім"),
                ("Uklon поїздка", -200, "Транспорт"),
                ("Аптека здоров'я", -400, "Здоров'я"),
                ("Sport club абонемент", -800, "Спорт"),
                ("Комісія банку", -50, "Банківські комісії"),
                ("Бонус за проект", 5000, "Дохід")
            };

            var random = new Random(42);
            while (dataset.Count < 500)
            {
                var sample = syntheticSeeds[random.Next(syntheticSeeds.Count)];
                var jitter = (float)(random.NextDouble() * 20 - 10);
                dataset.Add(new TransactionTextData
                {
                    Description = sample.desc,
                    Amount = sample.amount + jitter,
                    Category = sample.category
                });
            }

            return dataset;
        }
    }
}



