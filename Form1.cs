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
            Directory.CreateDirectory(AppDataFolder);
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

            this.KeyPreview = true;
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;

            Task.Run(async () => await StartAuthentication());
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.Hide();
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
                this.Invoke(new Action(() => {
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
                this.Invoke(new Action(() => {
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

        private (string id, string secret, string? refresh) GetCredentials()
        {
            string path = ConfigPath;

            if (File.Exists(path))
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

            // Only open browser and prompt once
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

        private async void VolumeUp(object sender, HotkeyEventArgs e)
        {
            if (Spotify == null) return;
            try
            {
                CurrentVolume = Math.Min(100, CurrentVolume + VolumeChangeAmount);
                await Spotify.Player.SetVolume(new PlayerVolumeRequest(CurrentVolume));
                Log("Turned up volume to " + CurrentVolume);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Volume change failed: " + ex.Message);
                Log("Volume up failed: " + ex.Message);
            }
        }

        private async void VolumeDown(object sender, HotkeyEventArgs e)
        {
            if (Spotify == null) return;
            try
            {
                CurrentVolume = Math.Max(0, CurrentVolume - VolumeChangeAmount);
                await Spotify.Player.SetVolume(new PlayerVolumeRequest(CurrentVolume));
                Log("Turned down volume to " + CurrentVolume);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Volume change failed: " + ex.Message);
                Log("Volume down failed: " + ex.Message);
            }
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
                    CurrentVolume = playback.Device.VolumePercent ?? 50;
                    Log("Synced volume with Spotify: " + CurrentVolume);
                }
            }
            catch (Exception ex)
            {
                CurrentVolume = 50;
                Log("Failed to sync volume with Spotify: " + ex.Message);
            }
        }
    }
}
