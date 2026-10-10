using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Launcher.Core.Models;

namespace Launcher.Modules.Commands;

public partial class CommandSuggestionsWindow : Window
{
    public class SuggestionItem : INotifyPropertyChanged
    {
        public CommandShortcut Template { get; }
        public string Name => Template.Name;
        public string Description => Template.Description;

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnChanged(); } }

        public SuggestionItem(CommandShortcut template) => Template = template;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public ObservableCollection<SuggestionItem> Items { get; }

    /// <summary>Rellenado con los CommandShortcut seleccionados si el usuario confirma.</summary>
    public List<CommandShortcut> Result { get; } = new();

    public CommandSuggestionsWindow(IEnumerable<CommandShortcut> existing)
    {
        InitializeComponent();

        var existingTargets = existing
            .Select(c => c.Target)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Items = new ObservableCollection<SuggestionItem>(
            CommandSuggestions.GetAll()
                .Where(s => !existingTargets.Contains(s.Target))
                .Select(s => new SuggestionItem(s)));

        SuggestionsList.ItemsSource = Items;
    }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        Result.AddRange(Items.Where(i => i.IsSelected).Select(i => i.Template));
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}