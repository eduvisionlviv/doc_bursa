using System.Windows.Controls;
using FinDesk.ViewModels;

namespace FinDesk.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
        }
    }
}
