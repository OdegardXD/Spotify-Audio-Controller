using NAudio.CoreAudioApi;
using NHotkey;
using NHotkey.WindowsForms;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using Swan.Parsers;
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
        // Authentication Related
        private SpotifyClient Spotify;
        string ClientID;
        private static EmbedIOAuthServer AuthServer;

        // Volume Related
        private int CurrentVolume;
        private int VolumeChangeAmount = 5;
        private Keys VolumeUpKey = Keys.Control | Keys.Up;
        private Keys VolumeDownKey = Keys.Control | Keys.Down;

        // Skip Related
        private Keys SkipNextKey = Keys.Control | Keys.Right;
        private Keys SkipPrevKey = Keys.Control | Keys.Left;

        public enum AppMode { Unset, API, WindowsAudio }
        private AppMode CurrentMode = AppMode.Unset;

        // Windows API for media keys
        public const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const int KEYEVENTF_KEYUP = 0x0002;
        public const int VK_MEDIA_NEXT_TRACK = 0xB0;
        public const int VK_MEDIA_PREV_TRACK = 0xB1;

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

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

        // Static constructor to ensure folder and log file are ready
        static Form1()
        {
            Directory.CreateDirectory(AppDataFolder); // Create the folder for storing config and log file
            File.WriteAllText(LogPath, ""); // Overwrite log file on each startup
        }

        // Logging method
        public static void Log(string message)
        {
            string line = $"[{DateTime.Now:dd-MM-yyyy HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(LogPath, line);
        }

        public Form1()
        {
            InitializeComponent();
            Log("Started app...");

            this.KeyPreview = true; // Some stuff to hide the main useless window
            this.ShowInTaskbar = false; // Some more stuff to hide the main useless window
            this.WindowState = FormWindowState.Minimized; // Even more stuff to hide the main useless window

            Task.Run(async () => await StartAuthentication()); // Start authentication on a different thread
        }

        protected override void OnLoad(EventArgs e) // If user tries to load window (which shoudlnt be possible)
        {
            base.OnLoad(e);
            this.Hide(); // Hide the main window if it somehow appears
        }

        private async Task StartAuthentication()
        {
            var (clientId, clientSecret, refreshToken) = GetCredentials();
            
            if (CurrentMode == AppMode.WindowsAudio)
            {
                this.Invoke(new Action(() =>
                {
                    RegisterHotkeys();
                    notifyIcon1.ShowBalloonTip(3000, "Spotify Controller", "Ready (Windows Audio Mode)", ToolTipIcon.Info);
                }));
                Log("Started in Windows Audio Mode.");
                return;
            }

            this.ClientID = clientId;

            // --- CASE A: We already have a Refresh Token (Silent Login) ---
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var authenticator = new AuthorizationCodeAuthenticator(clientId, clientSecret,
                    new AuthorizationCodeTokenResponse { RefreshToken = refreshToken });

                // Subscribe to token refreshed event to update config.txt
                authenticator.TokenRefreshed += (sender, token) =>
                {
                    var lines = File.ReadAllLines(ConfigPath);
                    if (lines.Length >= 3)
                    {
                        if (lines.Length < 4 || lines[3] != "RefreshToken: " + token.RefreshToken)
                        {
                            SaveConfig(clientId, clientSecret, token.RefreshToken, VolumeChangeAmount, VolumeUpKey, VolumeDownKey, SkipNextKey, SkipPrevKey);
                            Log("Refresh token updated (silent login).");
                        }
                    }
                };

                var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
                Spotify = new SpotifyClient(config);

                await SyncVolumeWithSpotify();
                this.Invoke(new Action(() =>
                {
                    RegisterHotkeys();
                    timer1.Start();
                    notifyIcon1.ShowBalloonTip(3000, "Spotify Controller", "Ready (Auto-Refreshed)", ToolTipIcon.Info);
                }));
                Log("Silent login successful.");
                return;
            }

            // --- CASE B: First time login (Browser Login) ---
            AuthServer = new EmbedIOAuthServer(new Uri("http://127.0.0.1:5000/callback"), 5000);
            await AuthServer.Start();

            AuthServer.AuthorizationCodeReceived += async (sender, response) =>
            {
                await AuthServer.Stop();
                var config = SpotifyClientConfig.CreateDefault();
                var tokenResponse = await new OAuthClient(config).RequestToken(
                    new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, new Uri("http://127.0.0.1:5000/callback"))
                );

                SaveConfig(clientId, clientSecret, tokenResponse.RefreshToken, VolumeChangeAmount, VolumeUpKey, VolumeDownKey, SkipNextKey, SkipPrevKey);
                Log("Refresh token saved (first login).");

                var authenticator = new AuthorizationCodeAuthenticator(clientId, clientSecret, tokenResponse);

                authenticator.TokenRefreshed += (s, token) =>
                {
                    var lines = File.ReadAllLines(ConfigPath);
                    if (lines.Length >= 3)
                    {
                        if (lines.Length < 4 || lines[3] != "RefreshToken: " + token.RefreshToken)
                        {
                            SaveConfig(clientId, clientSecret, token.RefreshToken, VolumeChangeAmount, VolumeUpKey, VolumeDownKey, SkipNextKey, SkipPrevKey);
                            Log("Refresh token updated (browser login).");
                        }
                    }
                };

                Spotify = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator));

                await SyncVolumeWithSpotify();
                this.Invoke(new Action(() =>
                {
                    RegisterHotkeys();
                    timer1.Start();
                    notifyIcon1.ShowBalloonTip(3000, "Spotify Controller", "Connected!", ToolTipIcon.Info);
                }));
                Log("First time login successful.");
            };

            var request = new LoginRequest(AuthServer.BaseUri, clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new[] { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState }
            };
            BrowserUtil.Open(request.ToUri());
        }

        private AppMode PromptForMode()
        {
            AppMode selectedMode = AppMode.Unset;
            using (Form prompt = new Form())
            {
                prompt.Width = 450;
                prompt.Height = 220;
                prompt.Text = "Select Application Mode";
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

                Label textLabel = new Label() { Left = 20, Top = 15, Width = 400, Height = 100, Text = "How do you want to control Spotify?\n\n- API Mode: Uses Spotify Web API (Requires Spotify Premium & Developer App Setup).\n- Windows Audio Mode: Changes Spotify's audio locally in Windows (No setup required, works instantly)." };
                Button apiButton = new Button() { Text = "Mode: API", Left = 20, Width = 120, Top = 130 };
                Button windowsAudioButton = new Button() { Text = "Mode: Windows Audio", Left = 150, Width = 140, Top = 130 };
                Button cancelButton = new Button() { Text = "Quit", Left = 300, Width = 100, Top = 130 };

                apiButton.Click += (sender, e) => { selectedMode = AppMode.API; prompt.Close(); };
                windowsAudioButton.Click += (sender, e) => { selectedMode = AppMode.WindowsAudio; prompt.Close(); };
                cancelButton.Click += (sender, e) => { prompt.Close(); };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(apiButton);
                prompt.Controls.Add(windowsAudioButton);
                prompt.Controls.Add(cancelButton);

                prompt.ShowDialog();
            }
            
            if (selectedMode == AppMode.Unset)
            {
                Environment.Exit(0);
            }
            return selectedMode;
        }

        private (string id, string secret, string? refresh) GetCredentials()
        {
            if (File.Exists(ConfigPath))
            {
                var lines = File.ReadAllLines(ConfigPath);
                string id = "", secret = "", refresh = "";
                string modeStr = "";

                foreach (var line in lines)
                {
                    if (line.StartsWith("Mode:")) modeStr = line.Replace("Mode:", "").Trim();
                    if (line.StartsWith("ClientID:")) id = line.Replace("ClientID:", "").Trim();
                    if (line.StartsWith("ClientSecret:")) secret = line.Replace("ClientSecret:", "").Trim();
                    if (line.StartsWith("RefreshToken:")) refresh = line.Replace("RefreshToken:", "").Trim();
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

                if (Enum.TryParse(modeStr, out AppMode parsedMode))
                {
                    CurrentMode = parsedMode;
                }

                if (CurrentMode == AppMode.Unset)
                {
                    Log("Config mode mismatch or missing. Wiping config.");
                    File.Delete(ConfigPath);
                }
                else
                {
                    // Valid mode found
                    if (CurrentMode == AppMode.API)
                    {
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(secret))
                            return (id, secret, string.IsNullOrEmpty(refresh) ? null : refresh);
                    }
                    else if (CurrentMode == AppMode.WindowsAudio)
                    {
                        // No credentials needed for Windows Audio mode
                        return ("", "", null);
                    }
                }
            }

            // --- First Time Setup Logic ---
            CurrentMode = PromptForMode();

            if (CurrentMode == AppMode.API)
            {
                MessageBox.Show("First time setup! I'm opening the Spotify Developer Dashboard.", "Setup");
                BrowserUtil.Open(new Uri("https://developer.spotify.com/dashboard"));

                string idInput = Microsoft.VisualBasic.Interaction.InputBox("Enter Client ID:", "Setup", "");
                string secretInput = Microsoft.VisualBasic.Interaction.InputBox("Enter Client Secret:", "Setup", "");

                if (string.IsNullOrWhiteSpace(idInput) || string.IsNullOrWhiteSpace(secretInput))
                {
                    Environment.Exit(0);
                }

                SaveConfig(idInput, secretInput, null, VolumeChangeAmount, VolumeUpKey, VolumeDownKey, SkipNextKey, SkipPrevKey);
                return (idInput, secretInput, null);
            }
            else
            {
                // Windows Audio Mode selected
                SaveConfig("", "", null, VolumeChangeAmount, VolumeUpKey, VolumeDownKey, SkipNextKey, SkipPrevKey);
                return ("", "", null);
            }
        }

        private void SaveConfig(string clientId, string clientSecret, string? refresh, int step, Keys upKey, Keys downKey, Keys nextKey, Keys prevKey)
        {
            string[] lines = {
                $"Mode: {CurrentMode}",
                $"ClientID: {clientId}",
                $"ClientSecret: {clientSecret}",
                $"RefreshToken: {refresh ?? ""}",
                $"VolumeIncrementSize: {step}",
                $"VolumeUpKey: {upKey}",
                $"VolumeDownKey: {downKey}",
                $"SkipNextKey: {nextKey}",
                $"SkipPrevKey: {prevKey}"
            };
            File.WriteAllLines(ConfigPath, lines);
            Log($"Config file saved with current settings (Mode: {CurrentMode}).");
        }

        private void UpdateContextMenuText()
        {
            currentModeToolStripMenuItem.Text = $"Current Mode: {CurrentMode}";
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
                        catch { }
                    }
                }

                if (!spotifyFound)
                    Log("Spotify process not found in audio sessions. Ensure Spotify is playing audio.");
            }
            catch (Exception ex)
            {
                Log("Failed to adjust Spotify volume: " + ex.Message);
            }
        }

        private void VolumeUp(object sender, HotkeyEventArgs e)
        {
            if (CurrentMode == AppMode.API)
            {
                if (Spotify == null) return;
                CurrentVolume = Math.Min(100, CurrentVolume + VolumeChangeAmount);
                Log("User adjusted volume up to " + CurrentVolume);
                _ = SendVolumeUpdateAsync(CurrentVolume);
            }
            else
            {
                AdjustSpotifyVolume(VolumeChangeAmount / 100f);
            }
        }

        private void VolumeDown(object sender, HotkeyEventArgs e)
        {
            if (CurrentMode == AppMode.API)
            {
                if (Spotify == null) return;
                CurrentVolume = Math.Max(0, CurrentVolume - VolumeChangeAmount);
                Log("User adjusted volume down to " + CurrentVolume);
                _ = SendVolumeUpdateAsync(CurrentVolume);
            }
            else
            {
                AdjustSpotifyVolume(-VolumeChangeAmount / 100f);
            }
        }

        private async Task SkipNextAsync()
        {
            try { await Spotify.Player.SkipNext(); Log("Skipped to next song."); }
            catch (Exception ex) { Log("Skip next failed: " + ex.Message); }
        }

        private async Task SkipPrevAsync()
        {
            try { await Spotify.Player.SkipPrevious(); Log("Skipped to previous song."); }
            catch (Exception ex) { Log("Skip prev failed: " + ex.Message); }
        }

        private void SkipNext(object sender, HotkeyEventArgs e)
        {
            if (CurrentMode == AppMode.API)
            {
                if (Spotify == null) return;
                _ = SkipNextAsync();
            }
            else
            {
                keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_KEYUP, 0);
                Log("Skipped to next song via media key.");
            }
        }

        private void SkipPrev(object sender, HotkeyEventArgs e)
        {
            if (CurrentMode == AppMode.API)
            {
                if (Spotify == null) return;
                _ = SkipPrevAsync();
            }
            else
            {
                keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY, 0);
                keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_KEYUP, 0);
                Log("Skipped to previous song via media key.");
            }
        }

        private void changeModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Log("User requested mode change. Wiping config and restarting.");
            if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
            Application.Restart();
            Environment.Exit(0);
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

                // --- SAVE THE NEW KEYBINDS HERE ---
                var (cid, csec, cref) = GetCredentials();
                SaveConfig(cid, csec, cref, VolumeChangeAmount, VolumeUpKey, VolumeDownKey, SkipNextKey, SkipPrevKey);

                IsChangingKey = false;
                CurrentKeyBind = KeyBindType.None;
                RegisterHotkeys();

                this.Hide();
                notifyIcon1.ShowBalloonTip(2000, "Keybind Updated", $"New combo: {e.KeyData}", ToolTipIcon.Info);
                return;
            }

            base.OnKeyDown(e);
        }

        private async Task SyncVolumeWithSpotify()
        {
            if (Spotify == null) return;

            try
            {
                var playback = await Spotify.Player.GetCurrentPlayback();

                if (playback != null && playback.Device != null)
                {
                    // Update the local variable with the actual cloud value
                    CurrentVolume = playback.Device.VolumePercent ?? 50;
                }
            }
            catch (Exception ex)
            {
                Log("Sync failed: " + ex.Message);
            }
        }

        private async void timer1_Tick(object sender, EventArgs e) // Added this so that IF the user decides to change volume manually in the spotify app then this will sync eventually. It runs on a timer that executes every 10000MS aka 10 seconds.
        {
            await SyncVolumeWithSpotify();
            Log("Synced Audio With Spotify (Timer)");
        }

        private async Task SendVolumeUpdateAsync(int volume)
        {
            try
            {
                await Spotify.Player.SetVolume(new PlayerVolumeRequest(volume));
            }
            catch (Exception ex)
            {
                // This catches the error in the background so it doesn't crash your app
                Log("Background volume update failed: " + ex.Message);
            }
        }

        private void changeVolumeChangeIncrementalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string volumeStepSizeInput = Microsoft.VisualBasic.Interaction.InputBox("Enter Volume Step Size. Current is " + VolumeChangeAmount, "Change Volume Step Size", "");
            if (int.TryParse(volumeStepSizeInput, out int result))
            {
                result = Math.Clamp(result, 1, 100); // clamp it

                var (id, secret, refresh) = GetCredentials(); // read config before updating the value
                VolumeChangeAmount = result; // set after GetCredentials so it doesn't get overwritten

                SaveConfig(id, secret, refresh, VolumeChangeAmount, VolumeUpKey, VolumeDownKey, SkipNextKey, SkipPrevKey); // save it to config

                UpdateContextMenuText();
                Log($"Volume step size changed to: {VolumeChangeAmount}");
                notifyIcon1.ShowBalloonTip(2000, "Update Success", $"Steps set to {VolumeChangeAmount}", ToolTipIcon.Info);
            }
        }
    }
}
