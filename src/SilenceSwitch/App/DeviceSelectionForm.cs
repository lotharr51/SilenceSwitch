namespace SilenceSwitch;

public sealed class DeviceSelectionForm : Form
{
    private readonly AppSettings _settings;
    private ListBox _deviceList = null!;
    private NumericUpDown _timeoutInput = null!;
    private CheckBox _startupCheckbox = null!;

    public DeviceSelectionForm(AppSettings settings)
    {
        _settings = settings;
        InitializeComponents();
        LoadDevices();
    }

    private void InitializeComponents()
    {
        Text = "SilenceSwitch - Select Headphone Device";
        Size = new Size(450, 400);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label
        {
            Text = "Select your headphone device:",
            Location = new Point(15, 15),
            AutoSize = true
        };

        _deviceList = new ListBox
        {
            Location = new Point(15, 40),
            Size = new Size(400, 180)
        };

        var timeoutLabel = new Label
        {
            Text = "Switch back after silence (minutes):",
            Location = new Point(15, 235),
            AutoSize = true
        };

        _timeoutInput = new NumericUpDown
        {
            Location = new Point(260, 233),
            Size = new Size(80, 25),
            Minimum = 1,
            Maximum = 120,
            Value = _settings.SilenceTimeoutMinutes
        };

        _startupCheckbox = new CheckBox
        {
            Text = "Switch to headphones on app startup",
            Location = new Point(15, 270),
            AutoSize = true,
            Checked = _settings.SwitchOnStartup
        };

        var okButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(230, 320),
            Size = new Size(90, 30)
        };
        okButton.Click += OnSave;

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(325, 320),
            Size = new Size(90, 30)
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.AddRange(new Control[]
        {
            label, _deviceList, timeoutLabel, _timeoutInput,
            _startupCheckbox, okButton, cancelButton
        });
    }

    private void LoadDevices()
    {
        using var manager = new AudioDeviceManager();
        var devices = manager.GetActiveRenderDevices();

        _deviceList.Items.Clear();
        _deviceList.DisplayMember = "FriendlyName";

        foreach (var device in devices)
            _deviceList.Items.Add(device);

        if (_settings.PreferredDeviceId != null)
        {
            for (int i = 0; i < _deviceList.Items.Count; i++)
            {
                if (((AudioDeviceManager.AudioDevice)_deviceList.Items[i]).Id == _settings.PreferredDeviceId)
                {
                    _deviceList.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (_deviceList.SelectedItem is not AudioDeviceManager.AudioDevice selected)
        {
            MessageBox.Show("Please select a device.", "SilenceSwitch",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        _settings.PreferredDeviceId = selected.Id;
        _settings.PreferredDeviceName = selected.FriendlyName;
        _settings.SilenceTimeoutMinutes = (int)_timeoutInput.Value;
        _settings.SwitchOnStartup = _startupCheckbox.Checked;
        _settings.Save();
    }
}
