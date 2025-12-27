using System.Windows;
using System.Windows.Controls;
using doc_bursa.ViewModels;

namespace doc_bursa.Views
{
    public partial class GroupsView : UserControl
    {
        public GroupsView()
        {
            InitializeComponent();
            DataContext = new GroupsViewModel();
        }
    }
}


