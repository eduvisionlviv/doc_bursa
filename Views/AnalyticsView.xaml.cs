using System.Windows.Controls;
using doc_bursa.ViewModels;

namespace doc_bursa.Views
{
    public partial class AnalyticsView : UserControl
    {
        public AnalyticsView()
        {
            InitializeComponent();
            DataContext = new AnalyticsViewModel();
        }
    }
}
