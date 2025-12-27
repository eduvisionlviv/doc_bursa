using System.Windows.Controls;
using doc_bursa.ViewModels;

namespace doc_bursa.Views
{
    public partial class SourcesView : UserControl
    {
        public SourcesView()
        {
            InitializeComponent();
            DataContext = new SourcesViewModel();
        }
    }
}
