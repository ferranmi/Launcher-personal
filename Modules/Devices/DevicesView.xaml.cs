using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Microsoft.Win32;

namespace Launcher.Modules.Devices;

public partial class DevicesView : UserControl
{
    private string _search = "";
    private bool _refreshing;
    private ListCollectionView? _view;

    public DevicesView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;

        StatusText.Text = "> buscando dispositivos_";
        StatusText.Visibility = Visibility.Visible;

        List<DeviceInfo> devices;
        try
        {
            devices = await DeviceService.QueryAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la lectura de dispositivos", ex);
            devices = new List<DeviceInfo>();
        }

        var view = new ListCollectionView(devices);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DeviceInfo.Group)));
        view.SortDescriptions.Add(new SortDescription(nameof(DeviceInfo.Group), ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription(nameof(DeviceInfo.Name), ListSortDirection.Ascending));
        view.Filter = o => o is DeviceInfo d &&
            (string.IsNullOrWhiteSpace(_search) ||
             d.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
             d.Manufacturer.Contains(_search, StringComparison.OrdinalIgnoreCase));

        _view = view;
        DevicesItems.ItemsSource = view;
        UpdateStatus();

        _refreshing = false;
    }

    private void UpdateStatus()
    {
        var empty = _view == null || _view.IsEmpty;
        StatusText.Text = "> sin dispositivos_";
        StatusText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text;
        _view?.Refresh();
        UpdateStatus();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void DeviceManager_Click(object sender, RoutedEventArgs e) => Open("devmgmt.msc");

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DeviceInfo { SettingsUri: { } uri }) Open(uri);
    }

    private async void AppButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not DeviceInfo device || device.VendorKeyword == null) return;

        if (device.VendorPath != null)
        {
            Open(device.VendorPath);
            return;
        }

        // No esta en la ruta habitual: que el usuario indique donde esta y se recuerda.
        var dialog = new OpenFileDialog
        {
            Title = $"Selecciona el ejecutable de {device.VendorLabel}",
            Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        DeviceService.SetVendorApp(device.VendorKeyword, dialog.FileName);
        await RefreshAsync();
    }

    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Error($"No se pudo abrir {target}", ex);
        }
    }
}
