using System.Windows;
using System.Windows.Input;
using Launcher.Core.Models;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Launcher.Modules.Commands;

public partial class AddEditCommandWindow : Window
{
    private readonly CommandShortcut? _editingCommand;

    public CommandShortcut? Result { get; private set; }

    /// <summary>Constructor para anadir un comando nuevo.</summary>
    public AddEditCommandWindow()
    {
        InitializeComponent();
    }

    /// <summary>Constructor para editar un comando existente.</summary>
    public AddEditCommandWindow(CommandShortcut command) : this()
    {
        _editingCommand = command;
        HeaderText.Text = "> Editar comando";

        NameBox.Text = command.Name;
        CategoryCombo.SelectedIndex = (int)command.Category;
        TargetBox.Text = command.Target;
        ArgumentsBox.Text = command.Arguments;
        WorkingDirBox.Text = command.WorkingDirectory ?? "";
        IconBox.Text = command.Icon;
        TerminalCombo.SelectedIndex = (int)command.Terminal;
        RunAsAdminCheck.IsChecked = command.RunAsAdmin;
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecciona el ejecutable",
            Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            TargetBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void BrowseWorkingDir_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog { Description = "Selecciona la carpeta de trabajo" };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            WorkingDirBox.Text = dialog.SelectedPath;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "El comando necesita un nombre.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var target = TargetBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(target))
        {
            MessageBox.Show(this, "Indica el comando o la ruta del ejecutable.", "Falta informacion",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!CommandsService.TargetExists(target))
        {
            var proceed = MessageBox.Show(
                this,
                "No se ha encontrado ese comando o ruta en este equipo. ¿Guardarlo igualmente?",
                "No se ha podido verificar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (proceed != MessageBoxResult.Yes) return;
        }

        var command = _editingCommand ?? new CommandShortcut();
        command.Name = NameBox.Text.Trim();
        command.Category = (CommandCategory)CategoryCombo.SelectedIndex;
        command.Target = target;
        command.Arguments = ArgumentsBox.Text.Trim();
        command.WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirBox.Text) ? null : WorkingDirBox.Text.Trim();
        command.Icon = IconBox.Text.Trim();
        command.Terminal = (TerminalHost)TerminalCombo.SelectedIndex;
        command.RunAsAdmin = RunAsAdminCheck.IsChecked == true;

        Result = command;
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