using System.Windows.Controls;
using doc_bursa.ViewModels;

namespace doc_bursa.Views
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
