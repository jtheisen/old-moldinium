namespace SampleApp
{
    using System.Windows.Controls;
    using static Global;

    public partial class CartEditor : UserControl
    {
        public CartEditor()
        {
            InitializeComponent();

            Content.Content = Create<CartViewModel>();
        }
    }
}
