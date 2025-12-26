using System.Windows.Controls;
using FinDesk.ViewModels;

namespace FinDesk.Views
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
