using System.Net.Http;
using System.Windows.Controls;
using doc_bursa.Services;
using doc_bursa.ViewModels;

namespace doc_bursa.Views
{
    public partial class SourcesView : UserControl
    {
        public SourcesView()
        {
            InitializeComponent();
            // Не створюємо DataContext тут, бо він передається з MainViewModel
            // DataContext = new SourcesViewModel(new MonobankService(new HttpClient()), new PrivatBankService());
        }
    }
}
