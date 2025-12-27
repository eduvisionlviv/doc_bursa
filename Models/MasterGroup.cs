using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace doc_bursa.Models
{
    /// <summary>
    /// Майстер група для об'єднання декількох рахунків в одну групу для спільної аналітики
    /// </summary>
    public class MasterGroup : INotifyPropertyChanged
    {
        private int _id;
        private string _name;
        private string _description;
        private DateTime _createdDate;
        private bool _isActive;
        private string _color;

        [Key]
        public int Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public DateTime CreatedDate
        {
            get => _createdDate;
            set
            {
                if (_createdDate != value)
                {
                    _createdDate = value;
                    OnPropertyChanged(nameof(CreatedDate));
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        public string Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnPropertyChanged(nameof(Color));
                }
            }
        }

        /// <summary>
        /// Колекція рахунків в групі
        /// </summary>
        [NotMapped]
        public ObservableCollection<string> AccountNumbers { get; set; }

        /// <summary>
        /// Посилання на зв'язки з групами рахунків.
        /// </summary>
        public ICollection<MasterGroupAccountGroup> AccountGroupLinks { get; set; } = new List<MasterGroupAccountGroup>();

        /// <summary>
        /// Загальна сума по всіх рахунках групи
        /// </summary>
        private decimal _totalBalance;
        public decimal TotalBalance
        {
            get => _totalBalance;
            set
            {
                if (_totalBalance != value)
                {
                    _totalBalance = value;
                    OnPropertyChanged(nameof(TotalBalance));
                }
            }
        }

        /// <summary>
        /// Загальний оборот по дебету
        /// </summary>
        private decimal _totalDebit;
        public decimal TotalDebit
        {
            get => _totalDebit;
            set
            {
                if (_totalDebit != value)
                {
                    _totalDebit = value;
                    OnPropertyChanged(nameof(TotalDebit));
                }
            }
        }

        /// <summary>
        /// Загальний оборот по кредиту
        /// </summary>
        private decimal _totalCredit;
        public decimal TotalCredit
        {
            get => _totalCredit;
            set
            {
                if (_totalCredit != value)
                {
                    _totalCredit = value;
                    OnPropertyChanged(nameof(TotalCredit));
                }
            }
        }

        public MasterGroup()
        {
            AccountNumbers = new ObservableCollection<string>();
            CreatedDate = DateTime.Now;
            IsActive = true;
            Color = "#2196F3"; // Синій за замовчуванням
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Додати рахунок до групи
        /// </summary>
        public void AddAccount(string accountNumber)
        {
            if (!string.IsNullOrWhiteSpace(accountNumber) && !AccountNumbers.Contains(accountNumber))
            {
                AccountNumbers.Add(accountNumber);
                OnPropertyChanged(nameof(AccountNumbers));
            }
        }

        /// <summary>
        /// Видалити рахунок з групи
        /// </summary>
        public void RemoveAccount(string accountNumber)
        {
            if (AccountNumbers.Contains(accountNumber))
            {
                AccountNumbers.Remove(accountNumber);
                OnPropertyChanged(nameof(AccountNumbers));
            }
        }

        /// <summary>
        /// Перевірка чи рахунок входить в групу
        /// </summary>
        public bool ContainsAccount(string accountNumber)
        {
            return AccountNumbers.Contains(accountNumber);
        }
    }
}