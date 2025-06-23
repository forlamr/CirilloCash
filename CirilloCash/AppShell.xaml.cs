namespace CirilloCash
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(TransactionsPage), typeof(TransactionsPage));
        }
    }
}
