using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.DirectInput;

namespace SavagePadEmu;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SavagePadEmu",
            "startup-error.log");

        void Report(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
            }
            catch { }

            MessageBox.Show(
                "SavagePadEmu could not start.\n\n" + ex.Message +
                "\n\nA detailed log was saved to:\n" + logPath,
                "SavagePadEmu - Startup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Report(ex);
        };

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }
}

public sealed class MainForm : Form
{
    private readonly ComboBox _devices = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 135 };
    private readonly Button _refresh = new() { Text = "Actualizar", Width = 100 };
    private readonly Button _start = new() { Text = "Iniciar emulación", Width = 140 };
    private readonly Button _stop = new() { Text = "Detener", Width = 100, Enabled = false };
    private readonly Button _save = new() { Text = "Guardar perfil", Width = 115 };
    private readonly Button _load = new() { Text = "Cargar perfil", Width = 105 };
    private readonly Button _defaults = new() { Text = "Mapeo default", Width = 115 };
    private readonly Button _clearAll = new() { Text = "Limpiar", Width = 85 };
    private readonly Button _saveAs = new() { Text = "Guardar como...", Width = 125 };
    private readonly Button _openProfileFolder = new() { Text = "Perfiles", Width = 85 };
    private readonly Label _status = new() { AutoSize = true };
    private readonly Label _deviceLabel = new() { AutoSize = true, Padding = new Padding(0, 7, 8, 0) };
    private readonly Label _languageLabel = new() { AutoSize = true, Padding = new Padding(16, 7, 8, 0) };
    private readonly Label _help = new() { Dock = DockStyle.Top, Height = 42, Padding = new Padding(12, 8, 12, 4) };
    private string _lang = "es";
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 108, BorderStyle = BorderStyle.FixedSingle, BackColor = ModernTheme.Surface, ForeColor = ModernTheme.Text, Font = new Font("Consolas", 8.5F) };
    private readonly TableLayoutPanel _mapper = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16), ColumnCount = 5, BackColor = ModernTheme.Surface };
    private readonly VisualMapperView _visualMapper = new() { Dock = DockStyle.Fill };
    private readonly ModernTabControl _tabs = new() { Dock = DockStyle.Fill, BackColor = ModernTheme.AppBackground };
    private readonly TabPage _mapTab = new() { Text = "Mapeo", BackColor = ModernTheme.Surface };
    private readonly TabPage _testTab = new() { Text = "Test / Drift", BackColor = ModernTheme.AppBackground };
    private readonly TabPage _calibrationTab = new() { Text = "Calibración", BackColor = ModernTheme.AppBackground };
    private readonly TestPadView _testView = new() { Dock = DockStyle.Fill, BackColor = ModernTheme.Surface };
    private readonly TableLayoutPanel _testValues = new() { Dock = DockStyle.Right, Width = 340, Padding = new Padding(14), ColumnCount = 2, AutoScroll = true, BackColor = ModernTheme.Surface };
    private readonly Dictionary<string, Label> _testValueLabels = new();
    private readonly System.Windows.Forms.Timer _testTimer = new() { Interval = 25 };
    private DirectInput? _testDirectInput;
    private Joystick? _testJoystick;
    private Guid _testJoystickGuid;
    private DateTime _lastTestErrorLog = DateTime.MinValue;

    private readonly NumericUpDown _leftDeadzone = Num(8, 0, 50);
    private readonly NumericUpDown _rightDeadzone = Num(8, 0, 50);
    private readonly NumericUpDown _triggerDeadzone = Num(5, 0, 50);
    private readonly NumericUpDown _antiDeadzone = Num(0, 0, 40);
    private readonly NumericUpDown _sensitivity = Num(100, 25, 200);
    private readonly NumericUpDown _driftWarning = Num(12, 1, 50);
    private readonly NumericUpDown _pollInterval = Num(1, 1, 16);
    private readonly Label _calibrationHelp = new() { Dock = DockStyle.Top, Height = 58, Padding = new Padding(12, 8, 12, 4) };
    private CalibrationSettings _calibration = new();
    private volatile Binding[] _runtimeBindings = Array.Empty<Binding>();
    private int _runtimeRevision;

    private readonly ProfileRepository _profileRepository = new();
    private readonly Dictionary<string, Label> _bindingLabels = new();
    private readonly Dictionary<string, CheckBox> _invertChecks = new();
    private readonly Dictionary<string, Button> _bindButtons = new();

    private List<DeviceInstance> _deviceList = new();
    private List<Binding> _bindings = new();
    private CancellationTokenSource? _cts;
    private ViGEmClient? _vigem;
    private IXbox360Controller? _xbox;
    private readonly object _bindingLock = new();
    private volatile bool _isBinding;
    private bool _isClosing;

    private string ProfilePath => Path.Combine(AppContext.BaseDirectory, "profile.json");
    private string SettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static NumericUpDown Num(decimal value, decimal min, decimal max) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = value,
        DecimalPlaces = 0,
        Increment = 1,
        Width = 70,
        TextAlign = HorizontalAlignment.Right
    };

    public MainForm()
    {
        Text = "SavagePadEmu - Visual Mapper estilo x360ce";
        Width = 1040;
        Height = 760;
        MinimumSize = new System.Drawing.Size(940, 650);
        Font = new Font("Segoe UI", 9F);
        BackColor = ModernTheme.AppBackground;

        var header = new ModernCard { Dock = DockStyle.Top, Height = 116, Padding = new Padding(16, 12, 16, 8) };
        var brand = new Label
        {
            Text = "SAVAGEPAD",
            Location = new Point(16, 10),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = ModernTheme.Text
        };
        var subtitle = new Label
        {
            Text = "XInput emulator • DirectInput mapper",
            Location = new Point(154, 17),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F),
            ForeColor = ModernTheme.MutedText
        };
        var top = new FlowLayoutPanel
        {
            Location = new Point(12, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Width = 980,
            Height = 66,
            Padding = new Padding(0),
            AutoSize = false,
            BackColor = Color.Transparent
        };
        header.Controls.Add(brand);
        header.Controls.Add(subtitle);
        header.Controls.Add(top);
        top.Controls.Add(_deviceLabel);
        top.Controls.Add(_devices);
        top.Controls.Add(_refresh);
        top.Controls.Add(_languageLabel);
        _language.Items.AddRange(new object[] { "Español", "English" });
        top.Controls.Add(_language);
        top.SetFlowBreak(_refresh, true);
        top.Controls.Add(_start);
        top.Controls.Add(_stop);
        top.Controls.Add(_save);
        top.Controls.Add(_load);
        top.Controls.Add(_defaults);
        top.Controls.Add(_clearAll);
        top.Controls.Add(_saveAs);
        top.Controls.Add(_openProfileFolder);
        top.SetFlowBreak(_openProfileFolder, true);
        top.Controls.Add(_status);


        BuildMappingPanel();
        BuildTestPanel();
        BuildCalibrationPanel();
        _tabs.TabPages.Add(_mapTab);
        _tabs.TabPages.Add(_testTab);
        _tabs.TabPages.Add(_calibrationTab);

        Controls.Add(_tabs);
        Controls.Add(_log);
        Controls.Add(_help);
        Controls.Add(header);

        ApplyModernChrome();

        LoadSettings();
        ApplyLanguage();
        LoadProfileOrDefaults();
        RefreshCalibrationUi();
        UpdateRuntimeBindings();
        RefreshMapperUi();
        RefreshDevices();

        _refresh.Click += (_, _) => RefreshDevices();
        _language.SelectedIndexChanged += LanguageChanged;
        _devices.SelectedIndexChanged += (_, _) => ResetTestJoystick();
        _start.Click += (_, _) => StartEmulation();
        _stop.Click += (_, _) => StopEmulation();
        _save.Click += (_, _) => SaveProfile();
        _saveAs.Click += (_, _) => SaveProfileAs();
        _openProfileFolder.Click += (_, _) => OpenProfileFolder();
        _load.Click += (_, _) => { LoadProfileOrDefaults(forceFile: true); RefreshMapperUi(); };
        _defaults.Click += (_, _) => { lock (_bindingLock) _bindings = TargetCatalog.CreateDefaultBindings(); UpdateRuntimeBindings(); RefreshMapperUi(); Log(T("defaultRestored")); };
        _clearAll.Click += (_, _) => { lock (_bindingLock) _bindings = TargetCatalog.All.Select(t => new Binding { Target = t }).ToList(); UpdateRuntimeBindings(); RefreshMapperUi(); Log(T("mappingCleared")); };
        _testTimer.Tick += (_, _) => UpdateTestPad();
        _testTimer.Start();
        FormClosing += (_, _) => { _isClosing = true; _testTimer.Stop(); ResetTestJoystick(); StopEmulation(); };
    }


    private void ApplyModernChrome()
    {
        ModernTheme.StyleInput(_devices);
        ModernTheme.StyleInput(_language);
        ModernTheme.StylePrimaryButton(_start);
        ModernTheme.StyleDangerButton(_stop);
        foreach (var button in new[] { _refresh, _save, _load, _defaults, _clearAll, _saveAs, _openProfileFolder })
            ModernTheme.StyleSecondaryButton(button);

        _help.BackColor = ModernTheme.AppBackground;
        _help.ForeColor = ModernTheme.MutedText;
        _status.ForeColor = ModernTheme.Success;
        _status.Font = new Font("Segoe UI Semibold", 9F);
        _deviceLabel.ForeColor = ModernTheme.Text;
        _languageLabel.ForeColor = ModernTheme.Text;
    }

    private string T(string key) => (_lang == "en" ? key switch
    {
        "title" => "SavagePadEmu - x360ce-style Visual Mapper",
        "device" => "Physical joystick:",
        "language" => "Language:",
        "refresh" => "Refresh",
        "start" => "Start emulation",
        "stop" => "Stop",
        "save" => "Save profile",
        "load" => "Load profile",
        "defaults" => "Default mapping",
        "clearAll" => "Clear",
        "stopped" => "Status: stopped",
        "emulating" => "Status: emulating virtual Xbox 360 controller",
        "connecting" => "Status: connecting virtual Xbox controller...",
        "mappingTab" => "Mapping",
        "testTab" => "Test / Drift",
        "calibrationTab" => "Calibration / Profiles",
        "help" => "Click 'Bind / Set key' on the Xbox control you want to configure, then press a button, axis, or D-pad direction on your physical joystick.",
        "testHelp" => "Test Pad: press buttons and move the sticks. The dots should return to center when released; if they stay off-center, there may be drift.",
        "leftStick" => "Left Stick",
        "rightStick" => "Right Stick",
        "driftLS" => "LS Drift",
        "driftRS" => "RS Drift",
        "ok" => "OK",
        "possibleDrift" => "POSSIBLE DRIFT",
        "virtualButton" => "Virtual button",
        "physicalInput" => "Assigned physical input",
        "bind" => "Bind / Set key",
        "invert" => "Invert",
        "clear" => "Clear",
        "none" => "None",
        "waiting" => "Waiting...",
        "selectJoystick" => "Select a physical joystick first.",
        "pressInput" => "Status: press/move an input for",
        "notDetected" => "No input detected for",
        "axisWarning" => "Warning: you assigned an axis to a digital button. It will work when the axis passes the threshold.",
        "assigned" => "assigned to",
        "detectError" => "Input detection error: ",
        "defaultRestored" => "Default mapping restored.",
        "mappingCleared" => "Mapping cleared.",
        "testConnected" => "Test Pad connected to the physical joystick.",
        "testReadError" => "Test Pad could not read the joystick: ",
        "profileSaved" => "Profile saved to profile.json",
        "profileLoaded" => "Profile loaded from profile.json",
        "profileMissing" => "profile.json was not found next to the .exe. Loading default mapping.",
        "profileLoadError" => "Could not load profile.json: ",
        "devicesFound" => "Devices found: ",
        "deviceError" => "Joystick scan error: ",
        "virtualConnected" => "Virtual Xbox 360 controller connected. Test it with joy.cpl or in-game.",
        "startError" => "Could not start. Make sure ViGEmBus is installed and working.",
        "error" => "Error: ",
        "stoppedLog" => "Emulation stopped.",
        "inverted" => "inverted",
        "off" => "OFF",
        "on" => "ON",
        "saveAs" => "Save as...",
        "profiles" => "Profiles",
        "calibrationHelp" => "Calibration: tune deadzones to remove drift, adjust sensitivity, and set the polling interval. Lower polling values reduce input lag but may use slightly more CPU.",
        "leftDeadzone" => "Left stick deadzone (%)",
        "rightDeadzone" => "Right stick deadzone (%)",
        "triggerDeadzone" => "Trigger deadzone (%)",
        "antiDeadzone" => "Anti-deadzone (%)",
        "sensitivity" => "Stick sensitivity (%)",
        "driftWarningValue" => "Drift warning (%)",
        "pollInterval" => "Polling interval (ms)",
        "settingsApplied" => "Calibration settings applied.",
        "profileSavedAs" => "Profile saved as: ",
        "hintLeftDz" => "Recommended: 6-12% if you notice drift.",
        "hintRightDz" => "Raise it if the dot does not return to center.",
        "hintTriggerDz" => "Prevents L2/R2 from staying slightly pressed.",
        "hintAntiDz" => "Compensates for games with internal deadzone. Use low values.",
        "hintSens" => "100% = linear. Higher values feel more aggressive.",
        "hintDrift" => "Visual drift warning threshold.",
        "hintPoll" => "1ms = lower input lag. 4ms = lower CPU usage.",
        _ => key
    } : key switch
    {
        "title" => "SavagePadEmu - Visual Mapper estilo x360ce",
        "device" => "Joystick físico:",
        "language" => "Idioma:",
        "refresh" => "Actualizar",
        "start" => "Iniciar emulación",
        "stop" => "Detener",
        "save" => "Guardar perfil",
        "load" => "Cargar perfil",
        "defaults" => "Mapeo default",
        "clearAll" => "Limpiar",
        "stopped" => "Estado: detenido",
        "emulating" => "Estado: emulando Xbox 360 virtual",
        "connecting" => "Estado: conectando mando Xbox virtual...",
        "mappingTab" => "Mapeo",
        "testTab" => "Test / Drift",
        "calibrationTab" => "Calibración / Perfiles",
        "help" => "Tocá 'Bind / Set key' en el control Xbox que quieras configurar y después presioná el botón, eje o cruceta de tu joystick físico.",
        "testHelp" => "Test Pad: presioná botones y mové sticks. Los puntos deben quedar centrados al soltar; si quedan movidos, hay drift.",
        "leftStick" => "Left Stick",
        "rightStick" => "Right Stick",
        "driftLS" => "Drift LS",
        "driftRS" => "Drift RS",
        "ok" => "OK",
        "possibleDrift" => "POSIBLE DRIFT",
        "virtualButton" => "Botón virtual",
        "physicalInput" => "Entrada física asignada",
        "bind" => "Bind / Set key",
        "invert" => "Invertir",
        "clear" => "Clear",
        "none" => "None",
        "waiting" => "Esperando...",
        "selectJoystick" => "Seleccioná un joystick físico primero.",
        "pressInput" => "Estado: presioná/mové una entrada para",
        "notDetected" => "No se detectó entrada para",
        "axisWarning" => "Aviso: asignaste un eje a un botón digital. Funcionará cuando el eje pase el umbral.",
        "assigned" => "asignado a",
        "detectError" => "Error detectando entrada: ",
        "defaultRestored" => "Mapeo default restaurado.",
        "mappingCleared" => "Mapeo limpiado.",
        "testConnected" => "Test Pad conectado al joystick físico.",
        "testReadError" => "Test Pad no pudo leer el joystick: ",
        "profileSaved" => "Perfil guardado en profile.json",
        "profileLoaded" => "Perfil cargado desde profile.json",
        "profileMissing" => "No encontré profile.json junto al .exe. Cargo el mapeo default.",
        "profileLoadError" => "No se pudo cargar profile.json: ",
        "devicesFound" => "Dispositivos encontrados: ",
        "deviceError" => "Error al buscar joysticks: ",
        "virtualConnected" => "Mando Xbox 360 virtual conectado. Probá con joy.cpl o dentro del juego.",
        "startError" => "No se pudo iniciar. Verificá que ViGEmBus esté instalado y funcionando.",
        "error" => "Error: ",
        "stoppedLog" => "Emulación detenida.",
        "inverted" => "invertido",
        "off" => "OFF",
        "on" => "ON",
        "saveAs" => "Guardar como...",
        "profiles" => "Perfiles",
        "calibrationHelp" => "Calibración: ajustá deadzones para eliminar drift, sensibilidad de sticks e intervalo de polling. Un valor más bajo reduce input lag, pero puede usar un poco más de CPU.",
        "leftDeadzone" => "Deadzone stick izquierdo (%)",
        "rightDeadzone" => "Deadzone stick derecho (%)",
        "triggerDeadzone" => "Deadzone gatillos (%)",
        "antiDeadzone" => "Anti-deadzone (%)",
        "sensitivity" => "Sensibilidad sticks (%)",
        "driftWarningValue" => "Aviso de drift (%)",
        "pollInterval" => "Intervalo polling (ms)",
        "settingsApplied" => "Calibración aplicada.",
        "profileSavedAs" => "Perfil guardado como: ",
        "hintLeftDz" => "Recomendado: 6-12% si notás drift.",
        "hintRightDz" => "Subilo si el punto no vuelve al centro.",
        "hintTriggerDz" => "Evita que L2/R2 queden apenas presionados.",
        "hintAntiDz" => "Compensa juegos con deadzone interna. Usar valores bajos.",
        "hintSens" => "100% = lineal. Más alto = respuesta más agresiva.",
        "hintDrift" => "Umbral del aviso visual de drift.",
        "hintPoll" => "1ms = menor input lag. 4ms = menor consumo de CPU.",
        _ => key
    });

    private void ApplyLanguage()
    {
        Text = T("title");
        _deviceLabel.Text = T("device");
        _languageLabel.Text = T("language");
        _refresh.Text = T("refresh");
        _start.Text = T("start");
        _stop.Text = T("stop");
        _save.Text = T("save");
        _load.Text = T("load");
        _defaults.Text = T("defaults");
        _clearAll.Text = T("clearAll");
        _saveAs.Text = T("saveAs");
        _openProfileFolder.Text = T("profiles");
        _mapTab.Text = T("mappingTab");
        _testTab.Text = T("testTab");
        _calibrationTab.Text = T("calibrationTab");
        _help.Text = T("help");
        _status.Text = _xbox is null ? T("stopped") : T("emulating");
        _language.SelectedIndexChanged -= LanguageChanged;
        _language.SelectedIndex = _lang == "en" ? 1 : 0;
        _language.SelectedIndexChanged += LanguageChanged;
        BuildVisualMapper();
        BuildTestPanel();
        BuildCalibrationPanel();
        RefreshCalibrationUi();
        RefreshMapperUi();
        _testView.Language = _lang;
        _testView.Invalidate();
        _visualMapper.Language = _lang;
        _visualMapper.Invalidate();
    }

    private void LanguageChanged(object? sender, EventArgs e)
    {
        if (_language.SelectedIndex >= 0)
        {
            _lang = _language.SelectedIndex == 1 ? "en" : "es";
            SaveSettings();
            ApplyLanguage();
        }
    }

    private void LoadSettings()
    {
        if (_profileRepository.TryLoadSettings(SettingsPath, out var settings))
            _lang = settings?.Language == "en" ? "en" : "es";
        else
            _lang = "es";
    }

    private void SaveSettings()
    {
        try { _profileRepository.SaveSettings(SettingsPath, new AppSettings { Language = _lang }); }
        catch { }
    }


    private void BuildCalibrationPanel()
    {
        _calibrationTab.Controls.Clear();
        _calibrationHelp.Text = T("calibrationHelp");
        var card = new ModernCard
        {
            Dock = DockStyle.Top,
            Padding = new Padding(18),
            Height = 330,
            Margin = new Padding(16)
        };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 8,
            BackColor = ModernTheme.Surface
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddCalibrationRow(panel, 0, T("leftDeadzone"), _leftDeadzone, T("hintLeftDz"));
        AddCalibrationRow(panel, 1, T("rightDeadzone"), _rightDeadzone, T("hintRightDz"));
        AddCalibrationRow(panel, 2, T("triggerDeadzone"), _triggerDeadzone, T("hintTriggerDz"));
        AddCalibrationRow(panel, 3, T("antiDeadzone"), _antiDeadzone, T("hintAntiDz"));
        AddCalibrationRow(panel, 4, T("sensitivity"), _sensitivity, T("hintSens"));
        AddCalibrationRow(panel, 5, T("driftWarningValue"), _driftWarning, T("hintDrift"));
        AddCalibrationRow(panel, 6, T("pollInterval"), _pollInterval, T("hintPoll"));
        var apply = new Button { Text = _lang == "en" ? "Apply" : "Aplicar", Width = 120, Height = 32 };
        ModernTheme.StylePrimaryButton(apply);
        apply.Click += (_, _) => { ApplyCalibrationFromUi(); SaveProfile(); Log(T("settingsApplied")); };
        panel.Controls.Add(apply, 1, 7);
        card.Controls.Add(panel);
        _calibrationTab.Controls.Add(card);
        _calibrationTab.Controls.Add(_calibrationHelp);
    }

    private static void AddCalibrationRow(TableLayoutPanel panel, int row, string label, NumericUpDown control, string hint)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = ModernTheme.Text }, 0, row);
        ModernTheme.StyleInput(control);
        panel.Controls.Add(control, 1, row);
        panel.Controls.Add(new Label { Text = hint, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = ModernTheme.MutedText }, 2, row);
    }

    private void RefreshCalibrationUi()
    {
        _leftDeadzone.Value = (decimal)Math.Clamp(_calibration.LeftStickDeadzone * 100.0, 0, 50);
        _rightDeadzone.Value = (decimal)Math.Clamp(_calibration.RightStickDeadzone * 100.0, 0, 50);
        _triggerDeadzone.Value = (decimal)Math.Clamp(_calibration.TriggerDeadzone * 100.0, 0, 50);
        _antiDeadzone.Value = (decimal)Math.Clamp(_calibration.AntiDeadzone * 100.0, 0, 40);
        _sensitivity.Value = (decimal)Math.Clamp(_calibration.Sensitivity * 100.0, 25, 200);
        _driftWarning.Value = (decimal)Math.Clamp(_calibration.DriftWarning * 100.0, 1, 50);
        _pollInterval.Value = Math.Clamp(_calibration.PollIntervalMs, 1, 16);
    }

    private void ApplyCalibrationFromUi()
    {
        // Swap the entire object so the polling thread always sees one coherent calibration snapshot.
        _calibration = new CalibrationSettings
        {
            LeftStickDeadzone = (double)_leftDeadzone.Value / 100.0,
            RightStickDeadzone = (double)_rightDeadzone.Value / 100.0,
            TriggerDeadzone = (double)_triggerDeadzone.Value / 100.0,
            AntiDeadzone = (double)_antiDeadzone.Value / 100.0,
            Sensitivity = (double)_sensitivity.Value / 100.0,
            DriftWarning = (double)_driftWarning.Value / 100.0,
            PollIntervalMs = (int)_pollInterval.Value
        };
        _testView.Calibration = _calibration;
        Interlocked.Increment(ref _runtimeRevision);
    }

    private void BuildMappingPanel()
    {
        _mapTab.Controls.Clear();

        // A TableLayoutPanel is used instead of SplitContainer here. On first startup
        // WinForms can temporarily lay out a SplitContainer narrower than the sum of
        // its minimum panel widths, which throws a SplitterDistance exception.
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = ModernTheme.AppBackground,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var visualCard = new ModernCard
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(8)
        };
        _visualMapper.Dock = DockStyle.Fill;
        visualCard.Controls.Add(_visualMapper);

        _mapper.Dock = DockStyle.Fill;
        _mapper.Margin = new Padding(4);

        layout.Controls.Add(visualCard, 0, 0);
        layout.Controls.Add(_mapper, 1, 0);
        _mapTab.Controls.Add(layout);

        _visualMapper.BindRequested -= VisualMapperBindRequested;
        _visualMapper.BindRequested += VisualMapperBindRequested;
    }

    private async void VisualMapperBindRequested(object? sender, string target)
    {
        await BindTargetAsync(target);
    }

    private void BuildTestPanel()
    {
        _testTab.Controls.Clear();
        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(18, 12, 18, 6),
            Text = T("testHelp"),
            ForeColor = ModernTheme.MutedText,
            BackColor = ModernTheme.AppBackground,
            Font = new Font("Segoe UI", 9F)
        };

        _testValues.Controls.Clear();
        _testValues.RowStyles.Clear();
        _testValues.ColumnStyles.Clear();
        _testValues.RowCount = 0;
        _testValueLabels.Clear();
        _testValues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        _testValues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        AddTestRow(T("leftStick"), "0 / 0", "LeftStick");
        AddTestRow(T("rightStick"), "0 / 0", "RightStick");
        AddTestRow("LT / L2", "0", "LeftTrigger");
        AddTestRow("RT / R2", "0", "RightTrigger");
        AddTestRow(T("driftLS"), T("ok"), "DriftLS");
        AddTestRow(T("driftRS"), T("ok"), "DriftRS");
        foreach (var t in new[] { "A", "B", "X", "Y", "LB", "RB", "Back", "Start", "LS", "RS", "DPadUp", "DPadRight", "DPadDown", "DPadLeft" })
            AddTestRow(TargetCatalog.Display(t), T("off"), t);

        _testTab.Controls.Add(_testView);
        _testTab.Controls.Add(_testValues);
        _testTab.Controls.Add(title);
    }

    private void AddTestRow(string name, string value, string key)
    {
        var row = _testValues.RowCount++;
        _testValues.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _testValues.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 0, row);
        var val = new Label { Text = value, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6, 0, 0, 0) };
        val.BackColor = Color.FromArgb(248, 250, 252);
        val.ForeColor = ModernTheme.Text;
        _testValues.Controls.Add(val, 1, row);
        _testValueLabels[key] = val;
    }

    private void UpdateTestPad()
    {
        if (_isBinding || _devices.SelectedIndex < 0 || _devices.SelectedIndex >= _deviceList.Count) return;

        try
        {
            var joystick = GetOrCreateTestJoystick(_deviceList[_devices.SelectedIndex].InstanceGuid);
            if (joystick is null) return;

            try
            {
                joystick.Poll();
            }
            catch
            {
                joystick.Acquire();
                joystick.Poll();
            }

            var s = joystick.GetCurrentState();
            var st = BuildVirtualTestState(s);
            _testView.State = st;
            _testView.Invalidate();
            _visualMapper.State = st;
            _visualMapper.Invalidate();

            SetTestText("LeftStick", $"{st.LeftX:+0.000;-0.000;0.000} / {st.LeftY:+0.000;-0.000;0.000}");
            SetTestText("RightStick", $"{st.RightX:+0.000;-0.000;0.000} / {st.RightY:+0.000;-0.000;0.000}");
            SetTestText("LeftTrigger", st.LeftTrigger.ToString());
            SetTestText("RightTrigger", st.RightTrigger.ToString());
            SetTestText("DriftLS", Math.Sqrt(st.LeftX * st.LeftX + st.LeftY * st.LeftY) > _calibration.DriftWarning ? T("possibleDrift") : T("ok"));
            SetTestText("DriftRS", Math.Sqrt(st.RightX * st.RightX + st.RightY * st.RightY) > _calibration.DriftWarning ? T("possibleDrift") : T("ok"));
            foreach (var kv in st.Buttons) SetTestText(kv.Key, kv.Value ? T("on") : T("off"));
        }
        catch (Exception ex)
        {
            // No spamear el log: algunos joysticks tiran error si Windows los reacquire mientras se bindea.
            if ((DateTime.Now - _lastTestErrorLog).TotalSeconds > 3)
            {
                _lastTestErrorLog = DateTime.Now;
                Log(T("testReadError") + ex.Message);
            }
            ResetTestJoystick();
        }
    }

    private Joystick? GetOrCreateTestJoystick(Guid instanceGuid)
    {
        if (_testJoystick is not null && _testJoystickGuid == instanceGuid)
            return _testJoystick;

        ResetTestJoystick();
        _testDirectInput = new DirectInput();
        _testJoystick = new Joystick(_testDirectInput, instanceGuid);
        _testJoystick.Properties.BufferSize = 128;
        _testJoystick.Acquire();
        _testJoystickGuid = instanceGuid;
        Log(T("testConnected"));
        return _testJoystick;
    }

    private void ResetTestJoystick()
    {
        try { _testJoystick?.Unacquire(); } catch { }
        try { _testJoystick?.Dispose(); } catch { }
        try { _testDirectInput?.Dispose(); } catch { }
        _testJoystick = null;
        _testDirectInput = null;
        _testJoystickGuid = Guid.Empty;
    }

    private void SetTestText(string key, string text)
    {
        if (_testValueLabels.TryGetValue(key, out var label)) label.Text = text;
    }

    private VirtualTestState BuildVirtualTestState(JoystickState state)
    {
        var input = new InputSnapshot(state);
        var testState = new VirtualTestState();
        foreach (var target in new[] { "A", "B", "X", "Y", "LB", "RB", "Back", "Start", "LS", "RS", "DPadUp", "DPadRight", "DPadDown", "DPadLeft" })
            testState.Buttons[target] = InputMapper.GetDigital(input, GetBinding(target));

        testState.LeftX = InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, GetBinding("LeftStickX")), true, _calibration);
        testState.LeftY = InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, GetBinding("LeftStickY")), true, _calibration);
        testState.RightX = InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, GetBinding("RightStickX")), false, _calibration);
        testState.RightY = InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, GetBinding("RightStickY")), false, _calibration);
        testState.LeftTrigger = InputMapper.CalibrateTrigger(InputMapper.GetAnalog(input, GetBinding("LeftTrigger")), _calibration);
        testState.RightTrigger = InputMapper.CalibrateTrigger(InputMapper.GetAnalog(input, GetBinding("RightTrigger")), _calibration);
        return testState;
    }

    private void BuildVisualMapper()
    {
        _mapper.SuspendLayout();
        _mapper.Controls.Clear();
        _bindingLabels.Clear();
        _invertChecks.Clear();
        _bindButtons.Clear();
        _mapper.RowStyles.Clear();
        _mapper.ColumnStyles.Clear();
        _mapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        _mapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        _mapper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        _mapper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        _mapper.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        AddHeader(T("virtualButton"), 0);
        AddHeader(T("physicalInput"), 1);
        AddHeader(T("bind"), 2);
        AddHeader(T("invert"), 3);
        AddHeader(T("clear"), 4);

        var row = 1;
        foreach (var target in TargetCatalog.All)
        {
            _mapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var targetLabel = new Label { Text = TargetCatalog.Display(target), AutoSize = false, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, ForeColor = ModernTheme.Text, Padding = new Padding(6, 0, 0, 0) };
            var currentLabel = new Label { Text = T("none"), AutoSize = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), BackColor = Color.FromArgb(248, 250, 252), ForeColor = ModernTheme.MutedText };
            var bindButton = new Button { Text = T("bind"), Dock = DockStyle.Fill, Tag = target };
            var invert = new CheckBox { Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter, Tag = target, Enabled = TargetCatalog.IsAnalog(target), ForeColor = ModernTheme.Accent };
            var clear = new Button { Text = T("clear"), Dock = DockStyle.Fill, Tag = target };
            ModernTheme.StylePrimaryButton(bindButton);
            ModernTheme.StyleSecondaryButton(clear);

            bindButton.Click += async (_, _) => await BindTargetAsync((string)bindButton.Tag);
            clear.Click += (_, _) => { SetBinding(new Binding { Target = (string)clear.Tag }); RefreshMapperUi(); };
            invert.CheckedChanged += InvertChangedDummy;

            _bindingLabels[target] = currentLabel;
            _invertChecks[target] = invert;
            _bindButtons[target] = bindButton;

            _mapper.Controls.Add(targetLabel, 0, row);
            _mapper.Controls.Add(currentLabel, 1, row);
            _mapper.Controls.Add(bindButton, 2, row);
            _mapper.Controls.Add(invert, 3, row);
            _mapper.Controls.Add(clear, 4, row);
            row++;
        }
        _mapper.ResumeLayout();
    }

    private void AddHeader(string text, int col)
    {
        _mapper.Controls.Add(new Label
        {
            Text = text,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
            ForeColor = ModernTheme.MutedText,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(6, 0, 0, 0)
        }, col, 0);
    }

    private async Task BindTargetAsync(string target)
    {
        if (_devices.SelectedIndex < 0)
        {
            MessageBox.Show(T("selectJoystick"), "SavagePadEmu");
            return;
        }
        if (_isBinding) return;
        _isBinding = true;
        SetBindButtonsEnabled(false);
        _bindButtons[target].Text = T("waiting");
        _status.Text = $"{T("pressInput")} {TargetCatalog.Display(target)}";
        try
        {
            var detected = await Task.Run(() => DetectInput(_deviceList[_devices.SelectedIndex].InstanceGuid, TimeSpan.FromSeconds(6)));
            if (detected is null)
            {
                Log($"{T("notDetected")} {target}.");
                return;
            }
            detected.Target = target;
            if (!TargetCatalog.IsAnalog(target) && detected.Kind == SourceKind.Axis)
                Log(T("axisWarning"));
            SetBinding(detected);
            RefreshMapperUi();
            Log($"{TargetCatalog.Display(target)} {T("assigned")} {BindingSourceText(detected)}");
        }
        catch (Exception ex)
        {
            Log(T("detectError") + ex.Message);
        }
        finally
        {
            _isBinding = false;
            SetBindButtonsEnabled(true);
            if (_bindButtons.TryGetValue(target, out var btn)) btn.Text = T("bind");
            _status.Text = _xbox is null ? T("stopped") : T("emulating");
        }
    }

    private void SetBindButtonsEnabled(bool enabled)
    {
        foreach (var b in _bindButtons.Values) b.Enabled = enabled;
        _refresh.Enabled = enabled && _xbox is null;
        _start.Enabled = enabled && _xbox is null;
        _stop.Enabled = enabled && _xbox is not null;
        _save.Enabled = enabled;
        _load.Enabled = enabled;
        _defaults.Enabled = enabled;
        _clearAll.Enabled = enabled;
    }

    private void RefreshMapperUi()
    {
        foreach (var target in TargetCatalog.All)
        {
            var b = GetBinding(target);
            if (_bindingLabels.TryGetValue(target, out var label)) label.Text = BindingSourceText(b);
            if (_invertChecks.TryGetValue(target, out var chk))
            {
                chk.CheckedChanged -= InvertChangedDummy;
                chk.Checked = b.Invert;
                chk.Enabled = TargetCatalog.IsAnalog(target) || b.Kind == SourceKind.Axis || b.Kind == SourceKind.Button;
                chk.CheckedChanged += InvertChangedDummy;
            }
        }

        Binding[] copy;
        lock (_bindingLock) copy = _bindings.Select(b => b.Clone()).ToArray();
        _visualMapper.SetBindings(copy);
    }

    private void InvertChangedDummy(object? sender, EventArgs e)
    {
        if (sender is CheckBox chk && chk.Tag is string t)
        {
            var b = GetBinding(t);
            b.Invert = chk.Checked;
            SetBinding(b);
        }
    }

    private Binding GetBinding(string target)
    {
        lock (_bindingLock)
        {
            return _bindings.FirstOrDefault(x => x.Target == target) ?? new Binding { Target = target };
        }
    }

    private void SetBinding(Binding binding)
    {
        lock (_bindingLock)
        {
            var idx = _bindings.FindIndex(x => x.Target == binding.Target);
            if (idx >= 0) _bindings[idx] = binding;
            else _bindings.Add(binding);
            _bindings = TargetCatalog.Normalize(_bindings);
            UpdateRuntimeBindings();
        }
    }

    private void SaveProfile()
    {
        ApplyCalibrationFromUi();
        List<Binding> copy;
        lock (_bindingLock) copy = TargetCatalog.Normalize(_bindings);
        _profileRepository.SaveProfile(ProfilePath, new Profile { Name = "Default", Calibration = _calibration.Clone(), Bindings = copy });
        Log(T("profileSaved"));
    }

    private void SaveProfileAs()
    {
        ApplyCalibrationFromUi();
        using var dialog = new SaveFileDialog
        {
            Filter = "SavagePadEmu profile (*.json)|*.json|JSON (*.json)|*.json",
            FileName = "profile.json",
            InitialDirectory = AppContext.BaseDirectory
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        List<Binding> copy;
        lock (_bindingLock) copy = TargetCatalog.Normalize(_bindings);
        _profileRepository.SaveProfile(dialog.FileName, new Profile
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            Calibration = _calibration.Clone(),
            Bindings = copy
        });
        Log(T("profileSavedAs") + dialog.FileName);
    }

    private void OpenProfileFolder()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = AppContext.BaseDirectory, UseShellExecute = true }); } catch { }
    }

    private void UpdateRuntimeBindings()
    {
        lock (_bindingLock)
            _runtimeBindings = TargetCatalog.Normalize(_bindings).Select(binding => binding.Clone()).ToArray();
        Interlocked.Increment(ref _runtimeRevision);
    }

    private void LoadProfileOrDefaults(bool forceFile = false)
    {
        if (_profileRepository.TryLoadProfile(ProfilePath, out var profile, out var error) && profile?.Bindings.Count > 0)
        {
            lock (_bindingLock) _bindings = TargetCatalog.Normalize(profile.Bindings);
            _calibration = profile.Calibration ?? new CalibrationSettings();
            RefreshCalibrationUi();
            ApplyCalibrationFromUi();
            UpdateRuntimeBindings();
            Log(T("profileLoaded"));
            return;
        }

        if (error is not null) Log(T("profileLoadError") + error.Message);
        if (forceFile) MessageBox.Show(T("profileMissing"), "SavagePadEmu");
        lock (_bindingLock) _bindings = TargetCatalog.CreateDefaultBindings();
        _calibration ??= new CalibrationSettings();
        UpdateRuntimeBindings();
    }

    private void RefreshDevices()
    {
        try
        {
            using var directInput = new DirectInput();
            _deviceList = directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AttachedOnly)
                .Concat(directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly))
                .GroupBy(d => d.InstanceGuid).Select(g => g.First()).ToList();

            _devices.Items.Clear();
            foreach (var device in _deviceList) _devices.Items.Add(device.InstanceName);
            if (_devices.Items.Count > 0) _devices.SelectedIndex = 0;
            Log($"{T("devicesFound")}{_devices.Items.Count}");
        }
        catch (Exception ex)
        {
            Log(T("deviceError") + ex.Message);
        }
    }

    private Binding? DetectInput(Guid instanceGuid, TimeSpan timeout)
    {
        using var directInput = new DirectInput();
        using var joystick = new Joystick(directInput, instanceGuid);
        joystick.Acquire();
        joystick.Poll();
        var baseline = new InputSnapshot(joystick.GetCurrentState());
        var deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);

        while (Stopwatch.GetTimestamp() < deadline)
        {
            joystick.Poll();
            var input = new InputSnapshot(joystick.GetCurrentState());
            for (var index = 0; index < 128; index++)
                if (input.IsButtonPressed(index)) return new Binding { Kind = SourceKind.Button, Index = index };

            if (input.Pov >= 0) return new Binding { Kind = SourceKind.Pov, Index = PovDirection(input.Pov) };

            for (var index = 0; index < 8; index++)
            {
                var value = input.Axis(index);
                var baseValue = baseline.Axis(index);
                if (Math.Abs(value - baseValue) > 9000)
                    return new Binding { Kind = SourceKind.Axis, Index = index, Invert = value < baseValue };
            }

            Thread.Sleep(8);
        }
        return null;
    }

    private static int PovDirection(int pov)
    {
        if (pov >= 31500 || pov <= 4500) return 0;
        if (pov <= 13500) return 1;
        if (pov <= 22500) return 2;
        return 3;
    }

    private void StartEmulation()
    {
        if (_devices.SelectedIndex < 0 || _devices.SelectedIndex >= _deviceList.Count)
        {
            MessageBox.Show(T("selectJoystick"), "SavagePadEmu");
            return;
        }
        SaveProfile();
        _start.Enabled = false;
        _stop.Enabled = true;
        _refresh.Enabled = false;
        _status.Text = T("connecting");
        _cts = new CancellationTokenSource();
        // The test panel uses DirectInput too; release it while emulation owns the device.
        _testTimer.Stop();
        ResetTestJoystick();

        try
        {
            _vigem = new ViGEmClient();
            _xbox = _vigem.CreateXbox360Controller();
            _xbox.Connect();
            Log(T("virtualConnected"));
            _status.Text = T("emulating");
            _ = Task.Run(() => PollLoop(_deviceList[_devices.SelectedIndex].InstanceGuid, _cts.Token));
        }
        catch (Exception ex)
        {
            Log(T("error") + ex.Message);
            MessageBox.Show(T("startError") + "\n\n" + ex.Message, "SavagePadEmu");
            StopEmulation();
        }
    }

    private void PollLoop(Guid instanceGuid, CancellationToken token)
    {
        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        using var directInput = new DirectInput();
        using var joystick = new Joystick(directInput, instanceGuid);
        joystick.Properties.BufferSize = 128;
        joystick.Acquire();

        ulong previousInput = ulong.MaxValue;
        var previousRevision = -1;
        while (!token.IsCancellationRequested)
        {
            try
            {
                joystick.Poll();
                var input = new InputSnapshot(joystick.GetCurrentState());
                var revision = Volatile.Read(ref _runtimeRevision);

                // Skip redundant ViGEm reports. This lowers CPU use and bus traffic while preserving
                // immediate updates whenever a physical input or mapping/calibration setting changes.
                if (input.Fingerprint != previousInput || revision != previousRevision)
                {
                    ApplyState(input);
                    previousInput = input.Fingerprint;
                    previousRevision = revision;
                }
            }
            catch
            {
                try { joystick.Acquire(); } catch { }
            }

            var delay = Math.Clamp(_calibration.PollIntervalMs, 1, 16);
            if (delay <= 1) Thread.Yield();
            else token.WaitHandle.WaitOne(delay);
        }
    }

    private void ApplyState(in InputSnapshot input)
    {
        var xbox = _xbox;
        if (xbox is null) return;

        var calibration = _calibration;
        var bindings = _runtimeBindings;
        foreach (var binding in bindings)
        {
            switch (binding.Target)
            {
                case "A": xbox.SetButtonState(Xbox360Button.A, InputMapper.GetDigital(input, binding)); break;
                case "B": xbox.SetButtonState(Xbox360Button.B, InputMapper.GetDigital(input, binding)); break;
                case "X": xbox.SetButtonState(Xbox360Button.X, InputMapper.GetDigital(input, binding)); break;
                case "Y": xbox.SetButtonState(Xbox360Button.Y, InputMapper.GetDigital(input, binding)); break;
                case "LB": xbox.SetButtonState(Xbox360Button.LeftShoulder, InputMapper.GetDigital(input, binding)); break;
                case "RB": xbox.SetButtonState(Xbox360Button.RightShoulder, InputMapper.GetDigital(input, binding)); break;
                case "Back": xbox.SetButtonState(Xbox360Button.Back, InputMapper.GetDigital(input, binding)); break;
                case "Start": xbox.SetButtonState(Xbox360Button.Start, InputMapper.GetDigital(input, binding)); break;
                case "LS": xbox.SetButtonState(Xbox360Button.LeftThumb, InputMapper.GetDigital(input, binding)); break;
                case "RS": xbox.SetButtonState(Xbox360Button.RightThumb, InputMapper.GetDigital(input, binding)); break;
                case "DPadUp": xbox.SetButtonState(Xbox360Button.Up, InputMapper.GetDigital(input, binding)); break;
                case "DPadRight": xbox.SetButtonState(Xbox360Button.Right, InputMapper.GetDigital(input, binding)); break;
                case "DPadDown": xbox.SetButtonState(Xbox360Button.Down, InputMapper.GetDigital(input, binding)); break;
                case "DPadLeft": xbox.SetButtonState(Xbox360Button.Left, InputMapper.GetDigital(input, binding)); break;
                case "LeftStickX": xbox.SetAxisValue(Xbox360Axis.LeftThumbX, AxisToShort(InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, binding), true, calibration))); break;
                case "LeftStickY": xbox.SetAxisValue(Xbox360Axis.LeftThumbY, AxisToShort(InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, binding), true, calibration))); break;
                case "RightStickX": xbox.SetAxisValue(Xbox360Axis.RightThumbX, AxisToShort(InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, binding), false, calibration))); break;
                case "RightStickY": xbox.SetAxisValue(Xbox360Axis.RightThumbY, AxisToShort(InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, binding), false, calibration))); break;
                case "LeftTrigger": xbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)InputMapper.CalibrateTrigger(InputMapper.GetAnalog(input, binding), calibration)); break;
                case "RightTrigger": xbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)InputMapper.CalibrateTrigger(InputMapper.GetAnalog(input, binding), calibration)); break;
            }
        }
        xbox.SubmitReport();
    }

    private string BindingSourceText(Binding b) => b.Kind switch
    {
        SourceKind.Button => $"Button {b.Index + 1}",
        SourceKind.Axis => TargetCatalog.AxisNames[Math.Clamp(b.Index, 0, TargetCatalog.AxisNames.Length - 1)] + (b.Invert ? " (" + T("inverted") + ")" : ""),
        SourceKind.Pov => TargetCatalog.PovNames[Math.Clamp(b.Index, 0, TargetCatalog.PovNames.Length - 1)],
        _ => T("none")
    };

    private static short AxisToShort(double normalized)
    {
        return (short)Math.Clamp(Math.Round(normalized * 32767.0), short.MinValue, short.MaxValue);
    }

    private void StopEmulation()
    {
        try { _cts?.Cancel(); } catch { }
        try { _xbox?.Disconnect(); } catch { }
        try { _vigem?.Dispose(); } catch { }
        _cts = null;
        _xbox = null;
        _vigem = null;
        _start.Enabled = true;
        _stop.Enabled = false;
        _refresh.Enabled = true;
        _status.Text = T("stopped");
        if (!_isClosing && !_testTimer.Enabled) _testTimer.Start();
        Log(T("stoppedLog"));
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(message));
            return;
        }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
