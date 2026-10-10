using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Modules.Assistant;

public partial class AssistantWindow : Window
{
    private readonly OllamaService _ollama = new();
    private readonly List<AssistantMessage> _history = new();
    private bool _isStreaming;

    public AssistantWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => InputBox.Focus();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Se cierra al perder el foco, como una paleta de comandos tipo
        // Spotlight/PowerToys Run. Si prefieres que se quede abierta en
        // segundo plano, quita esta linea (y el evento Deactivated del XAML).
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelCombo.SelectedItem is ComboBoxItem item)
        {
            _ollama.Model = item.Content?.ToString() ?? "qwen3.5:4b";
        }
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Send_Click(sender, e);
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (_isStreaming) return;

        var text = InputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        InputBox.Clear();
        AppendMessage("user", text);
        _history.Add(new AssistantMessage { Role = "user", Content = text });

        var assistantBubble = AppendMessage("assistant", "");
        _isStreaming = true;

        var full = new StringBuilder();
        try
        {
            await foreach (var chunk in _ollama.StreamChatAsync(_history))
            {
                full.Append(chunk);
                assistantBubble.Text = full.ToString();
                HistoryScroll.ScrollToEnd();
            }
        }
        catch (Exception ex)
        {
            full.Clear();
            full.Append("[No se ha podido contactar con Ollama. Comprueba que este corriendo (ollama serve) en localhost:11434]");
            assistantBubble.Text = full.ToString();
            System.Diagnostics.Debug.WriteLine($"[AssistantWindow] Error de Ollama: {ex.Message}");
        }
        finally
        {
            _isStreaming = false;
        }

        _history.Add(new AssistantMessage { Role = "assistant", Content = full.ToString() });
    }

    private TextBlock AppendMessage(string role, string content)
    {
        var isUser = role == "user";

        var bubble = new Border
        {
            Margin = new Thickness(isUser ? 60 : 0, 0, isUser ? 0 : 60, 8),
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(6),
            Background = (System.Windows.Media.Brush)FindResource(isUser ? "Brush.AccentDim" : "Brush.Surface"),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };

        var textBlock = new TextBlock
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = (System.Windows.Media.FontFamily)FindResource("Font.Primary"),
            FontSize = 13,
            Foreground = (System.Windows.Media.Brush)FindResource("Brush.TextPrimary")
        };

        bubble.Child = textBlock;
        HistoryPanel.Children.Add(bubble);
        HistoryScroll.ScrollToEnd();
        return textBlock;
    }
}