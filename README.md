# FinDesk

Автономний Windows десктопний застосунок для управління особистими фінансами.

## Функціонал

- Синхронізація з Monobank, PrivatBank, Ukrsibbank через API
- Імпорт виписок CSV/XLSX
- Автоматична категоризація транзакцій
- Візуалізація доходів, витрат та балансу
- Фільтрація по періодах та категоріях

## Технології

- .NET 8.0
- WPF
- SQLite
- MVVM (CommunityToolkit.Mvvm)

## Збірка



dotnet build
dotnet run

## 🎯 Можливості

### 📊 Аналітика та Звіти
- 📈 Cash Flow аналіз
- 📊 Тренди та прогнози
- 📋 Бюджетування з алертами
- 📄 Експорт у Excel, PDF, CSV
- 🔍 Розширений пошук та фільтри

### 📋 Управління Рахунками
- 🏛️ Групування рахунків
- 🔗 Майстер-групи (об’єднання груп)
- 🔄 Періодичні платежі
- 🧾 Автоматичне видалення дублікатів

### 🔒 Безпека
- 🔐 Шифрування API токенів (DPAPI)
- ✅ Валідація вхідних даних
- 🛡️ Обробка помилок

## 📦 Архітектура

```
FinDesk/
├── Models/          # Моделі даних
│   ├── Account.cs
│   ├── AccountGroup.cs
│   ├── Budget.cs
│   ├── MasterGroup.cs
│   └── RecurringTransaction.cs
├── Services/       # Бізнес-логіка
│   ├── AnalyticsService.cs
│   ├── DatabaseService.cs
│   ├── ExportService.cs
│   └── SearchService.cs
├── ViewModels/     # MVVM ViewModels
│   ├── AnalyticsViewModel.cs
│   ├── DashboardViewModel.cs
│   └── GroupsViewModel.cs
└── Views/          # WPF Інтерфейс
    ├── DashboardView.xaml
    ├── GroupsView.xaml
    └── TransactionsView.xaml
```

## 🚀 Статус Проекту

✅ **Критичні помилки**: ВИПРАВЛЕНО  
✅ **Моделі**: 100% (8/8)  
✅ **Сервіси**: 100% (7/7)  
✅ **ViewModels**: 100% (7/7)  
✅ **Views**: 75% (3/4)  

**Загальний прогрес**: ~70% ✅✅✅✅✅✅✅⬜⬜⬜

## 📝 Ліцензія

MIT License - див. LICENSE файл

## 👥 Автори

eduvisionlviv - Розробка та підтримка

---

🌟 **FinDesk** - Потужний інструмент для управління вашими фінансами!
