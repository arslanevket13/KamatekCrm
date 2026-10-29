using System;
using System.Windows;

namespace KamatekCrm.Views.DevCenter
{
    /// <summary>
    /// Geliştirici Merkezi penceresi. Yalnızca <see cref="ViewModels.DevCenter.DevCenterViewModel"/>
    /// yetki kapısından geçtiyse açılır; pencere kapanınca sekmelerin zamanlayıcıları durdurulur.
    /// </summary>
    public partial class DevCenterWindow : Window
    {
        private readonly ViewModels.DevCenter.DevCenterViewModel _viewModel;

        public DevCenterWindow(ViewModels.DevCenter.DevCenterViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;

            Loaded += async (_, _) => await _viewModel.InitializeAsync();
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.Dispose();
            base.OnClosed(e);
        }
    }
}
