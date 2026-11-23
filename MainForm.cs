using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

public class MainForm : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };

    public MainForm()
    {
        Text = "KhoViet - Quản lý kho";
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        Controls.Add(_web);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KhoViet", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, folder);
            await _web.EnsureCoreWebView2Async(env);
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _web.CoreWebView2.NewWindowRequested += (s, a) => { a.Handled = true; _web.CoreWebView2.Navigate(a.Uri); };
            _web.CoreWebView2.Navigate("http://localhost:5000");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không mở được WebView2: " + ex.Message, "KhoViet");
        }
    }
}
