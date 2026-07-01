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
    private readonly CheckBox _useAllConnectedDevices = new() { AutoSize = true, Padding = new Padding(8, 6, 0, 0) };
    private readonly System.Windows.Forms.Timer _deviceWatchTimer = new() { Interval = 1200 };
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
    private readonly ComboBox _profileSelector = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Button _newProfile = new() { Width = 110, Height = 32 };
    private readonly Button _associateGame = new() { Width = 165, Height = 32 };
    private readonly Button _removeAssociation = new() { Width = 170, Height = 32 };
    private readonly Label _gameProfileStatus = new() { AutoSize = true, ForeColor = ModernTheme.MutedText, Padding = new Padding(0, 8, 0, 0) };
    private readonly System.Windows.Forms.Timer _gameWatcherTimer = new() { Interval = 2000 };
    private AppSettings _appSettings = new();
    private string _activeProfilePath = "";
    private bool _updatingProfileSelector;
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
    private readonly RuntimeDiagnostics _diagnostics = new();

    private readonly NumericUpDown _leftDeadzone = Num(8, 0, 50);
    private readonly NumericUpDown _rightDeadzone = Num(8, 0, 50);
    private readonly NumericUpDown _triggerDeadzone = Num(5, 0, 50);
    private readonly NumericUpDown _antiDeadzone = Num(0, 0, 40);
    private readonly NumericUpDown _sensitivity = Num(100, 25, 200);
    private readonly NumericUpDown _driftWarning = Num(12, 1, 50);
    private readonly NumericUpDown _pollInterval = Num(1, 1, 16);
    private readonly ComboBox _stickCurve = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _triggerCurve = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly Button _autoDeadzone = new() { Width = 180, Height = 32 };
    private readonly Label _calibrationHelp = new() { Dock = DockStyle.Top, Height = 58, Padding = new Padding(12, 8, 12, 4) };
    private readonly Label _wizardStatus = new() { AutoSize = true, ForeColor = ModernTheme.MutedText, Padding = new Padding(0, 8, 0, 0) };
    private readonly Button _captureCenter = new() { Width = 150, Height = 32 };
    private readonly Button _captureRange = new() { Width = 180, Height = 32 };
    private readonly Button _resetAxisCalibration = new() { Width = 160, Height = 32 };
    private bool _rangeCaptureActive;
    private DateTime _rangeCaptureEndsUtc;
    private readonly Dictionary<string, AxisCalibration> _rangeCapture = new();
    private CalibrationSettings _calibration = new();
    private volatile Binding[] _runtimeBindings = Array.Empty<Binding>();
    private int _runtimeRevision;

    private readonly ProfileRepository _profileRepository = new();
    private readonly AppLogger _fileLogger;
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
    // Prevent hot-plug refreshes from resetting the active test-device handle.
    private bool _refreshingDevices;

    // User data is kept outside the publish folder so updates do not overwrite profiles.
    private string AppDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SavagePadEmu");
    private string DefaultProfilePath => Path.Combine(ProfileDirectory, "Default.json");
    private string ProfileDirectory => Path.Combine(AppDataDirectory, "Profiles");
    private string ProfilePath => string.IsNullOrWhiteSpace(_activeProfilePath) ? DefaultProfilePath : _activeProfilePath;
    private string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");
    private string LegacyDefaultProfilePath => Path.Combine(AppContext.BaseDirectory, "profile.json");
    private string LegacyProfileDirectory => Path.Combine(AppContext.BaseDirectory, "Profiles");
    private string LegacySettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.json");

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
        _fileLogger = new AppLogger(AppDataDirectory);

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
        _useAllConnectedDevices.Text = T("allDevices");
        ModernTheme.StyleInput(_useAllConnectedDevices);
        top.Controls.Add(_useAllConnectedDevices);
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

        MigrateLegacyProfileData();
        LoadSettings();
        ApplyLanguage();
        LoadProfileOrDefaults();
        RefreshCalibrationUi();
        UpdateRuntimeBindings();
        RefreshMapperUi();
        RefreshDevices();
        _deviceWatchTimer.Tick += (_, _) => RefreshDevices(preserveSelection: true);
        _deviceWatchTimer.Start();

        _refresh.Click += (_, _) => RefreshDevices();
        _language.SelectedIndexChanged += LanguageChanged;
        _devices.SelectedIndexChanged += (_, _) => { if (!_refreshingDevices) ResetTestJoystick(); };
        _start.Click += (_, _) => StartEmulation();
        _stop.Click += (_, _) => StopEmulation();
        _save.Click += (_, _) => SaveProfile();
        _saveAs.Click += (_, _) => SaveProfileAs();
        _openProfileFolder.Click += (_, _) => OpenProfileFolder();
        _newProfile.Click += (_, _) => CreateNewProfile();
        _associateGame.Click += (_, _) => AssociateCurrentProfileWithGame();
        _removeAssociation.Click += (_, _) => RemoveCurrentProfileAssociation();
        _profileSelector.SelectedIndexChanged += (_, _) => ProfileSelectionChanged();
        _gameWatcherTimer.Tick += (_, _) => CheckGameProfileAutoSwitch();
        _gameWatcherTimer.Start();
        _load.Click += (_, _) => BrowseAndLoadProfile();
        _defaults.Click += (_, _) => { var defaultProfile = DefaultProfileFactory.Create(); lock (_bindingLock) _bindings = TargetCatalog.Normalize(defaultProfile.Bindings); _calibration = defaultProfile.Calibration; RefreshCalibrationUi(); ApplyCalibrationFromUi(); UpdateRuntimeBindings(); RefreshMapperUi(); Log(T("defaultRestored")); };
        _clearAll.Click += (_, _) => { lock (_bindingLock) _bindings = TargetCatalog.All.Select(t => new Binding { Target = t }).ToList(); UpdateRuntimeBindings(); RefreshMapperUi(); Log(T("mappingCleared")); };
        _testTimer.Tick += (_, _) => UpdateTestPad();
        _testTimer.Start();
        FormClosing += (_, _) => { _isClosing = true; _testTimer.Stop(); _gameWatcherTimer.Stop(); _deviceWatchTimer.Stop(); ResetTestJoystick(); StopEmulation(); };
    }


    private void ApplyModernChrome()
    {
        ModernTheme.StyleInput(_devices);
        ModernTheme.StyleInput(_language);
        ModernTheme.StyleInput(_useAllConnectedDevices);
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
        "rawLeftStick" => "Left stick RAW",
        "rawRightStick" => "Right stick RAW",
        "inputSampling" => "Input sampling",
        "readTime" => "Last input read",
        "virtualReports" => "Virtual reports",
        "virtualStatus" => "Virtual controller",
        "connected" => "Connected",
        "disconnected" => "Disconnected",
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
        "profileSaved" => "Profile saved.",
        "profileLoaded" => "Profile loaded.",
        "profileMissing" => "No profile was found. Loading the default mapping.",
        "profileLoadError" => "Could not load profile: ",
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
        "guidedCalibration" => "Guided stick calibration",
        "captureCenter" => "1. Capture center",
        "captureRange" => "2. Capture range (5s)",
        "finishRange" => "Finish range",
        "resetAxisCalibration" => "Reset stick calibration",
        "wizardReady" => "Leave both sticks centered, then capture the center. Next, move both sticks fully in every direction for 5 seconds.",
        "centerCaptured" => "Center captured. Now capture the range and move both sticks fully.",
        "rangeCapturing" => "Recording range: move both sticks fully in every direction...",
        "rangeCaptured" => "Stick range calibration saved.",
        "axisCalibrationReset" => "Saved stick centers/ranges were reset.",
        "profileManager" => "Profiles & games",
        "activeProfile" => "Active profile:",
        "newProfile" => "New profile...",
        "associateGame" => "Associate game .exe...",
        "removeGameAssociation" => "Remove game association",
        "noGameAssociation" => "No game is associated with this profile.",
        "gameAssociationSaved" => "Game association saved: ",
        "gameAssociationRemoved" => "Game association removed.",
        "profileAutoLoaded" => "Game detected. Loaded profile: ",
        "profileCreated" => "New profile created: ",
        "selectGameExe" => "Select the game executable",
        "selectProfileFirst" => "Save or select a profile first.",
        "profileLoadErrorFile" => "Could not load profile: ",
        "allDevices" => "Use all connected devices",
        "stickCurve" => "Stick response curve",
        "triggerCurve" => "Trigger response curve",
        "autoDeadzone" => "Auto-detect deadzone",
        "curveLinear" => "Linear",
        "curvePrecision" => "Precision",
        "curveAggressive" => "Aggressive",
        "curveSmooth" => "Smooth",
        "autoDeadzoneStarted" => "Measuring stick noise. Keep both sticks centered...",
        "autoDeadzoneSaved" => "Recommended deadzones were applied and saved.",
        "deviceDisconnected" => "Selected joystick was disconnected. Emulation stopped.",
        "multiDeviceActive" => "Combined input from connected devices is enabled.",

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
        "rawLeftStick" => "RAW stick izquierdo",
        "rawRightStick" => "RAW stick derecho",
        "inputSampling" => "Muestreo de entrada",
        "readTime" => "Última lectura",
        "virtualReports" => "Reportes virtuales",
        "virtualStatus" => "Mando virtual",
        "connected" => "Conectado",
        "disconnected" => "Desconectado",
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
        "profileSaved" => "Perfil guardado.",
        "profileLoaded" => "Perfil cargado.",
        "profileMissing" => "No se encontró un perfil. Cargo el mapeo default.",
        "profileLoadError" => "No se pudo cargar el perfil: ",
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
        "guidedCalibration" => "Calibración guiada de sticks",
        "captureCenter" => "1. Capturar centro",
        "captureRange" => "2. Capturar recorrido (5s)",
        "finishRange" => "Finalizar recorrido",
        "resetAxisCalibration" => "Restaurar calibración sticks",
        "wizardReady" => "Dejá ambos sticks centrados y capturá el centro. Luego capturá el recorrido y mové ambos sticks al máximo en todas las direcciones durante 5 segundos.",
        "centerCaptured" => "Centro capturado. Ahora capturá el recorrido y mové ambos sticks al máximo.",
        "rangeCapturing" => "Grabando recorrido: mové ambos sticks al máximo en todas las direcciones...",
        "rangeCaptured" => "Calibración de recorrido guardada.",
        "axisCalibrationReset" => "Se restauraron los centros/recorridos de sticks.",
        "profileManager" => "Perfiles y juegos",
        "activeProfile" => "Perfil activo:",
        "newProfile" => "Nuevo perfil...",
        "associateGame" => "Asociar .exe de juego...",
        "removeGameAssociation" => "Quitar asociación de juego",
        "noGameAssociation" => "No hay juego asociado a este perfil.",
        "gameAssociationSaved" => "Asociación de juego guardada: ",
        "gameAssociationRemoved" => "Asociación de juego eliminada.",
        "profileAutoLoaded" => "Juego detectado. Perfil cargado: ",
        "profileCreated" => "Nuevo perfil creado: ",
        "selectGameExe" => "Seleccioná el ejecutable del juego",
        "selectProfileFirst" => "Guardá o seleccioná un perfil primero.",
        "profileLoadErrorFile" => "No se pudo cargar el perfil: ",
        "allDevices" => "Usar todos los joysticks conectados",
        "stickCurve" => "Curva de respuesta sticks",
        "triggerCurve" => "Curva de respuesta gatillos",
        "autoDeadzone" => "Detectar deadzone automática",
        "curveLinear" => "Lineal",
        "curvePrecision" => "Precisión",
        "curveAggressive" => "Agresiva",
        "curveSmooth" => "Suave",
        "autoDeadzoneStarted" => "Midiendo ruido de sticks. Dejá ambos sticks centrados...",
        "autoDeadzoneSaved" => "Deadzones recomendadas aplicadas y guardadas.",
        "deviceDisconnected" => "El joystick seleccionado se desconectó. Emulación detenida.",
        "multiDeviceActive" => "Entrada combinada de joysticks conectados activada.",

        _ => key
    });

    private void ApplyLanguage()
    {
        Text = T("title");
        _deviceLabel.Text = T("device");
        _languageLabel.Text = T("language");
        _useAllConnectedDevices.Text = T("allDevices");
        _refresh.Text = T("refresh");
        _start.Text = T("start");
        _stop.Text = T("stop");
        _save.Text = T("save");
        _load.Text = T("load");
        _defaults.Text = T("defaults");
        _clearAll.Text = T("clearAll");
        _saveAs.Text = T("saveAs");
        _openProfileFolder.Text = T("profiles");
        _newProfile.Text = T("newProfile");
        _associateGame.Text = T("associateGame");
        _removeAssociation.Text = T("removeGameAssociation");
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

    private void MigrateLegacyProfileData()
    {
        try
        {
            Directory.CreateDirectory(ProfileDirectory);
            if (File.Exists(LegacyDefaultProfilePath) && !File.Exists(DefaultProfilePath))
                File.Copy(LegacyDefaultProfilePath, DefaultProfilePath);

            if (Directory.Exists(LegacyProfileDirectory))
            {
                foreach (var source in Directory.EnumerateFiles(LegacyProfileDirectory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var destination = Path.Combine(ProfileDirectory, Path.GetFileName(source));
                    if (!File.Exists(destination)) File.Copy(source, destination);
                }
            }

            if (File.Exists(LegacySettingsPath) && !File.Exists(SettingsPath))
                File.Copy(LegacySettingsPath, SettingsPath);
        }
        catch { }
    }

    private void LoadSettings()
    {
        if (_profileRepository.TryLoadSettings(SettingsPath, out var settings) && settings is not null)
        {
            _appSettings = settings;
            _appSettings.GameProfiles ??= new();
            _lang = settings.Language == "en" ? "en" : "es";
            _activeProfilePath = string.IsNullOrWhiteSpace(settings.ActiveProfilePath) ? DefaultProfilePath : NormalizeStoredProfilePath(settings.ActiveProfilePath);
        }
        else
        {
            _appSettings = new AppSettings();
            _activeProfilePath = DefaultProfilePath;
            _lang = "es";
        }
        _language.SelectedIndex = _lang == "en" ? 1 : 0;
    }

    private string NormalizeStoredProfilePath(string storedPath)
    {
        try
        {
            if (string.Equals(Path.GetFullPath(storedPath), Path.GetFullPath(LegacyDefaultProfilePath), StringComparison.OrdinalIgnoreCase))
                return DefaultProfilePath;
            if (Path.GetFullPath(storedPath).StartsWith(Path.GetFullPath(LegacyProfileDirectory), StringComparison.OrdinalIgnoreCase))
                return Path.Combine(ProfileDirectory, Path.GetFileName(storedPath));
        }
        catch { }
        return storedPath;
    }

    private void SaveSettings()
    {
        try
        {
            _appSettings.Language = _lang;
            _appSettings.ActiveProfilePath = ProfilePath;
            _appSettings.GameProfiles ??= new();
            _profileRepository.SaveSettings(SettingsPath, _appSettings);
        }
        catch { }
    }


    private ModernCard BuildProfileManagementCard()
    {
        var card = new ModernCard { Dock = DockStyle.Top, Height = 116, Padding = new Padding(18), Margin = new Padding(16) };
        var title = new Label { Text = T("profileManager"), Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI Semibold", 10F), ForeColor = ModernTheme.Text };
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(0, 4, 0, 0), WrapContents = false };
        row.Controls.Add(new Label { Text = T("activeProfile"), AutoSize = true, Padding = new Padding(0, 8, 8, 0), ForeColor = ModernTheme.Text });
        ModernTheme.StyleInput(_profileSelector);
        ModernTheme.StyleSecondaryButton(_newProfile);
        ModernTheme.StylePrimaryButton(_associateGame);
        ModernTheme.StyleSecondaryButton(_removeAssociation);
        row.Controls.Add(_profileSelector);
        row.Controls.Add(_newProfile);
        row.Controls.Add(_associateGame);
        row.Controls.Add(_removeAssociation);
        card.Controls.Add(_gameProfileStatus);
        _gameProfileStatus.Dock = DockStyle.Bottom;
        card.Controls.Add(row);
        card.Controls.Add(title);
        RefreshProfileSelector();
        RefreshGameAssociationStatus();
        return card;
    }

    private void BuildCalibrationPanel()
    {
        _calibrationTab.Controls.Clear();
        _calibrationHelp.Text = T("calibrationHelp");

        var wizardCard = new ModernCard
        {
            Dock = DockStyle.Top,
            Height = 126,
            Padding = new Padding(18),
            Margin = new Padding(16)
        };
        var wizardTitle = new Label
        {
            Text = T("guidedCalibration"),
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = ModernTheme.Text
        };
        var wizardButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(0, 4, 0, 0) };
        _captureCenter.Text = T("captureCenter");
        _captureRange.Text = T("captureRange");
        _resetAxisCalibration.Text = T("resetAxisCalibration");
        ModernTheme.StylePrimaryButton(_captureCenter);
        ModernTheme.StylePrimaryButton(_captureRange);
        ModernTheme.StyleSecondaryButton(_resetAxisCalibration);
        _captureCenter.Click -= CaptureCenterClicked;
        _captureCenter.Click += CaptureCenterClicked;
        _captureRange.Click -= CaptureRangeClicked;
        _captureRange.Click += CaptureRangeClicked;
        _resetAxisCalibration.Click -= ResetAxisCalibrationClicked;
        _resetAxisCalibration.Click += ResetAxisCalibrationClicked;
        wizardButtons.Controls.Add(_captureCenter);
        wizardButtons.Controls.Add(_captureRange);
        wizardButtons.Controls.Add(_resetAxisCalibration);
        wizardButtons.Controls.Add(_wizardStatus);
        wizardCard.Controls.Add(wizardButtons);
        wizardCard.Controls.Add(wizardTitle);

        var card = new ModernCard
        {
            Dock = DockStyle.Top,
            Padding = new Padding(18),
            Height = 420,
            Margin = new Padding(16)
        };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 11,
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
        AddCurveRow(panel, 7, T("stickCurve"), _stickCurve);
        AddCurveRow(panel, 8, T("triggerCurve"), _triggerCurve);
        _autoDeadzone.Text = T("autoDeadzone");
        ModernTheme.StyleSecondaryButton(_autoDeadzone);
        _autoDeadzone.Click -= AutoDeadzoneClicked;
        _autoDeadzone.Click += AutoDeadzoneClicked;
        panel.Controls.Add(_autoDeadzone, 1, 9);
        var apply = new Button { Text = _lang == "en" ? "Apply" : "Aplicar", Width = 120, Height = 32 };
        ModernTheme.StylePrimaryButton(apply);
        apply.Click += (_, _) => { ApplyCalibrationFromUi(); SaveProfile(); Log(T("settingsApplied")); };
        panel.Controls.Add(apply, 1, 10);
        card.Controls.Add(panel);

        var profilesCard = BuildProfileManagementCard();
        _calibrationTab.Controls.Add(card);
        _calibrationTab.Controls.Add(wizardCard);
        _calibrationTab.Controls.Add(profilesCard);
        _calibrationTab.Controls.Add(_calibrationHelp);
        UpdateCalibrationWizardText();
    }

    private static void AddCalibrationRow(TableLayoutPanel panel, int row, string label, NumericUpDown control, string hint)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = ModernTheme.Text }, 0, row);
        ModernTheme.StyleInput(control);
        panel.Controls.Add(control, 1, row);
        panel.Controls.Add(new Label { Text = hint, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = ModernTheme.MutedText }, 2, row);
    }

    private void AddCurveRow(TableLayoutPanel panel, int row, string label, ComboBox control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = ModernTheme.Text }, 0, row);
        control.Items.Clear();
        control.Items.AddRange(new object[] { T("curveLinear"), T("curvePrecision"), T("curveAggressive"), T("curveSmooth") });
        ModernTheme.StyleInput(control);
        panel.Controls.Add(control, 1, row);
    }

    private static ResponseCurve SelectedCurve(ComboBox control) => control.SelectedIndex switch
    {
        1 => ResponseCurve.Precision,
        2 => ResponseCurve.Aggressive,
        3 => ResponseCurve.Smooth,
        _ => ResponseCurve.Linear
    };

    private static void SetCurveSelection(ComboBox control, ResponseCurve curve)
    {
        if (control.Items.Count == 0) return;
        control.SelectedIndex = curve switch
        {
            ResponseCurve.Precision => 1,
            ResponseCurve.Aggressive => 2,
            ResponseCurve.Smooth => 3,
            _ => 0
        };
    }

    private async void AutoDeadzoneClicked(object? sender, EventArgs e)
    {
        _autoDeadzone.Enabled = false;
        _wizardStatus.Text = T("autoDeadzoneStarted");
        Log(T("autoDeadzoneStarted"));

        var maxLeft = 0.0;
        var maxRight = 0.0;
        var end = DateTime.UtcNow.AddSeconds(1.5);
        while (DateTime.UtcNow < end && !_isClosing)
        {
            if (TryGetCurrentTestState(out var state))
            {
                var left = Math.Sqrt(Math.Pow((state.LeftXRaw - 32768) / 32767.0, 2) + Math.Pow((state.LeftYRaw - 32768) / 32767.0, 2));
                var right = Math.Sqrt(Math.Pow((state.RightXRaw - 32768) / 32767.0, 2) + Math.Pow((state.RightYRaw - 32768) / 32767.0, 2));
                maxLeft = Math.Max(maxLeft, left);
                maxRight = Math.Max(maxRight, right);
            }
            await Task.Delay(20);
        }

        _leftDeadzone.Value = (decimal)Math.Clamp(Math.Ceiling((maxLeft * 1.35 + 0.01) * 100.0), 2, 30);
        _rightDeadzone.Value = (decimal)Math.Clamp(Math.Ceiling((maxRight * 1.35 + 0.01) * 100.0), 2, 30);
        ApplyCalibrationFromUi();
        SaveProfile();
        _wizardStatus.Text = T("autoDeadzoneSaved");
        Log(T("autoDeadzoneSaved"));
        _autoDeadzone.Enabled = true;
    }

    private void CaptureCenterClicked(object? sender, EventArgs e)
    {
        if (!TryGetCurrentTestState(out var state)) return;
        CaptureCenters(state);
        _rangeCaptureActive = false;
        ApplyCalibrationFromUi();
        SaveProfile();
        UpdateCalibrationWizardText(T("centerCaptured"));
        Log(T("centerCaptured"));
    }

    private void CaptureRangeClicked(object? sender, EventArgs e)
    {
        if (_rangeCaptureActive)
        {
            FinalizeRangeCapture();
            return;
        }

        if (!TryGetCurrentTestState(out var state)) return;
        CaptureCenters(state);
        _rangeCapture.Clear();
        foreach (var pair in _calibration.AxisCalibrations)
            _rangeCapture[pair.Key] = pair.Value.Clone();

        _rangeCaptureActive = true;
        _rangeCaptureEndsUtc = DateTime.UtcNow.AddSeconds(5);
        UpdateCalibrationWizardText(T("rangeCapturing"));
        Log(T("rangeCapturing"));
    }

    private void ResetAxisCalibrationClicked(object? sender, EventArgs e)
    {
        _rangeCaptureActive = false;
        _rangeCapture.Clear();
        _calibration.AxisCalibrations = new();
        ApplyCalibrationFromUi();
        SaveProfile();
        UpdateCalibrationWizardText(T("axisCalibrationReset"));
        Log(T("axisCalibrationReset"));
    }

    private bool TryGetCurrentTestState(out VirtualTestState state)
    {
        state = new VirtualTestState();
        if (_devices.SelectedIndex < 0 || _devices.SelectedIndex >= _deviceList.Count)
        {
            MessageBox.Show(T("selectJoystick"), "SavagePadEmu", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        try
        {
            var joystick = GetOrCreateTestJoystick(_deviceList[_devices.SelectedIndex].InstanceGuid);
            if (joystick is null) return false;
            joystick.Poll();
            state = BuildVirtualTestState(joystick.GetCurrentState());
            return true;
        }
        catch (Exception ex)
        {
            Log(T("testReadError") + ex.Message);
            return false;
        }
    }

    private void CaptureCenters(VirtualTestState state)
    {
        _calibration.AxisCalibrations ??= new();
        SetCenter("LeftStickX", state.LeftXRaw);
        SetCenter("LeftStickY", state.LeftYRaw);
        SetCenter("RightStickX", state.RightXRaw);
        SetCenter("RightStickY", state.RightYRaw);
    }

    private void SetCenter(string target, int value)
    {
        _calibration.AxisCalibrations[target] = new AxisCalibration
        {
            Center = value,
            Minimum = Math.Max(0, value - 1),
            Maximum = Math.Min(65535, value + 1)
        };
    }

    private void CaptureRangeSample(VirtualTestState state)
    {
        if (!_rangeCaptureActive) return;
        UpdateRange("LeftStickX", state.LeftXRaw);
        UpdateRange("LeftStickY", state.LeftYRaw);
        UpdateRange("RightStickX", state.RightXRaw);
        UpdateRange("RightStickY", state.RightYRaw);

        if (DateTime.UtcNow >= _rangeCaptureEndsUtc)
            FinalizeRangeCapture();
        else
            UpdateCalibrationWizardText(T("rangeCapturing"));
    }

    private void UpdateRange(string target, int value)
    {
        if (!_rangeCapture.TryGetValue(target, out var calibration)) return;
        calibration.Minimum = Math.Min(calibration.Minimum, value);
        calibration.Maximum = Math.Max(calibration.Maximum, value);
    }

    private void FinalizeRangeCapture()
    {
        _rangeCaptureActive = false;
        _calibration.AxisCalibrations = _rangeCapture
            .Where(pair => pair.Value.Minimum < pair.Value.Center - 400 && pair.Value.Maximum > pair.Value.Center + 400)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Clone());
        _rangeCapture.Clear();
        ApplyCalibrationFromUi();
        SaveProfile();
        UpdateCalibrationWizardText(T("rangeCaptured"));
        Log(T("rangeCaptured"));
    }

    private void UpdateCalibrationWizardText(string? message = null)
    {
        if (_rangeCaptureActive)
        {
            var remaining = Math.Max(0, (_rangeCaptureEndsUtc - DateTime.UtcNow).TotalSeconds);
            _wizardStatus.Text = $"{T("rangeCapturing")} {remaining:0.0}s";
            _captureRange.Text = T("finishRange");
            _captureCenter.Enabled = false;
            _resetAxisCalibration.Enabled = false;
            return;
        }

        _wizardStatus.Text = message ?? T("wizardReady");
        _captureRange.Text = T("captureRange");
        _captureCenter.Enabled = true;
        _resetAxisCalibration.Enabled = true;
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
        SetCurveSelection(_stickCurve, _calibration.StickResponseCurve);
        SetCurveSelection(_triggerCurve, _calibration.TriggerResponseCurve);
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
            PollIntervalMs = (int)_pollInterval.Value,
            StickResponseCurve = SelectedCurve(_stickCurve),
            TriggerResponseCurve = SelectedCurve(_triggerCurve),
            AxisCalibrations = _calibration.AxisCalibrations?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()) ?? new()
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
        AddTestRow(T("rawLeftStick"), "32768 / 32768", "RawLeftStick");
        AddTestRow(T("rightStick"), "0 / 0", "RightStick");
        AddTestRow(T("rawRightStick"), "32768 / 32768", "RawRightStick");
        AddTestRow("LT / L2", "0", "LeftTrigger");
        AddTestRow("RT / R2", "0", "RightTrigger");
        AddTestRow(T("driftLS"), T("ok"), "DriftLS");
        AddTestRow(T("driftRS"), T("ok"), "DriftRS");
        AddTestRow(T("inputSampling"), "0 Hz", "InputSampling");
        AddTestRow(T("readTime"), "0.00 ms", "ReadTime");
        AddTestRow(T("virtualReports"), "0 Hz", "VirtualReports");
        AddTestRow(T("virtualStatus"), T("disconnected"), "VirtualStatus");
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

            var readStarted = Stopwatch.GetTimestamp();
            var s = joystick.GetCurrentState();
            _diagnostics.RecordInputRead(Stopwatch.GetTimestamp() - readStarted);
            var st = BuildVirtualTestState(s);
            CaptureRangeSample(st);
            _testView.State = st;
            _testView.Invalidate();
            _visualMapper.State = st;
            _visualMapper.Invalidate();

            SetTestText("LeftStick", $"{st.LeftX:+0.000;-0.000;0.000} / {st.LeftY:+0.000;-0.000;0.000}");
            SetTestText("RightStick", $"{st.RightX:+0.000;-0.000;0.000} / {st.RightY:+0.000;-0.000;0.000}");
            SetTestText("LeftTrigger", $"{st.LeftTrigger} (RAW {st.LeftTriggerRaw})");
            SetTestText("RightTrigger", $"{st.RightTrigger} (RAW {st.RightTriggerRaw})");
            SetTestText("RawLeftStick", $"{st.LeftXRaw} / {st.LeftYRaw}");
            SetTestText("RawRightStick", $"{st.RightXRaw} / {st.RightYRaw}");
            var diagnostics = _diagnostics.Snapshot();
            SetTestText("InputSampling", $"{diagnostics.InputHz:0} Hz ({diagnostics.InputReads} total)");
            SetTestText("ReadTime", diagnostics.LastReadMs <= 0 ? "-" : $"{diagnostics.LastReadMs:0.000} ms");
            SetTestText("VirtualReports", $"{diagnostics.ReportHz:0} Hz ({diagnostics.VirtualReports} total)");
            SetTestText("VirtualStatus", _xbox is null ? T("disconnected") : T("connected"));
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

        testState.LeftX = InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, GetBinding("LeftStickX")), "LeftStickX", true, _calibration);
        testState.LeftY = InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, GetBinding("LeftStickY")), "LeftStickY", true, _calibration);
        testState.RightX = InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, GetBinding("RightStickX")), "RightStickX", false, _calibration);
        testState.RightY = InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, GetBinding("RightStickY")), "RightStickY", false, _calibration);
        testState.LeftTriggerRaw = InputMapper.GetAnalog(input, GetBinding("LeftTrigger"));
        testState.RightTriggerRaw = InputMapper.GetAnalog(input, GetBinding("RightTrigger"));
        testState.LeftTrigger = InputMapper.CalibrateTrigger(testState.LeftTriggerRaw, _calibration);
        testState.RightTrigger = InputMapper.CalibrateTrigger(testState.RightTriggerRaw, _calibration);
        testState.LeftXRaw = InputMapper.GetAnalog(input, GetBinding("LeftStickX"));
        testState.LeftYRaw = InputMapper.GetAnalog(input, GetBinding("LeftStickY"));
        testState.RightXRaw = InputMapper.GetAnalog(input, GetBinding("RightStickX"));
        testState.RightYRaw = InputMapper.GetAnalog(input, GetBinding("RightStickY"));
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
        Directory.CreateDirectory(ProfileDirectory);
        if (string.IsNullOrWhiteSpace(_activeProfilePath)) _activeProfilePath = DefaultProfilePath;
        List<Binding> copy;
        lock (_bindingLock) copy = TargetCatalog.Normalize(_bindings);
        var profile = new Profile { Name = Path.GetFileNameWithoutExtension(ProfilePath), Calibration = _calibration.Clone(), Bindings = copy };
        _profileRepository.SaveProfile(ProfilePath, profile);
        SaveSettings();
        RefreshProfileSelector();
        Log(T("profileSaved") + " " + ProfilePath);
    }

    private void BrowseAndLoadProfile()
    {
        Directory.CreateDirectory(ProfileDirectory);
        using var dialog = new OpenFileDialog
        {
            Filter = "SavagePadEmu profile (*.json)|*.json|JSON (*.json)|*.json",
            InitialDirectory = ProfileDirectory,
            Title = _lang == "en" ? "Open profile" : "Abrir perfil"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        LoadProfileFromPath(dialog.FileName, showError: true);
    }

    private void SaveProfileAs()
    {
        ApplyCalibrationFromUi();
        Directory.CreateDirectory(ProfileDirectory);
        using var dialog = new SaveFileDialog
        {
            Filter = "SavagePadEmu profile (*.json)|*.json|JSON (*.json)|*.json",
            FileName = "New profile.json",
            InitialDirectory = ProfileDirectory
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _activeProfilePath = dialog.FileName;
        SaveProfile();
        Log(T("profileSavedAs") + dialog.FileName);
    }

    private void CreateNewProfile()
    {
        Directory.CreateDirectory(ProfileDirectory);
        using var dialog = new SaveFileDialog
        {
            Filter = "SavagePadEmu profile (*.json)|*.json",
            FileName = "New profile.json",
            InitialDirectory = ProfileDirectory,
            Title = T("newProfile")
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _activeProfilePath = dialog.FileName;
        SaveProfile();
        RefreshProfileSelector();
        Log(T("profileCreated") + Path.GetFileNameWithoutExtension(_activeProfilePath));
    }

    private void OpenProfileFolder()
    {
        try
        {
            Directory.CreateDirectory(ProfileDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = ProfileDirectory, UseShellExecute = true });
        }
        catch { }
    }

    private void RefreshProfileSelector()
    {
        if (_updatingProfileSelector) return;
        _updatingProfileSelector = true;
        try
        {
            Directory.CreateDirectory(ProfileDirectory);
            var entries = new List<ProfileEntry>();
            if (File.Exists(DefaultProfilePath)) entries.Add(new ProfileEntry { Path = DefaultProfilePath, Name = "Default" });
            entries.AddRange(Directory.EnumerateFiles(ProfileDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.CurrentCultureIgnoreCase)
                .Select(path => new ProfileEntry { Path = path, Name = Path.GetFileNameWithoutExtension(path) }));
            if (!entries.Any(entry => string.Equals(entry.Path, ProfilePath, StringComparison.OrdinalIgnoreCase)))
                entries.Insert(0, new ProfileEntry { Path = ProfilePath, Name = Path.GetFileNameWithoutExtension(ProfilePath) });
            _profileSelector.Items.Clear();
            foreach (var entry in entries) _profileSelector.Items.Add(entry);
            var index = entries.FindIndex(entry => string.Equals(entry.Path, ProfilePath, StringComparison.OrdinalIgnoreCase));
            _profileSelector.SelectedIndex = Math.Max(0, index);
        }
        finally { _updatingProfileSelector = false; }
    }

    private void ProfileSelectionChanged()
    {
        if (_updatingProfileSelector || _profileSelector.SelectedItem is not ProfileEntry entry) return;
        if (string.Equals(entry.Path, ProfilePath, StringComparison.OrdinalIgnoreCase)) return;
        SaveProfile();
        LoadProfileFromPath(entry.Path, showError: true);
    }

    private void LoadProfileFromPath(string path, bool showError = false)
    {
        if (!_profileRepository.TryLoadProfile(path, out var profile, out var error) ||
            profile is null ||
            profile.Bindings is null ||
            profile.Bindings.Count <= 0)
        {
            if (error is not null) Log(T("profileLoadErrorFile") + error.Message);
            if (showError) MessageBox.Show(T("profileLoadErrorFile") + Path.GetFileName(path), "SavagePadEmu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // `profile` is verified non-null above; keeping a local non-null reference
        // prevents nullable-flow warnings and protects malformed JSON profiles.
        var loadedProfile = profile;
        _activeProfilePath = path;
        lock (_bindingLock) _bindings = TargetCatalog.Normalize(loadedProfile.Bindings);
        _calibration = loadedProfile.Calibration ?? new CalibrationSettings();
        RefreshCalibrationUi();
        ApplyCalibrationFromUi();
        UpdateRuntimeBindings();
        RefreshMapperUi();
        RefreshProfileSelector();
        RefreshGameAssociationStatus();
        SaveSettings();
        Log(T("profileLoaded"));
    }

    private void AssociateCurrentProfileWithGame()
    {
        SaveProfile();
        using var dialog = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe", Title = T("selectGameExe") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var fullPath = Path.GetFullPath(dialog.FileName);
        _appSettings.GameProfiles.RemoveAll(item => string.Equals(item.ExecutablePath, fullPath, StringComparison.OrdinalIgnoreCase));
        _appSettings.GameProfiles.Add(new GameProfileAssociation { ExecutablePath = fullPath, ProfilePath = ProfilePath, Enabled = true });
        SaveSettings();
        RefreshGameAssociationStatus();
        Log(T("gameAssociationSaved") + Path.GetFileName(fullPath));
    }

    private void RemoveCurrentProfileAssociation()
    {
        var removed = _appSettings.GameProfiles.RemoveAll(item => string.Equals(item.ProfilePath, ProfilePath, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) { SaveSettings(); Log(T("gameAssociationRemoved")); }
        else Log(T("noGameAssociation"));
        RefreshGameAssociationStatus();
    }

    private void RefreshGameAssociationStatus()
    {
        var games = _appSettings.GameProfiles.Where(item => string.Equals(item.ProfilePath, ProfilePath, StringComparison.OrdinalIgnoreCase))
            .Select(item => Path.GetFileName(item.ExecutablePath)).ToArray();
        _gameProfileStatus.Text = games.Length == 0 ? T("noGameAssociation") : string.Join(" • ", games);
    }

    private void CheckGameProfileAutoSwitch()
    {
        if (_isClosing || _isBinding || _appSettings.GameProfiles.Count == 0) return;
        foreach (var association in _appSettings.GameProfiles.Where(item => item.Enabled && File.Exists(item.ProfilePath)))
        {
            var processName = Path.GetFileNameWithoutExtension(association.ExecutablePath);
            if (string.IsNullOrWhiteSpace(processName)) continue;
            try
            {
                if (System.Diagnostics.Process.GetProcessesByName(processName).Length == 0) continue;
                if (string.Equals(association.ProfilePath, ProfilePath, StringComparison.OrdinalIgnoreCase)) return;
                LoadProfileFromPath(association.ProfilePath);
                Log(T("profileAutoLoaded") + Path.GetFileNameWithoutExtension(association.ProfilePath));
                return;
            }
            catch { }
        }
    }

    private void UpdateRuntimeBindings()
    {
        lock (_bindingLock)
            _runtimeBindings = TargetCatalog.Normalize(_bindings).Select(binding => binding.Clone()).ToArray();
        Interlocked.Increment(ref _runtimeRevision);
    }

    private void LoadProfileOrDefaults(bool forceFile = false)
    {
        _profileRepository.EnsureDefaultProfile(DefaultProfilePath);
        var path = ProfilePath;
        if (_profileRepository.TryLoadProfile(path, out var profile, out var error) && profile?.Bindings.Count > 0)
        {
            lock (_bindingLock) _bindings = TargetCatalog.Normalize(profile.Bindings);
            _calibration = profile.Calibration ?? new CalibrationSettings();
            RefreshCalibrationUi();
            ApplyCalibrationFromUi();
            UpdateRuntimeBindings();
            RefreshProfileSelector();
            return;
        }
        if (error is not null) Log(T("profileLoadError") + error.Message);
        if (forceFile) MessageBox.Show(T("profileMissing"), "SavagePadEmu");
        lock (_bindingLock) _bindings = TargetCatalog.CreateDefaultBindings();
        _calibration ??= new CalibrationSettings();
        UpdateRuntimeBindings();
        RefreshProfileSelector();
    }

    private void RefreshDevices(bool preserveSelection = false)
    {
        try
        {
            var selectedGuid = _devices.SelectedIndex >= 0 && _devices.SelectedIndex < _deviceList.Count
                ? _deviceList[_devices.SelectedIndex].InstanceGuid
                : Guid.Empty;

            using var directInput = new DirectInput();
            var fresh = directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AttachedOnly)
                .Concat(directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly))
                .GroupBy(d => d.InstanceGuid).Select(g => g.First()).ToList();

            if (_cts is not null && !_useAllConnectedDevices.Checked && selectedGuid != Guid.Empty &&
                !fresh.Any(item => item.InstanceGuid == selectedGuid))
            {
                Log(T("deviceDisconnected"));
                StopEmulation();
            }

            // The watcher runs periodically. Rebuilding the ComboBox on every tick triggers
            // SelectedIndexChanged, disposes the DirectInput test handle, and caused visible
            // one-frame releases while a button/stick was held. Only rebuild when the device
            // topology actually changes.
            var topologyChanged = fresh.Count != _deviceList.Count ||
                fresh.Select(d => d.InstanceGuid).OrderBy(g => g)
                    .SequenceEqual(_deviceList.Select(d => d.InstanceGuid).OrderBy(g => g)) == false;

            if (!topologyChanged)
            {
                if (!preserveSelection) Log($"{T("devicesFound")}{fresh.Count}");
                return;
            }

            var newSelectedGuid = selectedGuid != Guid.Empty && fresh.Any(item => item.InstanceGuid == selectedGuid)
                ? selectedGuid
                : fresh.FirstOrDefault()?.InstanceGuid ?? Guid.Empty;

            _refreshingDevices = true;
            try
            {
                _deviceList = fresh;
                _devices.BeginUpdate();
                _devices.Items.Clear();
                foreach (var device in _deviceList) _devices.Items.Add(device.InstanceName);
                var selectedIndex = newSelectedGuid == Guid.Empty ? -1 : _deviceList.FindIndex(item => item.InstanceGuid == newSelectedGuid);
                _devices.SelectedIndex = selectedIndex;
                _devices.EndUpdate();
            }
            finally
            {
                _refreshingDevices = false;
            }

            // Preserve the test device when the same physical joystick remains selected.
            if (newSelectedGuid != selectedGuid)
                ResetTestJoystick();

            if (!preserveSelection) Log($"{T("devicesFound")}{_devices.Items.Count}");
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
        var selectedGuids = _useAllConnectedDevices.Checked
            ? _deviceList.Select(item => item.InstanceGuid).ToList()
            : (_devices.SelectedIndex >= 0 && _devices.SelectedIndex < _deviceList.Count
                ? new List<Guid> { _deviceList[_devices.SelectedIndex].InstanceGuid }
                : new List<Guid>());
        if (selectedGuids.Count == 0)
        {
            MessageBox.Show(T("selectJoystick"), "SavagePadEmu");
            return;
        }
        if (_useAllConnectedDevices.Checked) Log(T("multiDeviceActive"));
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
            _ = Task.Run(() => PollLoop(selectedGuids, _cts.Token));
        }
        catch (Exception ex)
        {
            Log(T("error") + ex.Message);
            MessageBox.Show(T("startError") + "\n\n" + ex.Message, "SavagePadEmu");
            StopEmulation();
        }
    }

    private void PollLoop(IReadOnlyList<Guid> instanceGuids, CancellationToken token)
    {
        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        using var devices = new DirectInputDeviceSet(instanceGuids);

        ulong previousInput = ulong.MaxValue;
        var previousRevision = -1;
        while (!token.IsCancellationRequested)
        {
            var readStarted = Stopwatch.GetTimestamp();
            if (devices.TryRead(out var input))
            {
                _diagnostics.RecordInputRead(Stopwatch.GetTimestamp() - readStarted);
                var revision = Volatile.Read(ref _runtimeRevision);

                if (input.Fingerprint != previousInput || revision != previousRevision)
                {
                    ApplyState(input);
                    previousInput = input.Fingerprint;
                    previousRevision = revision;
                }
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
                case "LeftStickX": xbox.SetAxisValue(Xbox360Axis.LeftThumbX, AxisToShort(InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, binding), "LeftStickX", true, calibration))); break;
                case "LeftStickY": xbox.SetAxisValue(Xbox360Axis.LeftThumbY, AxisToShort(InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, binding), "LeftStickY", true, calibration))); break;
                case "RightStickX": xbox.SetAxisValue(Xbox360Axis.RightThumbX, AxisToShort(InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, binding), "RightStickX", false, calibration))); break;
                case "RightStickY": xbox.SetAxisValue(Xbox360Axis.RightThumbY, AxisToShort(InputMapper.CalibrateAxis(InputMapper.GetAnalog(input, binding), "RightStickY", false, calibration))); break;
                case "LeftTrigger": xbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)InputMapper.CalibrateTrigger(InputMapper.GetAnalog(input, binding), calibration)); break;
                case "RightTrigger": xbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)InputMapper.CalibrateTrigger(InputMapper.GetAnalog(input, binding), calibration)); break;
            }
        }
        xbox.SubmitReport();
        _diagnostics.RecordVirtualReport();
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
        _fileLogger.Write(message);
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
