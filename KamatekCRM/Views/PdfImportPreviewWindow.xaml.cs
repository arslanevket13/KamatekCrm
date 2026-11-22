using System.Windows;
using KamatekCrm.Helpers;

namespace KamatekCrm.Views
{
    public partial class PdfImportPreviewWindow : Window
    {
        public PdfImportPreviewWindow()
        {
            InitializeComponent();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            WindowControlHelper.SetupWindowControls(this);
        }
    }
}
