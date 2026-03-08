using System.Windows;

namespace BeyondSosariaLauncher;

public partial class AddServerDialog : Window
{
    public ServerEntry? Result { get; private set; }

    public AddServerDialog()
    {
        InitializeComponent();
    }

    private void AddBtn_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var host = HostBox.Text.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(host))
        {
            MessageBox.Show("Name and Host are required.", "Add Server", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PortBox.Text.Trim(), out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Port must be a number between 1 and 65535.", "Add Server", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new ServerEntry { Name = name, Host = host, Port = port };
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
