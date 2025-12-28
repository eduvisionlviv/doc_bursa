using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace doc_bursa.Models
{
    /// <summary>
    /// Рахунок, знайдений під час discovery-запиту до банку.
    /// </summary>
    public class DiscoveredAccount : INotifyPropertyChanged
    {
        private string id = Guid.NewGuid().ToString();
        private string displayName = string.Empty;
        private string? iban;
        private string? currency;
        private int? accountGroupId;
        private bool isVirtual;

        public string Id
        {
            get => id;
            set => SetField(ref id, value);
        }

        public string DisplayName
        {
            get => displayName;
            set => SetField(ref displayName, value);
        }

        public string? Iban
        {
            get => iban;
            set => SetField(ref iban, value);
        }

        public string? Currency
        {
            get => currency;
            set => SetField(ref currency, value);
        }

        /// <summary>
        /// Прив'язка до обраної групи рахунків.
        /// </summary>
        public int? AccountGroupId
        {
            get => accountGroupId;
            set => SetField(ref accountGroupId, value);
        }

        /// <summary>
        /// Позначка для штучних рахунків (наприклад, ручний імпорт).
        /// </summary>
        public bool IsVirtual
        {
            get => isVirtual;
            set => SetField(ref isVirtual, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}

