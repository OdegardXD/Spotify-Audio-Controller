using NHotkey;
using NHotkey.WindowsForms;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.IO;
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

        // Changing Keybind Related
        private bool IsChangingKey = false;
        private bool IsUpKey = true;

        // AppData folder and file paths
        private static readonly string AppDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");
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
                    if (lines.Length >= 2)
                    {
                        if (lines.Length < 3 || lines[2] != token.RefreshToken)
                        {
                            File.WriteAllLines(ConfigPath, new[] { clientId, clientSecret, token.RefreshToken });
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

                File.WriteAllLines(ConfigPath, new[] { clientId, clientSecret, tokenResponse.RefreshToken });
                Log("Refresh token saved (first login).");

                var authenticator = new AuthorizationCodeAuthenticator(clientId, clientSecret, tokenResponse);

                authenticator.TokenRefreshed += (s, token) =>
                {
                    var lines = File.ReadAllLines(ConfigPath);
                    if (lines.Length >= 2)
                    {
                        if (lines.Length < 3 || lines[2] != token.RefreshToken)
                        {
                            File.WriteAllLines(ConfigPath, new[] { clientId, clientSecret, token.RefreshToken });
                            Log("Refresh token updated (browser login).");
                        }
                    }
                };

                Spotify = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator));

                await SyncVolumeWithSpotify();
                this.Invoke(new Action(() =>
                {
                    RegisterHotkeys();
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

        private (string id, string secret, string? refresh) GetCredentials() // Get credentials from config file or prompt user if not found
        {
            string path = ConfigPath; // variable for file path

            if (File.Exists(path)) // If file exists, read it
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length >= 2)
                {
                    string id = lines[0].Trim();
                    string secret = lines[1].Trim();
                    string? refresh = lines.Length >= 3 ? lines[2].Trim() : null;
                    return (id, secret, refresh);
                }
            }

            MessageBox.Show("First time setup! I'm opening the Spotify Developer Dashboard. Please copy your Client ID and Client Secret.", "Setup");
            BrowserUtil.Open(new Uri("https://developer.spotify.com/dashboard"));

            string idInput = Microsoft.VisualBasic.Interaction.InputBox("Enter Client ID:", "Setup", "");
            string secretInput = Microsoft.VisualBasic.Interaction.InputBox("Enter Client Secret:", "Setup", "");

            if (string.IsNullOrWhiteSpace(idInput) || string.IsNullOrWhiteSpace(secretInput))
            {
                MessageBox.Show("Credentials required. Closing.", "Error");
                Log("App closed: No credentials provided.");
                Environment.Exit(0);
            }

            File.WriteAllLines(path, new[] { idInput, secretInput });
            Log("Saved new Spotify Client ID and Secret.");
            return (idInput, secretInput, null);
        }

        private void RegisterHotkeys()
        {
            HotkeyManager.Current.AddOrReplace("VolUp", VolumeUpKey, VolumeUp);
            HotkeyManager.Current.AddOrReplace("VolDown", VolumeDownKey, VolumeDown);
        }

        private void VolumeUp(object sender, HotkeyEventArgs e)
        {
            if (Spotify == null) return;

            // 1. Instant local math
            CurrentVolume = Math.Min(100, CurrentVolume + VolumeChangeAmount);
            Log("User adjusted volume up to " + CurrentVolume);

            // 2. Fire and forget the API request!
            _ = SendVolumeUpdateAsync(CurrentVolume);
        }

        private void VolumeDown(object sender, HotkeyEventArgs e)
        {
            if (Spotify == null) return;

            // 1. Instant local math
            CurrentVolume = Math.Max(0, CurrentVolume - VolumeChangeAmount);
            Log("User adjusted volume down to " + CurrentVolume);

            // 2. Fire and forget the API request!
            _ = SendVolumeUpdateAsync(CurrentVolume);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Log("App exited by user.");
            Application.Exit();
        }

        private void changeVolumeUpKeybindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IsChangingKey = true;
            IsUpKey = true;
            this.Show();
            this.Activate();
            notifyIcon1.ShowBalloonTip(3000, "Binder", "Press your new UP combo now...", ToolTipIcon.Info);
            Log("User started changing volume up keybind.");
        }

        private void changeVolumeDownKeybindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IsChangingKey = true;
            IsUpKey = false;
            this.Show();
            this.Activate();
            notifyIcon1.ShowBalloonTip(3000, "Binder", "Press your new DOWN combo now...", ToolTipIcon.Info);
            Log("User started changing volume down keybind.");
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsChangingKey)
            {
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu)
                {
                    return;
                }

                e.SuppressKeyPress = true;

                if (IsUpKey)
                {
                    VolumeUpKey = e.KeyData;
                    Log("Changed volume up keybind to " + e.KeyData);
                }
                else
                {
                    VolumeDownKey = e.KeyData;
                    Log("Changed volume down keybind to " + e.KeyData);
                }

                IsChangingKey = false;
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
    }
}
