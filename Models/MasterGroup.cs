using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        public ObservableCollection<string> AccountNumbers { get; set; }

        /// <summary>
        /// Загальна сума по всіх рахунках групи
        /// </summary>
        public decimal TotalBalance
        {
            get
            {
                // Буде обчислюватися через ViewModel на основі всіх рахунків
                return 0;
            }
        }

        /// <summary>
        /// Загальний оборот по дебету
        /// </summary>
        public decimal TotalDebit { get; set; }

        /// <summary>
        /// Загальний оборот по кредиту
        /// </summary>
        public decimal TotalCredit { get; set; }

        public MasterGroup()
        {
            AccountNumbers = new ObservableCollection<string>();
            CreatedDate = DateTime.Now;
            IsActive = true;
            Color = "#2196F3"; // Синій за замовчуванням
        }

        public event PropertyChangedEventHandler PropertyChanged;

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
