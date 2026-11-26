using System;
using System.Windows;
using KamatekCrm.ViewModels;

namespace KamatekCrm.Views;

public partial class BarcodeLabelPrintWindow : Window
{
    public BarcodeLabelPrintWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is BarcodeLabelPrintViewModel vm)
            {
                vm.RequestClose -= OnRequestClose;
                vm.RequestClose += OnRequestClose;
            }
        };
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        KamatekCrm.Helpers.WindowControlHelper.SetupWindowControls(this);
    }

    private void OnRequestClose(bool result)
    {
        try { DialogResult = result; } catch (Exception ex) { Serilog.Log.Debug(ex, "DialogResult ayarlanamadı: BarcodeLabelPrintWindow"); }
        Close();
    }
}
