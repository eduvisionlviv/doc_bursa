using System.Windows.Controls;
using FinDesk.ViewModels;

namespace FinDesk.Views
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
