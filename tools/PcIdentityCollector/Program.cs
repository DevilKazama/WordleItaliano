using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WordleItaliano.PcIdentityCollector;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--self-test" })) return SelfTest();
        var app = new Application();
        var content = new StackPanel { Margin = new Thickness(20) };
        content.Children.Add(new TextBlock
        {
            Text = "Codice della postazione", FontSize = 20,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12)
        });
        content.Children.Add(new TextBlock
        {
            Text = "Raccogli questo codice insieme al nome del giocatore per autorizzare la postazione a Wordle Italiano.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16)
        });
        var code = new TextBox
        {
            IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"), FontSize = 16,
            Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 12)
        };
        content.Children.Add(code);
        var copy = new Button
        {
            Content = "Copia codice", Padding = new Thickness(16, 8, 16, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        content.Children.Add(copy);
        var status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0)
        };
        content.Children.Add(status);
        try { code.Text = PcIdentity.ReadCode(); }
        catch (InvalidOperationException error)
        {
            copy.IsEnabled = false;
            status.Text = error.Message;
        }
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(code.Text);
                status.Text = "Codice copiato.";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                status.Text = "Non riesco a copiare negli appunti. Seleziona il codice e usa Ctrl+C.";
            }
        };
        var window = new Window
        {
            Title = "Wordle Italiano - Codice PC", Width = 520,
            SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = content
        };
        return app.Run(window);
    }

    private static int SelfTest()
    {
        try
        {
            const string fixture = "01234567-89ab-cdef-0123-456789abcdef";
            var code = PcIdentity.FromIdentifier(fixture);
            if (code != PcIdentity.FromIdentifier(" {01234567-89AB-CDEF-0123-456789ABCDEF} ")
                || code.Length != 70 || !code.StartsWith("WIPC1-", StringComparison.Ordinal)
                || code == PcIdentity.FromIdentifier("11234567-89ab-cdef-0123-456789abcdef")) return 1;
            foreach (var invalid in new string?[] { null, "", "not-a-guid", Guid.Empty.ToString() })
            {
                try { PcIdentity.ReadCode(() => invalid); return 2; }
                catch (InvalidOperationException) { }
            }
            try { PcIdentity.ReadCode(() => throw new UnauthorizedAccessException()); return 3; }
            catch (InvalidOperationException) { }
            if (PcIdentity.ReadCode() != PcIdentity.ReadCode()) return 4;
            return 0;
        }
        catch { return 5; }
    }
}
