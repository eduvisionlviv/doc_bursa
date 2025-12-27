using System.Windows;
using System.Windows.Controls;
using FinDesk.ViewModels;

namespace FinDesk.Views
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


