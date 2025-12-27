using System.Windows.Controls;
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
    }
}

