using Brisa.Core;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Brisa.Setup;

internal sealed class SetupWindow : Form
{
    readonly TextBox destination = new() { Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BrisaDock"), BorderStyle = BorderStyle.None, Dock = DockStyle.Fill };
    readonly SetupCheckBox shortcut = new() { Text = "Crear un acceso en el menú Inicio", Checked = true };
    readonly SetupCheckBox launch = new() { Text = "Abrir Brisa al terminar", Checked = true };
    readonly Label status = new() { Text = "Instalación para tu usuario. Si ya tienes Brisa, tus ajustes se conservan.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    readonly RoundedButton install = new() { Text = "Instalar Brisa", Primary = true, Dock = DockStyle.Fill };
    readonly RoundedButton browse = new() { Text = "Elegir carpeta", Dock = DockStyle.Fill };
    readonly RoundedButton cancel = new() { Text = "Ahora no", Dock = DockStyle.Fill };
    readonly RoundedButton close = new() { Text = "×", Width = 36, Height = 36 };
    readonly SetupProgress progress = new() { Dock = DockStyle.Bottom, Height = 4, Visible = false };
    readonly Label statusMark = new() { Text = "i", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = SetupColors.Accent, Font = new Font("Segoe UI", 17, FontStyle.Bold) };
    readonly ToolTip tips = new();
    readonly Bitmap brand;
    readonly bool review;
    bool installing, installed;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref uint value, int size);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll", EntryPoint="SendMessageW")] static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);

    public SetupWindow(string[] args)
    {
        review = args.Any(a => a.StartsWith("--review", StringComparison.Ordinal));
        SuspendLayout();
        Text = review ? "Brisa · Vista previa del instalador" : "Instalar Brisa Dock";
        Font = new Font("Segoe UI", 10); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen; FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(680, 610); MinimumSize = new Size(580, 520);
        BackColor = SetupColors.Background; ForeColor = SetupColors.Text; MaximizeBox = false;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Brisa.Icon.png")!)
        using (var image = Image.FromStream(stream)) brand = new Bitmap(image);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new(SizeType.Absolute, 100)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 82));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0) };
        header.ColumnStyles.Add(new(SizeType.Absolute, 86)); header.ColumnStyles.Add(new(SizeType.Percent, 100)); header.ColumnStyles.Add(new(SizeType.Absolute, 38));
        var logo = new PictureBox { Image = brand, SizeMode = PictureBoxSizeMode.Zoom, Width = 72, Height = 72, Margin = new Padding(0, 0, 14, 0), AccessibleName = "Icono de Brisa" };
        header.Controls.Add(logo);
        var titles = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Margin = new Padding(0) };
        titles.RowStyles.Add(new(SizeType.Absolute, 44)); titles.RowStyles.Add(new(SizeType.Absolute, 24)); titles.RowStyles.Add(new(SizeType.Absolute, 22));
        var title = new HeaderLabel { Text = "Brisa Dock", AutoSize = true, Font = new Font("Segoe UI", 27, FontStyle.Bold), Margin = new Padding(0) };
        var subtitle = new HeaderLabel { Text = "Tu escritorio, a tu manera.", AutoSize = true, ForeColor = SetupColors.Muted, Margin = new Padding(0, 1, 0, 0) };
        var edition = new HeaderLabel { Text = "WINDOWS 11  ·  UN ESPACIO A TU MEDIDA", AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = SetupColors.Accent, Margin = new Padding(0, 4, 0, 0) };
        titles.Controls.Add(title); titles.Controls.Add(subtitle); titles.Controls.Add(edition); header.Controls.Add(titles); header.Controls.Add(close);
        void DragHeader(object? sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); } }
        foreach (Control target in new Control[] { header, logo, titles, title, subtitle, edition }) target.MouseDown += DragHeader;
        close.Font = new Font("Segoe UI", 16); close.AccessibleName = "Cerrar instalador"; tips.SetToolTip(close, "Cerrar"); close.Click += (_, _) => Close(); layout.Controls.Add(header);

        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = new Padding(0) };
        var cards = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new Padding(0) };
        cards.ColumnStyles.Add(new(SizeType.Percent, 100));
        var locationCard = new RoundedPanel { Dock = DockStyle.Top, Height = 125, Padding = new Padding(18), Margin = new Padding(0, 4, 0, 12) };
        var location = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
        location.RowStyles.Add(new(SizeType.Absolute, 25)); location.RowStyles.Add(new(SizeType.Absolute, 42)); location.RowStyles.Add(new(SizeType.Absolute, 24));
        location.Controls.Add(new Label { Text = "Dónde vivirá Brisa", AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Margin = new Padding(0) });
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0), Padding = new Padding(0) };
        pathRow.RowCount = 1; pathRow.RowStyles.Add(new(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new(SizeType.Absolute, 124));
        var input = new RoundedPanel { FillColor = SetupColors.Input, Radius = 10, Dock = DockStyle.Fill, Padding = new Padding(11, 10, 11, 8), Margin = new Padding(0, 0, 8, 0) };
        destination.BackColor = SetupColors.Input; destination.ForeColor = SetupColors.Text; destination.AccessibleName = "Carpeta de instalación"; input.Controls.Add(destination);
        pathRow.Controls.Add(input); pathRow.Controls.Add(browse); location.Controls.Add(pathRow);
        location.Controls.Add(new Label { Text = "Solo para tu usuario. Sin permisos de administrador.", AutoSize = true, ForeColor = SetupColors.Muted, Font = new Font("Segoe UI", 8.5f), Margin = new Padding(0, 6, 0, 0) });
        location.BackColor = SetupColors.Surface; pathRow.BackColor = SetupColors.Surface; locationCard.Controls.Add(location); cards.Controls.Add(locationCard);
        var optionsCard = new RoundedPanel { Dock = DockStyle.Top, Height = 102, Padding = new Padding(14, 10, 14, 10), Margin = new Padding(0, 0, 0, 12) };
        var options = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0) };
        options.RowStyles.Add(new(SizeType.Percent, 50)); options.RowStyles.Add(new(SizeType.Percent, 50)); options.Controls.Add(shortcut); options.Controls.Add(launch); options.BackColor = SetupColors.Surface; optionsCard.Controls.Add(options); cards.Controls.Add(optionsCard);
        var statusCard = new RoundedPanel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(12), Margin = new Padding(0) };
        var statusRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
        statusRow.ColumnStyles.Add(new(SizeType.Absolute, 34)); statusRow.ColumnStyles.Add(new(SizeType.Percent, 100)); statusRow.Controls.Add(statusMark); statusRow.Controls.Add(status); statusRow.BackColor = SetupColors.Surface; statusCard.Controls.Add(statusRow); statusCard.Controls.Add(progress); cards.Controls.Add(statusCard);
        viewport.Controls.Add(cards); layout.Controls.Add(viewport);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(0, 16, 0, 0), Margin = new Padding(0) };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100)); footer.ColumnStyles.Add(new(SizeType.Absolute, 106)); footer.ColumnStyles.Add(new(SizeType.Absolute, 194));
        footer.RowStyles.Add(new(SizeType.Absolute, 44)); footer.RowStyles.Add(new(SizeType.Absolute, 22));
        var assurance = new Label { Text = "Sin cuenta adicional.\nTus ajustes se quedan contigo.", ForeColor = SetupColors.Muted, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0) };
        footer.Controls.Add(assurance); cancel.Margin = new Padding(0, 0, 8, 0); footer.Controls.Add(cancel); install.Margin = new Padding(0); footer.Controls.Add(install);
        var previewNote = new Label { Text = review ? "VISTA PREVIA · NO INSTALA ARCHIVOS" : "", ForeColor = SetupColors.Muted, Font = new Font("Segoe UI", 8), AutoSize = true, Margin = new Padding(0, 7, 0, 0) }; footer.Controls.Add(previewNote, 0, 1); footer.SetColumnSpan(previewNote, 3); layout.Controls.Add(footer);
        Controls.Add(layout); AcceptButton = install; CancelButton = cancel;
        browse.Click += (_, _) => { if (installing) return; using var picker = new FolderBrowserDialog { InitialDirectory = destination.Text, Description = "Elige una carpeta propia para Brisa" }; if (picker.ShowDialog(this) == DialogResult.OK) destination.Text = picker.SelectedPath; };
        cancel.Click += (_, _) => Close();
        install.Click += async (_, _) => { if (installed) Close(); else if (review) SetStatus("Vista previa de diseño. No se han instalado archivos."); else await InstallAsync(); };
        FormClosing += (_, e) => { if (installing) { e.Cancel = true; SetStatus("Espera a que termine la instalación antes de cerrar."); } };
        FormClosed += (_, _) => { brand.Dispose(); tips.Dispose(); };
        if (review) destination.Text = @"C:\Users\Example\Apps\BrisaDock";
        ResumeLayout(true);
        Shown += (_, _) => {
            if (args.Contains("--review-secondary") || args.Contains("--review-primary"))
            {
                var screen = args.Contains("--review-primary") ? Screen.PrimaryScreen! : Screen.AllScreens.FirstOrDefault(screen => !screen.Primary) ?? Screen.PrimaryScreen!;
                Location = new Point(screen.WorkingArea.X + 30, screen.WorkingArea.Y + 30);
            }
            if (args.Contains("--review-error")) SetStatus("No se pudo completar la instalación. Cierra Brisa si está abierta y comprueba la carpeta elegida.", true);
            else if (args.Contains("--review-success")) { installed = true; install.Text = "Listo"; SetStatus("Todo listo. Brisa está instalada y tus ajustes se han conservado."); SetBusy(false); }
            else if (args.Contains("--review-busy")) { SetStatus("Comprobando los archivos y preparando tu espacio…"); SetBusy(true); }
            if (review) BeginInvoke(() =>
            {
                var frame = RectangleToScreen(ClientRectangle);
                var controls = new[] { destination, (Control)browse, install, cancel, close, shortcut, launch, status }.Where(control => control.Visible).ToArray();
                var rectangles = controls.Select(control => control.RectangleToScreen(control.ClientRectangle)).ToArray();
                try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "brisa-setup-review.json"), System.Text.Json.JsonSerializer.Serialize(new { DeviceDpi, ClientSize.Width, ClientSize.Height, ControlsInside = rectangles.All(rect => frame.Contains(rect)), Frame = frame, Controls = rectangles })); } catch (IOException) { }
            });
        };
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        uint corners = 2, noBorder = 0xFFFFFFFE, dark = 1;
        DwmSetWindowAttribute(Handle, 33, ref corners, 4); DwmSetWindowAttribute(Handle, 34, ref noBorder, 4); DwmSetWindowAttribute(Handle, 20, ref dark, 4);
    }
    internal void CheckLayout()
    {
        // Exercise our controls without showing a window or changing system DPI.
        void LayoutTree(Control parent) { parent.PerformLayout(); foreach (Control child in parent.Controls) LayoutTree(child); }
        foreach (var size in new[] { new Size(680, 610), new Size(580, 520) })
        {
            ClientSize = new Size((int)Math.Ceiling(size.Width * DeviceDpi / 96d), (int)Math.Ceiling(size.Height * DeviceDpi / 96d)); LayoutTree(this);
            foreach (var control in new Control[] { destination, browse, install, cancel, close, shortcut, launch })
            {
                var bounds = control.Bounds;
                for (var parent = control.Parent; parent != null && parent != this; parent = parent.Parent) bounds.Offset(parent.Location);
                if (bounds.Width <= 0 || bounds.Height <= 0 || !ClientRectangle.Contains(bounds)) throw new InvalidDataException($"Setup control {control.AccessibleName ?? control.Text} exceeds {size}: {bounds}.");
            }
            if (browse.Height < Font.Height || install.Height < Font.Height) throw new InvalidDataException("Setup button text would be clipped.");
            SetBusy(true);
            if (install.Enabled || close.Enabled || destination.Enabled) throw new InvalidDataException("Setup remains editable during installation.");
            SetBusy(false);
            if (!install.Enabled || !close.Enabled || !destination.Enabled) throw new InvalidDataException("Setup does not recover after an installation attempt.");
        }
    }
    void SetStatus(string text, bool error = false)
    {
        status.Text = text; status.ForeColor = error ? SetupColors.Error : SetupColors.Muted;
        statusMark.Text = error ? "!" : installed ? "✓" : "i"; statusMark.ForeColor = error ? SetupColors.Error : SetupColors.Accent;
        tips.SetToolTip(status, text);
    }
    void SetBusy(bool busy)
    {
        install.Enabled = !busy; destination.Enabled = !busy && !installed; browse.Enabled = !busy && !installed;
        shortcut.Enabled = !busy && !installed; launch.Enabled = !busy && !installed; cancel.Enabled = !busy; close.Enabled = !busy;
        cancel.Visible = !installed;
        progress.Visible = busy; progress.Busy = busy;
        if (busy) install.Text = "Instalando…";
        else install.Text = installed ? "Listo" : "Instalar Brisa";
    }
    async Task InstallAsync()
    {
        if (installing) return;
        string target;
        try { target = Path.GetFullPath(destination.Text.Trim()); if (target == Path.GetPathRoot(target)) throw new ArgumentException(); }
        catch { SetStatus("Elige una carpeta propia para instalar Brisa.", true); return; }
        bool createShortcut = shortcut.Checked, open = launch.Checked;
        installing = true; SetBusy(true);
        SetStatus("Comprobando los archivos y preparando tu espacio…");
        string temporary = Path.Combine(Path.GetTempPath(), "Brisa-Setup-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Task.Run(async () =>
            {
                using var embedded = Program.Payload();
                // Embedded resources may not support seeking; the private temporary copy is bounded by the installer size.
                Directory.CreateDirectory(temporary);
                string zipPath = Path.Combine(temporary, "payload.zip");
                using (var output = File.Create(zipPath)) embedded.CopyTo(output);
                using (var input = File.OpenRead(zipPath)) ReleaseArchive.Extract(input, Path.Combine(temporary, "package"));
                string package = Path.Combine(temporary, "package");
                var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string arg in new[] { "-NoProfile", "-File", Path.Combine(package, "maintenance", "Install-Brisa.ps1"), "-SourceDirectory", package, "-Destination", target, "-NoDataImport" }) info.ArgumentList.Add(arg);
                if (!createShortcut) info.ArgumentList.Add("-NoShortcut");
                info.Environment["PSModulePath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "Modules");
                using var process = Process.Start(info) ?? throw new IOException("No se pudo iniciar el instalador.");
                var outputTask = process.StandardOutput.ReadToEndAsync(); var errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(); await outputTask; string error = await errorTask;
                if (process.ExitCode != 0) throw new IOException("No se pudo completar la instalación. Cierra Brisa si está abierta y comprueba el acceso a la carpeta. " + error);
            });
            installed = true; install.Text = "Listo";
            SetStatus("Todo listo. Brisa está instalada y tus ajustes se han conservado.");
            if (open) Process.Start(new ProcessStartInfo(Path.Combine(target, "BrisaDock.exe")) { UseShellExecute = false, WorkingDirectory = target });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException)
        { SetStatus(installed ? "Brisa está instalada, pero no se pudo abrir automáticamente. Puedes abrirla desde su carpeta." : e.Message, true); }
        finally
        {
            installing = false; SetBusy(false);
            string root = Path.GetFullPath(temporary); string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (root.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("Brisa-Setup-", StringComparison.Ordinal) && Directory.Exists(root))
                try { Directory.Delete(root, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
