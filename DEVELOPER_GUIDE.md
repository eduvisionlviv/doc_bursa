# FinDesk — Короткий посібник розробника

## Архітектура
- **Services/**: бізнес-логіка (Analytics, Budget, RecurringTransaction, Export, Import, Monobank тощо).
- **Infrastructure/Data**: робота з SQLite (`DatabaseService`) та репозиторіями EF (якщо використовуються).
- **Views/ViewModels/**: WPF MVVM, LiveCharts для графіків, темна тема у `App.xaml`.
- **Tests/**: unit-тести xUnit, покривають сервіси та імпорти.

### Ключові сервіси
- `AnalyticsService`: статистика, тренди (Daily/Weekly/Monthly), прогноз на лінійній регресії, аномалії (Z-score), кешування через `MemoryCache`.
- `BudgetService` + `BudgetAnalyzer`: облік витрат проти лімітів, алерти.
- `RecurringTransactionService`: CRUD рекурентних записів, планувальник на `Timer`, викликає `TransactionService`.
- `ExportService`: асинхронний експорт CSV/Excel/PDF; використовує `ReportRow`/`ReportResult`.
- `DatabaseService`: SQLite таблиці для транзакцій, бюджетів, рекурентних транзакцій; методи `GetRecurringTransactions`, `SaveRecurringTransaction`.

## Робочі сценарії
- **Рекурентні транзакції**: `RecurringTransactionService.Start()` запускає таймер; `ProcessDueAsync` створює транзакції зі статусом "Recurring".
- **Аналітика**: `AnalyticsService.GetTrend(account, granularity, from, to)` та `ForecastBalance` для прогнозу.
- **Експорт**: `ExportService.ExportReportAsync(result, path, format, options)` підтримує фільтри/колонки.

## Тестування
- Запуск: `dotnet test`.
- Нові тести: додавайте у `FinDesk.Tests/` — використовувати тимчасові файли/БД (`Path.GetTempPath()`).

## Діаграми (текстові)
- **Контекст**: Користувач ⇄ WPF UI ⇄ Services ⇄ SQLite.
- **Компоненти**: UI (Views/ViewModels) → Services (Analytics/Budget/Recurring/Export) → DatabaseService → SQLite.
- **Потік рекурентних**: Timer → RecurringTransactionService.ProcessDueAsync → TransactionService.AddTransaction → DatabaseService.SaveTransaction.

## Coding style
- Nullable ввімкнено; уникайте try/catch навколо import-ів (див. загальні правила).
- Використовуйте `MemoryCache` для повторних розрахунків аналітики.
- Для довгих операцій — `async/await` або `Task.Run`, щоб не блокувати UI.
