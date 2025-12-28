using System.Windows;
using System.Windows.Controls;
using doc_bursa.Models;
using doc_bursa.ViewModels;

namespace doc_bursa.Views
{
    public partial class TransactionsView : UserControl
    {
        public TransactionsView()
        {
            InitializeComponent();
            DataContext = new TransactionsViewModel();
        }

        private void TransactionsTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is TransactionsViewModel vm && e.NewValue is Transaction tx)
            {
                vm.SelectedTransaction = tx;
            }
        }
    }
}
