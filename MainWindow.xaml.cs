using System.Windows;
using doc_bursa.ViewModels;

namespace doc_bursa
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}

