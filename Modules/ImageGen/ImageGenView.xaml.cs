using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Launcher.Core.Converters;
using Launcher.Core.Models;
using Launcher.Core.Services;
using Launcher.Modules.ImageGen.Autocomplete;
using Launcher.Modules.ImageGen.Extensions;
using Microsoft.Win32;

namespace Launcher.Modules.ImageGen;

public partial class ImageGenView : UserControl
{
    private readonly StableDiffusionService _sd = new();
    private readonly GalleryService _gallery = GalleryService.Instance;
    private readonly ImageGenSettings _settings = ImageGenSettings.Instance;

    private List<string> _allLoras = new();
    private readonly List<SdExtension> _extensions = SdExtensionCatalog.CreateAll();
    private bool _connected;
    private bool _firstConnectDone;
    private bool _generating;
    private bool _startingServer;
    private bool _loadedOnce;
    private bool _autoStartTried;
    private CancellationTokenSource? _genCts;
    private int _imageIndex;
    private int _imageTotal; // > 1 solo cuando se genera imagen a imagen (wildcards)

    public ImageGenView()
    {
        InitializeComponent();

        GalleryList.ItemsSource = _gallery.Images;

        BuildExtensionPanels();

        // Autocompletado en prompt y negativo: LoRAs siempre; wildcards y tags de Danbooru
        // solo cuando su extension esta activada.
        var loraProvider = new LoraAutocompleteProvider(() => _allLoras, LoraWeight);
        IEnumerable<IAutocompleteProvider> Providers() =>
            new IAutocompleteProvider[] { loraProvider }.Concat(
                _extensions.Where(x => x.IsEnabled && x.IsAvailable)
                    .Select(x => x.AutocompleteProvider)
                    .OfType<IAutocompleteProvider>());

        _ = new PromptAutocomplete(PromptBox, SuggestPopup, SuggestList, Providers);
        _ = new PromptAutocomplete(NegativeBox, SuggestPopup, SuggestList, Providers);

        // Al salir del modulo se guardan los ajustes de las extensiones (aunque no se haya generado).
        Unloaded += (_, _) =>
        {
            if (!_connected || !_firstConnectDone) return;
            _settings.LastParams = ReadParams();
            _settings.Save();
        };
        Library.CloseRequested += () => Library.Visibility = Visibility.Collapsed;
        Library.ReuseRequested += p =>
        {
            ApplyParams(p);
            Library.Visibility = Visibility.Collapsed;
        };

        ApplyParams(_settings.LastParams ?? new ImageGenParams());
        UpdatePreviewEmpty();

        StartServerButton.Visibility = Visibility.Visible;
        Loaded += ImageGenView_Loaded;
    }

    private async void ImageGenView_Loaded(object sender, RoutedEventArgs e)
    {
        // Loaded se dispara cada vez que la vista vuelve al arbol visual: solo conectamos la primera.
        if (_loadedOnce) return;
        _loadedOnce = true;

        if (_gallery.Images.Count > 0) GalleryList.SelectedIndex = 0;
        await ConnectAsync();
    }

    // ===================== Conexion con A1111 =====================

    private void SetServerStatus(string text) => ServerStatusText.Text = text;

    private async Task<bool> ConnectAsync()
    {
        _sd.BaseUrl = _settings.Url;
        SetServerStatus("conectando...");

        var state = await _sd.CheckAsync();
        if (state != SdServerState.Ready)
        {
            _connected = false;
            StartServerButton.Visibility = Visibility.Visible;
            SetServerStatus(state == SdServerState.NoApi
                ? "A1111 responde pero sin API. Anade --api a COMMANDLINE_ARGS en webui-user.bat y reinicialo."
                : $"servidor apagado ({_settings.Url}). Pulsa \"Iniciar servidor\" o arrancalo a mano.");

            // Arranque automatico (ajuste de instalacion): solo un intento por sesion.
            if (state == SdServerState.Offline && _settings.AutoStartServer && !_autoStartTried)
            {
                _autoStartTried = true;
                _ = StartServerAsync(askPath: false);
            }
            return false;
        }

        try
        {
            // Aprende la carpeta de instalacion y las carpetas personalizadas del propio servidor.
            _settings.ApplyServerFlags(await _sd.GetCmdFlagsAsync());

            var models = await _sd.GetModelsAsync();
            ModelCombo.ItemsSource = models;
            var current = await _sd.GetCurrentModelTitleAsync();
            ModelCombo.SelectedItem = models.FirstOrDefault(m => m.Title == current) ?? models.FirstOrDefault();

            var samplers = await _sd.GetSamplersAsync();
            SamplerCombo.ItemsSource = samplers;
            SamplerCombo.SelectedItem = samplers.Contains("Euler a") ? "Euler a" : samplers.FirstOrDefault();

            var schedulers = await _sd.GetSchedulersAsync();
            SchedulerCombo.ItemsSource = new[] { "Automatic" }.Concat(schedulers).ToList();
            SchedulerCombo.SelectedIndex = 0;
            SchedulerPanel.Visibility = schedulers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            _allLoras = await _sd.GetLorasAsync();
            FilterLoras();

            var keepVae = VaeCombo.SelectedItem as string;
            var vaes = await _sd.GetVaesAsync();
            VaeCombo.ItemsSource = new[] { "Automatic", "None" }.Concat(vaes).ToList();
            VaeCombo.SelectedItem = keepVae != null && VaeCombo.Items.Contains(keepVae) ? keepVae : "Automatic";

            var scripts = await _sd.GetAlwaysOnScriptsAsync();
            foreach (var extension in _extensions) await extension.InitializeAsync(_sd, scripts);

            // Los ultimos parametros usados solo se reaplican la primera vez, para no
            // pisar lo que el usuario este editando si pulsa "Recargar".
            if (!_firstConnectDone)
            {
                _firstConnectDone = true;
                ApplyParams(_settings.LastParams ?? ReadParams());
            }
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo cargar la informacion de A1111", ex);
            SetServerStatus($"error al leer datos del servidor: {ex.Message}");
            return false;
        }

        _connected = true;
        ConnectButton.Content = "Recargar";
        StartServerButton.Visibility = Visibility.Collapsed;
        SetServerStatus($"conectado · {ModelCombo.Items.Count} modelos · {_allLoras.Count} LoRAs");
        return true;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e) => await ConnectAsync();

    private async void StartServer_Click(object sender, RoutedEventArgs e) => await StartServerAsync(askPath: true);

    private async Task StartServerAsync(bool askPath)
    {
        if (_startingServer) return;

        var bat = _settings.EffectiveBatPath;
        if (!askPath && (string.IsNullOrWhiteSpace(bat) || !File.Exists(bat))) return;

        if (string.IsNullOrWhiteSpace(bat) || !File.Exists(bat))
        {
            var dialog = new OpenFileDialog
            {
                Title = "Selecciona el webui-user.bat de A1111",
                Filter = "Scripts (*.bat;*.cmd)|*.bat;*.cmd|Todos los archivos (*.*)|*.*"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

            bat = dialog.FileName;
            _settings.ServerBatPath = bat;
            if (string.IsNullOrWhiteSpace(_settings.InstallPath)) _settings.InstallPath = Path.GetDirectoryName(bat);
            _settings.Save();
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = bat,
                WorkingDirectory = Path.GetDirectoryName(bat),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo lanzar A1111", ex);
            SetServerStatus($"no se pudo lanzar el servidor: {ex.Message}");
            return;
        }

        _startingServer = true;
        StartServerButton.IsEnabled = false;

        try
        {
            SetServerStatus("arrancando A1111 (puede tardar un par de minutos)...");

            // Hasta ~3 minutos: cargar el checkpoint la primera vez es lento.
            for (var i = 0; i < 90; i++)
            {
                await Task.Delay(2000);
                if (await _sd.CheckAsync() == SdServerState.Ready)
                {
                    await ConnectAsync();
                    return;
                }
            }

            SetServerStatus("el servidor no ha respondido a tiempo. Revisa su ventana de consola y pulsa Conectar.");
        }
        finally
        {
            _startingServer = false;
            StartServerButton.IsEnabled = true;
        }
    }

    // ===================== Parametros <-> controles =====================

    private ImageGenParams ReadParams()
    {
        var model = ModelCombo.SelectedItem as SdModel;
        var scheduler = SchedulerPanel.Visibility == Visibility.Visible ? SchedulerCombo.SelectedItem as string : null;

        return new ImageGenParams
        {
            Prompt = PromptBox.Text.Trim(),
            NegativePrompt = NegativeBox.Text.Trim(),
            ModelTitle = model?.Title,
            ModelName = model?.Name,
            Sampler = SamplerCombo.SelectedItem as string ?? "Euler a",
            Scheduler = string.IsNullOrEmpty(scheduler) || scheduler == "Automatic" ? null : scheduler,
            Steps = (int)StepsSlider.Value,
            CfgScale = Math.Round(CfgSlider.Value, 1),
            Width = (int)WidthSlider.Value,
            Height = (int)HeightSlider.Value,
            Seed = long.TryParse(SeedBox.Text.Trim(), out var seed) ? seed : -1,
            BatchCount = ParseInt(BatchCountBox.Text, 1, 1, 100),
            BatchSize = ParseInt(BatchSizeBox.Text, 1, 1, 8),
            ClipSkip = (int)ClipSkipSlider.Value,
            Vae = SelectedVae(),
            ExtensionStates = CollectExtensionStates()
        };
    }

    private void ApplyParams(ImageGenParams p)
    {
        PromptBox.Text = p.Prompt;
        NegativeBox.Text = p.NegativePrompt;
        StepsSlider.Value = p.Steps;
        CfgSlider.Value = p.CfgScale;
        WidthSlider.Value = p.Width;
        HeightSlider.Value = p.Height;
        SeedBox.Text = p.Seed.ToString(CultureInfo.InvariantCulture);
        BatchCountBox.Text = p.BatchCount.ToString(CultureInfo.InvariantCulture);
        BatchSizeBox.Text = p.BatchSize.ToString(CultureInfo.InvariantCulture);
        ClipSkipSlider.Value = Math.Max(1, p.ClipSkip);

        var vae = p.Vae ?? "Automatic";
        if (VaeCombo.Items.Contains(vae)) VaeCombo.SelectedItem = vae;

        foreach (var extension in _extensions)
        {
            if (p.ExtensionStates.TryGetValue(extension.Id, out var state)) extension.LoadState(state);
            else if (extension.AffectsRequest) extension.IsEnabled = false;
        }

        if (!string.IsNullOrEmpty(p.ModelTitle) &&
            ModelCombo.Items.OfType<SdModel>().FirstOrDefault(m => m.Title == p.ModelTitle) is { } model)
        {
            ModelCombo.SelectedItem = model;
        }

        if (!string.IsNullOrEmpty(p.Sampler) && SamplerCombo.Items.Contains(p.Sampler))
        {
            SamplerCombo.SelectedItem = p.Sampler;
        }

        var scheduler = p.Scheduler ?? "Automatic";
        if (SchedulerCombo.Items.Contains(scheduler)) SchedulerCombo.SelectedItem = scheduler;
    }

    private static int ParseInt(string? text, int fallback, int min, int max) =>
        int.TryParse(text?.Trim(), out var value) ? Math.Clamp(value, min, max) : fallback;

    // ===================== LoRA / LyCORIS =====================

    private void FilterLoras()
    {
        if (LoraList == null) return;

        var filter = LoraFilterBox.Text.Trim();
        LoraList.ItemsSource = _allLoras
            .Where(n => filter.Length == 0 || n.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        LoraCountText.Text = _allLoras.Count == 0 ? "ninguno" : $"{_allLoras.Count} disponibles";
    }

    private void LoraFilterBox_TextChanged(object sender, TextChangedEventArgs e) => FilterLoras();

    private async void RefreshLoras_Click(object sender, RoutedEventArgs e)
    {
        if (!_connected) return;

        await _sd.RefreshLorasAsync();
        _allLoras = await _sd.GetLorasAsync();
        FilterLoras();
    }

    /// <summary>Inserta &lt;lora:nombre:peso&gt; en la posicion del cursor del prompt.
    /// Sirve igual para LyCORIS (A1111 1.5+ los trata como LoRA).</summary>
    private void LoraList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LoraList.SelectedItem is not string name) return;

        var weightText = LoraWeightBox.Text.Trim().Replace(',', '.');
        var weight = double.TryParse(weightText, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 0.8;

        var tag = $"<lora:{name}:{weight.ToString("0.##", CultureInfo.InvariantCulture)}>";

        var text = PromptBox.Text;
        var index = Math.Clamp(PromptBox.CaretIndex, 0, text.Length);
        if (index > 0 && !char.IsWhiteSpace(text[index - 1])) tag = " " + tag;

        PromptBox.Text = text.Insert(index, tag);
        PromptBox.CaretIndex = index + tag.Length;
        PromptBox.Focus();
    }

    // ===================== Generacion =====================

    private void SetGenerating(bool generating)
    {
        _generating = generating;
        GenerateButton.IsEnabled = !generating;
        CancelButton.IsEnabled = generating;
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_generating) return;

        if (!_connected)
        {
            ProgressText.Text = "conecta primero con el servidor";
            return;
        }

        var p = ReadParams();
        if (string.IsNullOrWhiteSpace(p.Prompt))
        {
            ProgressText.Text = "escribe un prompt";
            return;
        }

        _settings.LastParams = p;
        _settings.Save();

        SetGenerating(true);
        GenProgress.Value = 0;
        ProgressText.Text = "enviando...";

        using var pollCts = new CancellationTokenSource();
        var poll = PollProgressAsync(pollCts.Token);

        try
        {
            _genCts = new CancellationTokenSource();
            _imageIndex = 0;
            _imageTotal = 0;

            // Si alguna extension necesita un prompt distinto por imagen (wildcards), se genera
            // una a una; si no, todo el lote en una sola peticion como siempre.
            var perImage = _extensions.Any(x => x.IsEnabled && x.IsAvailable && x.VariesPerImage(p.Prompt, p.NegativePrompt));

            var results = await _sd.Txt2ImgAsync(p, ApplyExtensions, perImage,
                (index, total) => { _imageIndex = index; _imageTotal = total; }, _genCts.Token);

            // Paramos el sondeo antes de mostrar el resultado para que una
            // vista previa tardia no pise la imagen final.
            pollCts.Cancel();
            await poll;

            GeneratedImage? last = null;
            var enabledIds = _extensions.Where(x => x.IsEnabled && x.IsAvailable && x.AffectsRequest).Select(x => x.Id).ToHashSet();
            foreach (var result in results)
            {
                // Guardamos el prompt ya resuelto (wildcards...) y solo las extensiones realmente usadas.
                var stored = p.Clone();
                stored.Prompt = result.Prompt;
                stored.NegativePrompt = result.NegativePrompt;
                stored.BatchCount = 1;
                stored.BatchSize = 1;
                stored.ExtensionStates = p.ExtensionStates
                    .Where(kv => enabledIds.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                last = await _gallery.AddAsync(result.Data, stored, result.Seed);
            }

            if (last != null)
            {
                GalleryList.SelectedItem = last;
                GalleryList.ScrollIntoView(last);
                ProgressText.Text = $"{results.Count} imagen(es) · seed {string.Join(", ", results.Select(r => r.Seed))}";
            }
            else
            {
                ProgressText.Text = "cancelado, sin imagenes";
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Fallo la generacion txt2img", ex);
            ProgressText.Text = $"error: {ex.Message}";
        }
        finally
        {
            pollCts.Cancel();
            try { await poll; } catch { /* ya cancelado */ }

            GenProgress.Value = 0;
            _imageTotal = 0;
            _genCts?.Dispose();
            _genCts = null;
            SetGenerating(false);
        }
    }

    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!_generating) return;

        ProgressText.Text = "cancelando...";
        _genCts?.Cancel(); // evita lanzar las imagenes que faltan del lote
        await _sd.InterruptAsync();
    }

    /// <summary>Consulta /progress cada ~0.7 s mientras dura la peticion de txt2img.
    /// La vista previa en vivo solo llega si en A1111 esta activada la opcion
    /// "Show live previews" (Settings > Live previews).</summary>
    private async Task PollProgressAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var progress = await _sd.GetProgressAsync(ct);
                if (progress != null)
                {
                    var fraction = _imageTotal > 1 ? (_imageIndex + progress.Fraction) / _imageTotal : progress.Fraction;
                    GenProgress.Value = Math.Clamp(fraction * 100, 0, 100);

                    if (progress.TotalSteps > 0)
                    {
                        var job = _imageTotal > 1
                            ? $" · imagen {_imageIndex + 1}/{_imageTotal}"
                            : progress.JobCount > 1
                            ? $" · imagen {Math.Min(progress.Job + 1, progress.JobCount)}/{progress.JobCount}"
                            : "";
                        var eta = progress.EtaSeconds > 0 ? $" · ~{progress.EtaSeconds:0}s" : "";
                        ProgressText.Text = $"paso {progress.Step}/{progress.TotalSteps}{job}{eta}";
                    }
                    else
                    {
                        ProgressText.Text = "preparando...";
                    }

                    if (!string.IsNullOrEmpty(progress.CurrentImageBase64) &&
                        DecodeBase64(progress.CurrentImageBase64) is { } preview)
                    {
                        PreviewImage.Source = preview;
                        UpdatePreviewEmpty();
                    }
                }

                await Task.Delay(700, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Fin normal del sondeo.
        }
    }

    // ===================== Extensiones / biblioteca =====================

    private void BuildExtensionPanels()
    {
        foreach (var extension in _extensions)
        {
            ExtensionsHost.Children.Add(ExtensionShell.Create(extension));
        }
    }

    private Dictionary<string, string> CollectExtensionStates() =>
        _extensions.ToDictionary(e => e.Id, e => e.SaveState());

    /// <summary>Callback de Txt2ImgAsync: cada extension activa modifica la peticion.</summary>
    private void ApplyExtensions(SdRequest request)
    {
        foreach (var extension in _extensions.Where(e => e.IsEnabled && e.IsAvailable))
        {
            extension.Apply(request);
        }
    }

    private double LoraWeight()
    {
        var text = LoraWeightBox.Text.Trim().Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 0.8;
    }

    private string? SelectedVae() =>
        VaeCombo.SelectedItem is string vae && vae != "Automatic" ? vae : null;

    private void Library_Click(object sender, RoutedEventArgs e) => Library.Visibility = Visibility.Visible;

    private async void InstallSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ImageGenSettingsWindow { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();

        // La URL o la instalacion pueden haber cambiado: volvemos a conectar.
        if (dialog.Saved) await ConnectAsync();
    }

    // ===================== Preview / galeria =====================

    private static BitmapImage? DecodeBase64(string base64)
    {
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(base64));
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void UpdatePreviewEmpty() =>
        PreviewEmpty.Visibility = PreviewImage.Source == null ? Visibility.Visible : Visibility.Collapsed;

    private void GalleryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GalleryList.SelectedItem is not GeneratedImage image) return;

        // Reutilizamos el conversor existente para cargar la imagen a tamano completo.
        PreviewImage.Source = new PathToBitmapConverter()
            .Convert(image.FilePath, typeof(BitmapSource), null, CultureInfo.InvariantCulture) as BitmapSource;
        UpdatePreviewEmpty();
    }

    private static GeneratedImage GetImageFromMenuItem(object sender)
    {
        var menuItem = (MenuItem)sender;
        var contextMenu = (ContextMenu)menuItem.Parent;
        var owner = (FrameworkElement)contextMenu.PlacementTarget;
        return (GeneratedImage)owner.DataContext;
    }

    private void ReuseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var image = GetImageFromMenuItem(sender);

        // La seed real usada, no el -1 que se pidio, para poder reproducir la imagen.
        var p = image.Params.Clone();
        p.Seed = image.Seed;
        ApplyParams(p);
    }

    private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var image = GetImageFromMenuItem(sender);
        if (!image.Exists) return;

        try
        {
            Process.Start("explorer.exe", $"/select,\"{image.FilePath}\"");
        }
        catch (Exception ex)
        {
            LogService.Error("No se pudo abrir la carpeta de la imagen", ex);
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var image = GetImageFromMenuItem(sender);

        var result = MessageBox.Show(
            Window.GetWindow(this),
            "Se borrara esta imagen generada del disco. Continuar?",
            "Eliminar imagen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var wasSelected = ReferenceEquals(GalleryList.SelectedItem, image);
        _gallery.Remove(image);

        if (wasSelected)
        {
            if (_gallery.Images.Count > 0)
            {
                GalleryList.SelectedIndex = 0;
            }
            else
            {
                PreviewImage.Source = null;
                UpdatePreviewEmpty();
            }
        }
    }
}
