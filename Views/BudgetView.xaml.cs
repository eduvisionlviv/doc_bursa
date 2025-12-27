using System.Windows.Controls;
using FinDesk.ViewModels;

namespace FinDesk.Views
{
    public partial class BudgetView : UserControl
    {
        public BudgetView()
        {
            InitializeComponent();
            DataContext = new BudgetViewModel();
        }
    }
}

