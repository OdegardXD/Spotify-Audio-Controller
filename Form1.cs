using NAudio.CoreAudioApi;
using NHotkey;
using NHotkey.WindowsForms;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Spotify_Audio_Controller
{
    public partial class Form1 : Form
    {
        // Volume Related
        private int VolumeChangeAmount = 5;
        private Keys VolumeUpKey = Keys.Control | Keys.Up;
        private Keys VolumeDownKey = Keys.Control | Keys.Down;

        // Skip Related
        private Keys SkipNextKey = Keys.Control | Keys.Right;
        private Keys SkipPrevKey = Keys.Control | Keys.Left;

        // Changing Keybind Related
        private bool IsChangingKey = false;
        private enum KeyBindType { None, VolumeUp, VolumeDown, SkipNext, SkipPrev }
        private KeyBindType CurrentKeyBind = KeyBindType.None;

        // Path to C:\Users\<User>\AppData\Local\SpotifyAudioController
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpotifyAudioController"
        );
        private static readonly string ConfigPath = Path.Combine(AppDataFolder, "config.txt");
        private static readonly string LogPath = Path.Combine(AppDataFolder, "log.txt");

        // Windows API for media keys
        public const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const int KEYEVENTF_KEYUP = 0x0002;
        public const int VK_MEDIA_NEXT_TRACK = 0xB0;
        public const int VK_MEDIA_PREV_TRACK = 0xB1;

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        // Static constructor to ensure folder and log file are ready
        static Form1()
        {
            Directory.CreateDirectory(AppDataFolder);
            File.WriteAllText(LogPath, "");
        }

        public static void Log(string message)
        {
            string line = $"[{DateTime.Now:dd-MM-yyyy HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(LogPath, line);
        }

        public Form1()
        {
            InitializeComponent();
            Log("Started app...");

            this.KeyPreview = true;
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;

            LoadConfig();
            RegisterHotkeys();
            notifyIcon1.ShowBalloonTip(3000, "Spotify Controller", "Ready and running!", ToolTipIcon.Info);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.Hide();
        }

        private void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                var lines = File.ReadAllLines(ConfigPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("VolumeIncrementSize:"))
                    {
                        if (int.TryParse(line.Replace("VolumeIncrementSize:", "").Trim(), out int val))
                            VolumeChangeAmount = val;
                    }
                    if (line.StartsWith("VolumeUpKey:"))
                    {
                        if (Enum.TryParse(line.Replace("VolumeUpKey:", "").Trim(), out Keys key))
                            VolumeUpKey = key;
                    }
                    if (line.StartsWith("VolumeDownKey:"))
                    {
                        if (Enum.TryParse(line.Replace("VolumeDownKey:", "").Trim(), out Keys key))
                            VolumeDownKey = key;
                    }
                    if (line.StartsWith("SkipNextKey:"))
                    {
                        if (Enum.TryParse(line.Replace("SkipNextKey:", "").Trim(), out Keys key))
                            SkipNextKey = key;
                    }
                    if (line.StartsWith("SkipPrevKey:"))
                    {
                        if (Enum.TryParse(line.Replace("SkipPrevKey:", "").Trim(), out Keys key))
                            SkipPrevKey = key;
                    }
                }
            }
            else
            {
                // Create default config if it doesn't exist
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            string[] lines = {
                $"VolumeIncrementSize: {VolumeChangeAmount}",
                $"VolumeUpKey: {VolumeUpKey}",
                $"VolumeDownKey: {VolumeDownKey}",
                $"SkipNextKey: {SkipNextKey}",
                $"SkipPrevKey: {SkipPrevKey}"
            };
            File.WriteAllLines(ConfigPath, lines);
            Log("Config file saved with current settings.");
        }

        private void UpdateContextMenuText()
        {
            changeVolumeUpKeybindToolStripMenuItem.Text = $"Change Volume Up Keybind ({VolumeUpKey})";
            changeVolumeDownKeybindToolStripMenuItem.Text = $"Change Volume Down Keybind ({VolumeDownKey})";
            changeSkipNextKeybindToolStripMenuItem.Text = $"Change Next Song Keybind ({SkipNextKey})";
            changeSkipPrevKeybindToolStripMenuItem.Text = $"Change Previous Song Keybind ({SkipPrevKey})";
            changeVolumeChangeIncrementalToolStripMenuItem.Text = $"Set Volume Step Size ({VolumeChangeAmount})";
        }

        private void RegisterHotkeys()
        {
            UpdateContextMenuText();
            HotkeyManager.Current.AddOrReplace("VolUp", VolumeUpKey, VolumeUp);
            HotkeyManager.Current.AddOrReplace("VolDown", VolumeDownKey, VolumeDown);
            HotkeyManager.Current.AddOrReplace("SkipNext", SkipNextKey, SkipNext);
            HotkeyManager.Current.AddOrReplace("SkipPrev", SkipPrevKey, SkipPrev);
        }

        private void AdjustSpotifyVolume(float amount)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessionManager = defaultDevice.AudioSessionManager;
                var sessions = sessionManager.Sessions;

                bool spotifyFound = false;

                for (int i = 0; i < sessions.Count; i++)
                {
                    using var session = sessions[i];
                    if (session.GetProcessID != 0)
                    {
                        try
                        {
                            var process = Process.GetProcessById((int)session.GetProcessID);
                            if (process.ProcessName.Equals("Spotify", StringComparison.OrdinalIgnoreCase))
                            {
                                spotifyFound = true;
                                var volumeControl = session.SimpleAudioVolume;
                                float currentVol = volumeControl.Volume;
                                float newVol = currentVol + amount;
                                newVol = Math.Clamp(newVol, 0.0f, 1.0f);
                                volumeControl.Volume = newVol;
                                Log($"Adjusted Spotify volume to {Math.Round(newVol * 100)}%");
                            }
                        }
                        catch
                        {
                            // Ignore processes that might have exited
                        }
                    }
                }

                if (!spotifyFound)
                {
                    Log("Spotify process not found in audio sessions. Ensure Spotify is playing audio.");
                }
            }
            catch (Exception ex)
            {
                Log("Failed to adjust Spotify volume: " + ex.Message);
            }
        }

        private void VolumeUp(object sender, HotkeyEventArgs e)
        {
            AdjustSpotifyVolume(VolumeChangeAmount / 100f);
        }

        private void VolumeDown(object sender, HotkeyEventArgs e)
        {
            AdjustSpotifyVolume(-VolumeChangeAmount / 100f);
        }

        private void SkipNext(object sender, HotkeyEventArgs e)
        {
            keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_KEYUP, 0);
            Log("Skipped to next song via media key.");
        }

        private void SkipPrev(object sender, HotkeyEventArgs e)
        {
            keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_KEYUP, 0);
            Log("Skipped to previous song via media key.");
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Log("App exited by user.");
            Application.Exit();
        }

        private void changeVolumeUpKeybindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IsChangingKey = true;
            CurrentKeyBind = KeyBindType.VolumeUp;
            this.Show();
            this.Activate();
            notifyIcon1.ShowBalloonTip(3000, "Binder", "Press your new UP combo now...", ToolTipIcon.Info);
            Log("User started changing volume up keybind.");
        }

        private void changeVolumeDownKeybindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IsChangingKey = true;
            CurrentKeyBind = KeyBindType.VolumeDown;
            this.Show();
            this.Activate();
            notifyIcon1.ShowBalloonTip(3000, "Binder", "Press your new DOWN combo now...", ToolTipIcon.Info);
            Log("User started changing volume down keybind.");
        }

        private void changeSkipNextKeybindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IsChangingKey = true;
            CurrentKeyBind = KeyBindType.SkipNext;
            this.Show();
            this.Activate();
            notifyIcon1.ShowBalloonTip(3000, "Binder", "Press your new SKIP NEXT combo now...", ToolTipIcon.Info);
            Log("User started changing skip next keybind.");
        }

        private void changeSkipPrevKeybindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IsChangingKey = true;
            CurrentKeyBind = KeyBindType.SkipPrev;
            this.Show();
            this.Activate();
            notifyIcon1.ShowBalloonTip(3000, "Binder", "Press your new SKIP PREVIOUS combo now...", ToolTipIcon.Info);
            Log("User started changing skip previous keybind.");
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsChangingKey)
            {
                // Ignore modifier-only presses
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu)
                {
                    return;
                }

                e.SuppressKeyPress = true;

                if (CurrentKeyBind == KeyBindType.VolumeUp)
                {
                    VolumeUpKey = e.KeyData;
                    Log("Changed volume up keybind to " + e.KeyData);
                }
                else if (CurrentKeyBind == KeyBindType.VolumeDown)
                {
                    VolumeDownKey = e.KeyData;
                    Log("Changed volume down keybind to " + e.KeyData);
                }
                else if (CurrentKeyBind == KeyBindType.SkipNext)
                {
                    SkipNextKey = e.KeyData;
                    Log("Changed skip next keybind to " + e.KeyData);
                }
                else if (CurrentKeyBind == KeyBindType.SkipPrev)
                {
                    SkipPrevKey = e.KeyData;
                    Log("Changed skip prev keybind to " + e.KeyData);
                }

                SaveConfig();

                IsChangingKey = false;
                CurrentKeyBind = KeyBindType.None;
                RegisterHotkeys();

                this.Hide();
                notifyIcon1.ShowBalloonTip(2000, "Keybind Updated", $"New combo: {e.KeyData}", ToolTipIcon.Info);
                return;
            }

            base.OnKeyDown(e);
        }

        private void changeVolumeChangeIncrementalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string volumeStepSizeInput = Microsoft.VisualBasic.Interaction.InputBox("Enter Volume Step Size. Current is " + VolumeChangeAmount, "Change Volume Step Size", "");
            if (int.TryParse(volumeStepSizeInput, out int result))
            {
                result = Math.Clamp(result, 1, 100);
                VolumeChangeAmount = result;

                SaveConfig();
                UpdateContextMenuText();
                Log($"Volume step size changed to: {VolumeChangeAmount}");
                notifyIcon1.ShowBalloonTip(2000, "Update Success", $"Steps set to {VolumeChangeAmount}", ToolTipIcon.Info);
            }
        }
    }
}
