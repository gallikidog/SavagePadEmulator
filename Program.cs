using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public enum SourceKind
{
    None,
    Button,
    Axis,
    Pov
}

public sealed class Binding
{
    public string Target { get; set; } = "";
    public SourceKind Kind { get; set; } = SourceKind.None;
    public int Index { get; set; }
    public bool Invert { get; set; }
}

public sealed class CalibrationSettings
{
    public double LeftStickDeadzone { get; set; } = 0.08;
    public double RightStickDeadzone { get; set; } = 0.08;
    public double TriggerDeadzone { get; set; } = 0.05;
    public double AntiDeadzone { get; set; } = 0.00;
    public double Sensitivity { get; set; } = 1.00;
    public double DriftWarning { get; set; } = 0.12;
    public int PollIntervalMs { get; set; } = 1;
}

public sealed class Profile
{
    public string Name { get; set; } = "Default";
    public CalibrationSettings Calibration { get; set; } = new();
    public List<Binding> Bindings { get; set; } = new();
}

public sealed class AppSettings
{
    public string Language { get; set; } = "es";
    public CalibrationSettings Calibration { get; set; } = new();
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
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 120 };
    private readonly TableLayoutPanel _mapper = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12), ColumnCount = 5 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly TabPage _mapTab = new() { Text = "Mapeo" };
    private readonly TabPage _testTab = new() { Text = "Test / Drift" };
    private readonly TabPage _calibrationTab = new() { Text = "Calibración" };
    private readonly TestPadView _testView = new() { Dock = DockStyle.Fill };
    private readonly TableLayoutPanel _testValues = new() { Dock = DockStyle.Right, Width = 330, Padding = new Padding(12), ColumnCount = 2, AutoScroll = true };
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

    private readonly string[] _targets = new[]
    {
        "A", "B", "X", "Y", "LB", "RB", "Back", "Start", "LS", "RS", "DPadUp", "DPadRight", "DPadDown", "DPadLeft",
        "LeftStickX", "LeftStickY", "RightStickX", "RightStickY", "LeftTrigger", "RightTrigger"
    };

    private readonly string[] _axisNames = new[] { "X", "Y", "Z", "RotationX", "RotationY", "RotationZ", "Slider0", "Slider1" };
    private readonly string[] _povNames = new[] { "POV Up", "POV Right", "POV Down", "POV Left" };
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
        Width = 980;
        Height = 720;
        MinimumSize = new System.Drawing.Size(900, 620);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 249, 252);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(12), AutoSize = false };
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


        _mapTab.Controls.Add(_mapper);
        BuildTestPanel();
        BuildCalibrationPanel();
        _tabs.TabPages.Add(_mapTab);
        _tabs.TabPages.Add(_testTab);
        _tabs.TabPages.Add(_calibrationTab);

        Controls.Add(_tabs);
        Controls.Add(_log);
        Controls.Add(_help);
        Controls.Add(top);

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
        _defaults.Click += (_, _) => { lock (_bindingLock) _bindings = DefaultBindings(); UpdateRuntimeBindings(); RefreshMapperUi(); Log(T("defaultRestored")); };
        _clearAll.Click += (_, _) => { lock (_bindingLock) _bindings = _targets.Select(t => new Binding { Target = t }).ToList(); UpdateRuntimeBindings(); RefreshMapperUi(); Log(T("mappingCleared")); };
        _testTimer.Tick += (_, _) => UpdateTestPad();
        _testTimer.Start();
        FormClosing += (_, _) => { _testTimer.Stop(); ResetTestJoystick(); StopEmulation(); };
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
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                _lang = settings?.Language == "en" ? "en" : "es";
            }
        }
        catch { _lang = "es"; }
    }

    private void SaveSettings()
    {
        try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new AppSettings { Language = _lang }, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }


    private void BuildCalibrationPanel()
    {
        _calibrationTab.Controls.Clear();
        _calibrationHelp.Text = T("calibrationHelp");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 8,
            Height = 310
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
        apply.Click += (_, _) => { ApplyCalibrationFromUi(); SaveProfile(); Log(T("settingsApplied")); };
        panel.Controls.Add(apply, 1, 7);
        _calibrationTab.Controls.Add(panel);
        _calibrationTab.Controls.Add(_calibrationHelp);
    }

    private static void AddCalibrationRow(TableLayoutPanel panel, int row, string label, NumericUpDown control, string hint)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        panel.Controls.Add(control, 1, row);
        panel.Controls.Add(new Label { Text = hint, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray }, 2, row);
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
        _calibration.LeftStickDeadzone = (double)_leftDeadzone.Value / 100.0;
        _calibration.RightStickDeadzone = (double)_rightDeadzone.Value / 100.0;
        _calibration.TriggerDeadzone = (double)_triggerDeadzone.Value / 100.0;
        _calibration.AntiDeadzone = (double)_antiDeadzone.Value / 100.0;
        _calibration.Sensitivity = (double)_sensitivity.Value / 100.0;
        _calibration.DriftWarning = (double)_driftWarning.Value / 100.0;
        _calibration.PollIntervalMs = (int)_pollInterval.Value;
        _testView.Calibration = _calibration;
    }

    private void BuildTestPanel()
    {
        _testTab.Controls.Clear();
        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(12, 10, 12, 4),
            Text = T("testHelp")
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
            AddTestRow(DisplayTarget(t), T("off"), t);

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

    private VirtualTestState BuildVirtualTestState(JoystickState s)
    {
        var st = new VirtualTestState();
        foreach (var t in new[] { "A", "B", "X", "Y", "LB", "RB", "Back", "Start", "LS", "RS", "DPadUp", "DPadRight", "DPadDown", "DPadLeft" })
            st.Buttons[t] = GetDigital(s, GetBinding(t));
        st.LeftX = CalibrateAxis(GetAnalog(s, GetBinding("LeftStickX")), true);
        st.LeftY = CalibrateAxis(GetAnalog(s, GetBinding("LeftStickY")), true);
        st.RightX = CalibrateAxis(GetAnalog(s, GetBinding("RightStickX")), false);
        st.RightY = CalibrateAxis(GetAnalog(s, GetBinding("RightStickY")), false);
        st.LeftTrigger = CalibrateTrigger(GetAnalog(s, GetBinding("LeftTrigger")));
        st.RightTrigger = CalibrateTrigger(GetAnalog(s, GetBinding("RightTrigger")));
        return st;
    }

    private double CalibrateAxis(int value, bool leftStick)
    {
        var x = Math.Clamp((value - 32768) / 32767.0, -1.0, 1.0);
        var sign = Math.Sign(x);
        var abs = Math.Abs(x);
        var dz = leftStick ? _calibration.LeftStickDeadzone : _calibration.RightStickDeadzone;
        if (abs <= dz) return 0;
        var scaled = (abs - dz) / Math.Max(0.0001, 1.0 - dz);
        scaled = Math.Min(1.0, scaled * Math.Max(0.25, _calibration.Sensitivity));
        if (_calibration.AntiDeadzone > 0 && scaled > 0) scaled = Math.Min(1.0, _calibration.AntiDeadzone + scaled * (1.0 - _calibration.AntiDeadzone));
        return sign * scaled;
    }

    private int CalibrateTrigger(int value)
    {
        var x = Math.Clamp(value / 65535.0, 0.0, 1.0);
        if (x <= _calibration.TriggerDeadzone) return 0;
        var scaled = (x - _calibration.TriggerDeadzone) / Math.Max(0.0001, 1.0 - _calibration.TriggerDeadzone);
        return (int)Math.Clamp(Math.Round(scaled * 255.0), 0, 255);
    }

    private void BuildVisualMapper()
    {
        _mapper.SuspendLayout();
        _mapper.Controls.Clear();
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
        foreach (var target in _targets)
        {
            _mapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            var targetLabel = new Label { Text = DisplayTarget(target), AutoSize = false, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            var currentLabel = new Label { Text = T("none"), AutoSize = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
            var bindButton = new Button { Text = T("bind"), Dock = DockStyle.Fill, Tag = target };
            var invert = new CheckBox { Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter, Tag = target, Enabled = IsAnalogTarget(target) };
            var clear = new Button { Text = T("clear"), Dock = DockStyle.Fill, Tag = target };

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
            Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold)
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
        _status.Text = $"{T("pressInput")} {DisplayTarget(target)}";
        try
        {
            var detected = await Task.Run(() => DetectInput(_deviceList[_devices.SelectedIndex].InstanceGuid, TimeSpan.FromSeconds(6)));
            if (detected is null)
            {
                Log($"{T("notDetected")} {target}.");
                return;
            }
            detected.Target = target;
            if (!IsAnalogTarget(target) && detected.Kind == SourceKind.Axis)
                Log(T("axisWarning"));
            SetBinding(detected);
            RefreshMapperUi();
            Log($"{DisplayTarget(target)} {T("assigned")} {BindingSourceText(detected)}");
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
        foreach (var target in _targets)
        {
            var b = GetBinding(target);
            if (_bindingLabels.TryGetValue(target, out var label)) label.Text = BindingSourceText(b);
            if (_invertChecks.TryGetValue(target, out var chk))
            {
                chk.CheckedChanged -= InvertChangedDummy;
                chk.Checked = b.Invert;
                chk.Enabled = IsAnalogTarget(target) || b.Kind == SourceKind.Axis || b.Kind == SourceKind.Button;
                chk.CheckedChanged += InvertChangedDummy;
            }
        }
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
            _bindings = NormalizeBindings(_bindings);
            UpdateRuntimeBindings();
        }
    }

    private static bool IsAnalogTarget(string target) => target is "LeftStickX" or "LeftStickY" or "RightStickX" or "RightStickY" or "LeftTrigger" or "RightTrigger";

    private static string DisplayTarget(string target) => target switch
    {
        "A" => "A (Xbox) / ✕ (PlayStation)",
        "B" => "B (Xbox) / ○ (PlayStation)",
        "X" => "X (Xbox) / □ (PlayStation)",
        "Y" => "Y (Xbox) / △ (PlayStation)",
        "LB" => "LB (Xbox) / L1 (PlayStation)",
        "RB" => "RB (Xbox) / R1 (PlayStation)",
        "LS" => "LS (Xbox) / L3 (PlayStation)",
        "RS" => "RS (Xbox) / R3 (PlayStation)",
        "Back" => "Back (Xbox) / Share (PlayStation)",
        "Start" => "Start (Xbox) / Options (PlayStation)",
        "DPadUp" => "D-Pad Up (Xbox) / ↑ (PlayStation)",
        "DPadRight" => "D-Pad Right (Xbox) / → (PlayStation)",
        "DPadDown" => "D-Pad Down (Xbox) / ↓ (PlayStation)",
        "DPadLeft" => "D-Pad Left (Xbox) / ← (PlayStation)",
        "LeftStickX" => "Left Stick X (Xbox) / Left Stick X (PlayStation)",
        "LeftStickY" => "Left Stick Y (Xbox) / Left Stick Y (PlayStation)",
        "RightStickX" => "Right Stick X (Xbox) / Right Stick X (PlayStation)",
        "RightStickY" => "Right Stick Y (Xbox) / Right Stick Y (PlayStation)",
        "LeftTrigger" => "LT (Xbox) / L2 (PlayStation)",
        "RightTrigger" => "RT (Xbox) / R2 (PlayStation)",
        _ => $"{target} (Xbox) / {target} (PlayStation)"
    };

    private List<Binding> DefaultBindings() => new()
    {
        // Mapeo default estilo PlayStation común:
        // A (Xbox) / ✕ (PlayStation) = Button 2
        // B (Xbox) / ○ (PlayStation) = Button 3
        // X (Xbox) / □ (PlayStation) = Button 1
        // Y (Xbox) / △ (PlayStation) = Button 4
        new() { Target = "A", Kind = SourceKind.Button, Index = 1 },
        new() { Target = "B", Kind = SourceKind.Button, Index = 2 },
        new() { Target = "X", Kind = SourceKind.Button, Index = 0 },
        new() { Target = "Y", Kind = SourceKind.Button, Index = 3 },
        new() { Target = "LB", Kind = SourceKind.Button, Index = 4 },
        new() { Target = "RB", Kind = SourceKind.Button, Index = 5 },
        new() { Target = "Back", Kind = SourceKind.Button, Index = 6 },
        new() { Target = "Start", Kind = SourceKind.Button, Index = 7 },
        new() { Target = "LS", Kind = SourceKind.Button, Index = 8 },
        new() { Target = "RS", Kind = SourceKind.Button, Index = 9 },
        new() { Target = "DPadUp", Kind = SourceKind.Pov, Index = 0 },
        new() { Target = "DPadRight", Kind = SourceKind.Pov, Index = 1 },
        new() { Target = "DPadDown", Kind = SourceKind.Pov, Index = 2 },
        new() { Target = "DPadLeft", Kind = SourceKind.Pov, Index = 3 },
        new() { Target = "LeftStickX", Kind = SourceKind.Axis, Index = 0 },
        new() { Target = "LeftStickY", Kind = SourceKind.Axis, Index = 1, Invert = true },
        new() { Target = "RightStickX", Kind = SourceKind.Axis, Index = 3 },
        new() { Target = "RightStickY", Kind = SourceKind.Axis, Index = 4, Invert = true },
        new() { Target = "LeftTrigger", Kind = SourceKind.Axis, Index = 2 },
        new() { Target = "RightTrigger", Kind = SourceKind.Axis, Index = 5 },
    };

    private void SaveProfile()
    {
        ApplyCalibrationFromUi();
        List<Binding> copy;
        lock (_bindingLock) copy = NormalizeBindings(_bindings);
        var profile = new Profile { Name = "Default", Calibration = _calibration, Bindings = copy };
        File.WriteAllText(ProfilePath, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
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
        lock (_bindingLock) copy = NormalizeBindings(_bindings);
        var profile = new Profile { Name = Path.GetFileNameWithoutExtension(dialog.FileName), Calibration = _calibration, Bindings = copy };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        Log(T("profileSavedAs") + dialog.FileName);
    }

    private void OpenProfileFolder()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = AppContext.BaseDirectory, UseShellExecute = true }); } catch { }
    }

    private void UpdateRuntimeBindings()
    {
        lock (_bindingLock)
            _runtimeBindings = NormalizeBindings(_bindings).Select(b => new Binding { Target = b.Target, Kind = b.Kind, Index = b.Index, Invert = b.Invert }).ToArray();
    }

    private void LoadProfileOrDefaults(bool forceFile = false)
    {
        try
        {
            if (File.Exists(ProfilePath))
            {
                var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(ProfilePath));
                if (profile?.Bindings?.Count > 0)
                {
                    lock (_bindingLock) _bindings = NormalizeBindings(profile.Bindings);
                    _calibration = profile.Calibration ?? new CalibrationSettings();
                    RefreshCalibrationUi();
                    ApplyCalibrationFromUi();
                    UpdateRuntimeBindings();
                    Log(T("profileLoaded"));
                    return;
                }
            }
            if (forceFile) MessageBox.Show(T("profileMissing"), "SavagePadEmu");
        }
        catch (Exception ex)
        {
            Log(T("profileLoadError") + ex.Message);
        }
        lock (_bindingLock) _bindings = DefaultBindings();
        _calibration ??= new CalibrationSettings();
        UpdateRuntimeBindings();
    }

    private List<Binding> NormalizeBindings(List<Binding> loaded)
    {
        var result = _targets.Select(t => new Binding { Target = t }).ToList();
        foreach (var item in loaded)
        {
            var idx = result.FindIndex(x => x.Target == item.Target);
            if (idx >= 0) result[idx] = item;
        }
        return result;
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
        var baseState = joystick.GetCurrentState();
        var baseAxes = AxisValues(baseState);
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            joystick.Poll();
            var s = joystick.GetCurrentState();
            var buttons = s.Buttons ?? Array.Empty<bool>();
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i]) return new Binding { Kind = SourceKind.Button, Index = i };

            var pov = s.PointOfViewControllers?.FirstOrDefault() ?? -1;
            if (pov >= 0) return new Binding { Kind = SourceKind.Pov, Index = PovDirection(pov) };

            var axes = AxisValues(s);
            for (int i = 0; i < axes.Length; i++)
                if (Math.Abs(axes[i] - baseAxes[i]) > 9000) return new Binding { Kind = SourceKind.Axis, Index = i, Invert = axes[i] < baseAxes[i] };

            Thread.Sleep(15);
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

        var stopwatch = Stopwatch.StartNew();
        long lastTick = 0;

        while (!token.IsCancellationRequested)
        {
            try
            {
                joystick.Poll();
                var state = joystick.GetCurrentState();
                ApplyState(state);
            }
            catch
            {
                try { joystick.Acquire(); } catch { }
            }

            var delay = Math.Clamp(_calibration.PollIntervalMs, 1, 16);
            if (delay <= 1)
            {
                // Para el modo de menor input lag evitamos dormir demasiado tiempo.
                // Yield baja el consumo frente a un spin agresivo y mantiene respuesta rápida.
                Thread.Yield();
            }
            else
            {
                token.WaitHandle.WaitOne(delay);
            }

            // Evita que un joystick problemático haga un loop extremadamente agresivo si Poll retorna instantáneo.
            var now = stopwatch.ElapsedMilliseconds;
            if (now == lastTick) Thread.Yield();
            lastTick = now;
        }
    }

    private void ApplyState(JoystickState s)
    {
        if (_xbox is null) return;
        var bindings = _runtimeBindings;

        foreach (var b in bindings)
        {
            switch (b.Target)
            {
                case "A": SetXboxButton(Xbox360Button.A, GetDigital(s, b)); break;
                case "B": SetXboxButton(Xbox360Button.B, GetDigital(s, b)); break;
                case "X": SetXboxButton(Xbox360Button.X, GetDigital(s, b)); break;
                case "Y": SetXboxButton(Xbox360Button.Y, GetDigital(s, b)); break;
                case "LB": SetXboxButton(Xbox360Button.LeftShoulder, GetDigital(s, b)); break;
                case "RB": SetXboxButton(Xbox360Button.RightShoulder, GetDigital(s, b)); break;
                case "Back": SetXboxButton(Xbox360Button.Back, GetDigital(s, b)); break;
                case "Start": SetXboxButton(Xbox360Button.Start, GetDigital(s, b)); break;
                case "LS": SetXboxButton(Xbox360Button.LeftThumb, GetDigital(s, b)); break;
                case "RS": SetXboxButton(Xbox360Button.RightThumb, GetDigital(s, b)); break;
                case "DPadUp": SetXboxButton(Xbox360Button.Up, GetDigital(s, b)); break;
                case "DPadRight": SetXboxButton(Xbox360Button.Right, GetDigital(s, b)); break;
                case "DPadDown": SetXboxButton(Xbox360Button.Down, GetDigital(s, b)); break;
                case "DPadLeft": SetXboxButton(Xbox360Button.Left, GetDigital(s, b)); break;
                case "LeftStickX": _xbox.SetAxisValue(Xbox360Axis.LeftThumbX, AxisToShort(CalibrateAxis(GetAnalog(s, b), true))); break;
                case "LeftStickY": _xbox.SetAxisValue(Xbox360Axis.LeftThumbY, AxisToShort(CalibrateAxis(GetAnalog(s, b), true))); break;
                case "RightStickX": _xbox.SetAxisValue(Xbox360Axis.RightThumbX, AxisToShort(CalibrateAxis(GetAnalog(s, b), false))); break;
                case "RightStickY": _xbox.SetAxisValue(Xbox360Axis.RightThumbY, AxisToShort(CalibrateAxis(GetAnalog(s, b), false))); break;
                case "LeftTrigger": _xbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)CalibrateTrigger(GetAnalog(s, b))); break;
                case "RightTrigger": _xbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)CalibrateTrigger(GetAnalog(s, b))); break;
            }
        }
        _xbox.SubmitReport();
    }

    private void SetXboxButton(Xbox360Button button, bool pressed) => _xbox?.SetButtonState(button, pressed);

    private bool GetDigital(JoystickState s, Binding b)
    {
        if (b.Kind == SourceKind.Button)
        {
            var buttons = s.Buttons ?? Array.Empty<bool>();
            return buttons.Length > b.Index && buttons[b.Index];
        }
        if (b.Kind == SourceKind.Pov)
        {
            var pov = s.PointOfViewControllers?.FirstOrDefault() ?? -1;
            if (pov < 0) return false;
            return PovMatchesDiagonal(pov, b.Index);
        }
        if (b.Kind == SourceKind.Axis)
        {
            var v = GetAnalog(s, b);
            return b.Invert ? v < 20000 : v > 45535;
        }
        return false;
    }

    private static bool PovMatchesDiagonal(int pov, int dir)
    {
        return dir switch
        {
            0 => pov >= 31500 || pov <= 4500,
            1 => pov >= 4500 && pov <= 13500,
            2 => pov >= 13500 && pov <= 22500,
            3 => pov >= 22500 && pov <= 31500,
            _ => false
        };
    }

    private int GetAnalog(JoystickState s, Binding b)
    {
        if (b.Kind == SourceKind.Axis)
        {
            var axes = AxisValues(s);
            var v = axes[Math.Clamp(b.Index, 0, axes.Length - 1)];
            return b.Invert ? 65535 - v : v;
        }
        if (b.Kind == SourceKind.Button)
        {
            var buttons = s.Buttons ?? Array.Empty<bool>();
            var pressed = buttons.Length > b.Index && buttons[b.Index];
            return pressed ^ b.Invert ? 65535 : 0;
        }
        return 32768;
    }

    private static int[] AxisValues(JoystickState s)
    {
        var sliders = s.Sliders ?? Array.Empty<int>();
        return new[]
        {
            s.X, s.Y, s.Z, s.RotationX, s.RotationY, s.RotationZ,
            sliders.Length > 0 ? sliders[0] : 32768,
            sliders.Length > 1 ? sliders[1] : 32768
        };
    }

    private string BindingSourceText(Binding b) => b.Kind switch
    {
        SourceKind.Button => $"Button {b.Index + 1}",
        SourceKind.Axis => _axisNames[Math.Clamp(b.Index, 0, _axisNames.Length - 1)] + (b.Invert ? " (" + T("inverted") + ")" : ""),
        SourceKind.Pov => _povNames[Math.Clamp(b.Index, 0, _povNames.Length - 1)],
        _ => T("none")
    };

    private static short AxisToShort(double normalized)
    {
        return (short)Math.Clamp(Math.Round(normalized * 32767.0), short.MinValue, short.MaxValue);
    }

    private static byte AxisToByte(int value)
    {
        var clamped = Math.Clamp(value, 0, 65535);
        return (byte)(clamped / 257);
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


public sealed class VirtualTestState
{
    public Dictionary<string, bool> Buttons { get; } = new();
    public double LeftX { get; set; }
    public double LeftY { get; set; }
    public double RightX { get; set; }
    public double RightY { get; set; }
    public int LeftTrigger { get; set; }
    public int RightTrigger { get; set; }
}

public sealed class TestPadView : UserControl
{
    public VirtualTestState State { get; set; } = new();
    public string Language { get; set; } = "es";
    public CalibrationSettings Calibration { get; set; } = new();

    public TestPadView()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(245, 245, 245);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var text = new SolidBrush(Color.Black);
        using var body = new SolidBrush(Color.FromArgb(232, 236, 244));
        using var outline = new Pen(Color.FromArgb(70, 70, 70), 2);
        using var on = new SolidBrush(Color.FromArgb(30, 140, 60));
        using var off = new SolidBrush(Color.White);
        using var axisPen = new Pen(Color.FromArgb(60, 60, 60), 2);
        using var driftPen = new Pen(Color.FromArgb(180, 80, 20), 1);

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var bodyRect = new Rectangle(Math.Max(30, w / 2 - 240), 70, 480, Math.Max(250, h - 130));
        g.FillEllipse(body, bodyRect);
        g.DrawEllipse(outline, bodyRect);

        DrawStick(g, bodyRect.Left + 145, bodyRect.Top + 145, State.LeftX, State.LeftY, Language == "en" ? "Left Stick" : "Stick Izquierdo", axisPen, driftPen, text);
        DrawStick(g, bodyRect.Right - 145, bodyRect.Top + 200, State.RightX, State.RightY, Language == "en" ? "Right Stick" : "Stick Derecho", axisPen, driftPen, text);
        DrawDPad(g, bodyRect.Left + 120, bodyRect.Top + 260, text, on, off, outline);
        DrawFaceButtons(g, bodyRect.Right - 135, bodyRect.Top + 105, text, on, off, outline);
        DrawTrigger(g, bodyRect.Left + 80, bodyRect.Top - 25, State.LeftTrigger, "LT / L2", text, outline);
        DrawTrigger(g, bodyRect.Right - 180, bodyRect.Top - 25, State.RightTrigger, "RT / R2", text, outline);
        DrawSmallButton(g, bodyRect.Left + 210, bodyRect.Top + 210, "Back", IsOn("Back"), text, on, off, outline);
        DrawSmallButton(g, bodyRect.Left + 285, bodyRect.Top + 210, "Start", IsOn("Start"), text, on, off, outline);
        DrawSmallButton(g, bodyRect.Left + 195, bodyRect.Top + 20, "LB/L1", IsOn("LB"), text, on, off, outline);
        DrawSmallButton(g, bodyRect.Right - 265, bodyRect.Top + 20, "RB/R1", IsOn("RB"), text, on, off, outline);
        g.DrawString(Language == "en" ? "Drift zone: the dot should return to center when you release the stick" : "Zona de drift: el punto debe volver al centro al soltar el stick", Font, text, 20, h - 30);
    }

    private bool IsOn(string key) => State.Buttons.TryGetValue(key, out var v) && v;

    private void DrawStick(Graphics g, int cx, int cy, double x, double y, string label, Pen axisPen, Pen driftPen, Brush text)
    {
        const int r = 55;
        g.DrawEllipse(axisPen, cx - r, cy - r, r * 2, r * 2);
        g.DrawEllipse(driftPen, cx - 8, cy - 8, 16, 16);
        g.DrawLine(axisPen, cx - r, cy, cx + r, cy);
        g.DrawLine(axisPen, cx, cy - r, cx, cy + r);
        var px = cx + (int)(x * r);
        var py = cy - (int)(y * r);
        using var knob = new SolidBrush(Color.FromArgb(70, 120, 210));
        g.FillEllipse(knob, px - 11, py - 11, 22, 22);
        g.DrawEllipse(Pens.Black, px - 11, py - 11, 22, 22);
        g.DrawString(label, Font, text, cx - 40, cy + r + 8);
    }

    private void DrawFaceButtons(Graphics g, int cx, int cy, Brush text, Brush on, Brush off, Pen outline)
    {
        DrawRoundButton(g, cx, cy - 42, "Y / △", IsOn("Y"), text, on, off, outline);
        DrawRoundButton(g, cx + 42, cy, "B / ○", IsOn("B"), text, on, off, outline);
        DrawRoundButton(g, cx, cy + 42, "A / ✕", IsOn("A"), text, on, off, outline);
        DrawRoundButton(g, cx - 42, cy, "X / □", IsOn("X"), text, on, off, outline);
    }

    private void DrawDPad(Graphics g, int cx, int cy, Brush text, Brush on, Brush off, Pen outline)
    {
        DrawSmallButton(g, cx, cy - 36, "↑", IsOn("DPadUp"), text, on, off, outline);
        DrawSmallButton(g, cx + 36, cy, "→", IsOn("DPadRight"), text, on, off, outline);
        DrawSmallButton(g, cx, cy + 36, "↓", IsOn("DPadDown"), text, on, off, outline);
        DrawSmallButton(g, cx - 36, cy, "←", IsOn("DPadLeft"), text, on, off, outline);
    }

    private void DrawRoundButton(Graphics g, int cx, int cy, string label, bool active, Brush text, Brush on, Brush off, Pen outline)
    {
        var r = 31;
        g.FillEllipse(active ? on : off, cx - r, cy - r, r * 2, r * 2);
        g.DrawEllipse(outline, cx - r, cy - r, r * 2, r * 2);
        var size = g.MeasureString(label, Font);
        g.DrawString(label, Font, text, cx - size.Width / 2, cy - size.Height / 2);
    }

    private void DrawSmallButton(Graphics g, int cx, int cy, string label, bool active, Brush text, Brush on, Brush off, Pen outline)
    {
        var rect = new Rectangle(cx - 28, cy - 14, 56, 28);
        g.FillRectangle(active ? on : off, rect);
        g.DrawRectangle(outline, rect);
        var size = g.MeasureString(label, Font);
        g.DrawString(label, Font, text, cx - size.Width / 2, cy - size.Height / 2);
    }

    private void DrawTrigger(Graphics g, int x, int y, int value, string label, Brush text, Pen outline)
    {
        var rect = new Rectangle(x, y, 100, 22);
        g.DrawRectangle(outline, rect);
        using var fill = new SolidBrush(Color.FromArgb(70, 120, 210));
        g.FillRectangle(fill, rect.X + 1, rect.Y + 1, (int)((rect.Width - 2) * (value / 255.0)), rect.Height - 2);
        g.DrawString($"{label}: {value}", Font, text, x, y - 20);
    }
}
