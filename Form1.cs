using NHotkey;
using NHotkey.WindowsForms;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace Spotify_Audio_Controller
{
    public partial class Form1 : Form
    {
        // Authentication Related
        private SpotifyClient Spotify; // unsure why this is needed
        string ClientID; // this is needed to authenticate with Spotify's API
        private static EmbedIOAuthServer AuthServer;

        // Volume Related
        private int CurrentVolume; // just the current volume
        private int VolumeChangeAmount = 5; // how much to change the volume by
        private Keys VolumeUpKey = Keys.Control | Keys.Up; // volume up key combo
        private Keys VolumeDownKey = Keys.Control | Keys.Down; // volume down key combo

        // Changing Keybind Related
        private bool IsChangingKey = false;
        private bool IsUpKey = true; // To track if we are changing 'Up' or 'Down'

        public Form1()
        {
            InitializeComponent();

            this.KeyPreview = true;

            this.ShowInTaskbar = false; // Hides program for taskbar as this is designed to run in tray
            this.WindowState = FormWindowState.Minimized; // Start the program minimized

            GetSecret();

            Task.Run(async () => await StartAuthentication());
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.Hide();
        }

        private string GetSecret()
        {
            string path = "config.txt";

            // 1. If file exists, just read it and return it
            if (File.Exists(path))
            {
                return File.ReadAllText(path).Trim();
            }

            // 2. Open the Spotify Dashboard for them
            // (A quick MessageBox first so they aren't startled when their browser randomly opens)
            MessageBox.Show("First time setup! I'm opening the Spotify Developer Dashboard. Please copy your 'Client Secret'.", "Setup");
            BrowserUtil.Open(new Uri("https://developer.spotify.com/dashboard/"));

            // 3. Ask the user for the Secret
            // Note: I removed "Paste Secret Here" because it forces the user to delete the text before pasting.
            // Leaving it blank is much more user-friendly!
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Please paste your Spotify Client Secret here:",
                "First Time Setup",
                "");

            if (!string.IsNullOrWhiteSpace(input))
            {
                File.WriteAllText(path, input);
                return input;
            }

            // 4. If they hit Cancel or leave it blank, kill the app completely
            MessageBox.Show("A Client Secret is required to run this app. Closing program.", "Error");
            Environment.Exit(0); // The absolute "kill switch" for the program
            return null;
        }

        private async Task StartAuthentication()
        {
            // Get both from our new method
            var (clientId, clientSecret) = GetCredentials();

            AuthServer = new EmbedIOAuthServer(new Uri("http://127.0.0.1:5000/callback"), 5000);
            await AuthServer.Start();

            AuthServer.AuthorizationCodeReceived += async (sender, response) =>
            {
                await AuthServer.Stop();

                var config = SpotifyClientConfig.CreateDefault();

                // Use the clientId and clientSecret we just loaded!
                var tokenResponse = await new OAuthClient(config).RequestToken(
                    new AuthorizationCodeTokenRequest(
                        clientId,
                        clientSecret,
                        response.Code,
                        new Uri("http://127.0.0.1:5000/callback")
                    )
                );

                Spotify = new SpotifyClient(tokenResponse.AccessToken);

                await SyncVolumeWithSpotify();

                this.Invoke(new Action(() =>
                {
                    RegisterHotkeys();
                    notifyIcon1.ShowBalloonTip(3000, "Spotify Controller", "Connected!", ToolTipIcon.Info);
                }));
            };

            var request = new LoginRequest(AuthServer.BaseUri, clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new[] { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState }
            };
            BrowserUtil.Open(request.ToUri());
        }

        private (string id, string secret) GetCredentials()
        {
            string path = "config.txt";

            // 1. If file exists, read both lines
            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                if (lines.Length >= 2)
                {
                    return (lines[0].Trim(), lines[1].Trim());
                }
            }

            // 2. First time setup: Open Dashboard and ask for both
            MessageBox.Show("First time setup! I'm opening the Spotify Dashboard. Copy your Client ID and Client Secret.", "Setup");
            BrowserUtil.Open(new Uri("https://developer.spotify.com/dashboard"));

            string idInput = Microsoft.VisualBasic.Interaction.InputBox("Paste your Client ID:", "Setup 1/2", "");
            if (string.IsNullOrWhiteSpace(idInput)) Environment.Exit(0);

            string secretInput = Microsoft.VisualBasic.Interaction.InputBox("Paste your Client Secret:", "Setup 2/2", "");
            if (string.IsNullOrWhiteSpace(secretInput)) Environment.Exit(0);

            // 3. Save to file (Line 1: ID, Line 2: Secret)
            File.WriteAllLines(path, new[] { idInput, secretInput });

            return (idInput, secretInput);
        }

        private void RegisterHotkeys()
        {
            // Bind Ctrl + Up to increase volume
            HotkeyManager.Current.AddOrReplace("VolUp", VolumeUpKey, VolumeUp);
            // Bind Ctrl + Down to decrease volume
            HotkeyManager.Current.AddOrReplace("VolDown", VolumeDownKey, VolumeDown);
        }

        private async void VolumeUp(object sender, HotkeyEventArgs e) // On volume up
        {
            if (Spotify == null) return;

            CurrentVolume = Math.Min(100, CurrentVolume + VolumeChangeAmount);
            await Spotify.Player.SetVolume(new PlayerVolumeRequest(CurrentVolume)); // send the volume change request to Spotify's API
        }

        private async void VolumeDown(object sender, HotkeyEventArgs e) // On volume down
        {
            if (Spotify == null) return;

            CurrentVolume = Math.Max(0, CurrentVolume - VolumeChangeAmount);
            await Spotify.Player.SetVolume(new PlayerVolumeRequest(CurrentVolume)); // send the volume change request to Spotify's API
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void changeVolumeUpKeybindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IsChangingKey = true;
            IsUpKey = true;
            this.Show();
            this.Activate(); // Forces the window to the front to catch keys
            notifyIcon1.ShowBalloonTip(3000, "Binder", "Press your new UP combo now...", ToolTipIcon.Info);
        }

        private void changeVolumeDownKeybindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            IsChangingKey = true;
            IsUpKey = false;
            this.Show();
            this.Activate();
            notifyIcon1.ShowBalloonTip(3000, "Binder", "Press your new DOWN combo now...", ToolTipIcon.Info);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsChangingKey)
            {
                // 1. If they only pressed Ctrl, Shift, or Alt, do nothing yet! 
                // We want to wait until they hit a second 'real' key.
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu)
                {
                    return;
                }

                // 2. Suppress the key so it doesn't 'ding' in Windows
                e.SuppressKeyPress = true;

                // 3. e.KeyData automatically combines the modifiers with the key
                // Example: If holding Ctrl and pressing Right, e.KeyData = (Keys.Control | Keys.Right)
                if (IsUpKey)
                    VolumeUpKey = e.KeyData;
                else
                    VolumeDownKey = e.KeyData;

                IsChangingKey = false;
                RegisterHotkeys(); // Update NHotkey with the new combo

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
                // Get the current playback status
                var playback = await Spotify.Player.GetCurrentPlayback();

                if (playback != null && playback.Device != null)
                {
                    CurrentVolume = playback.Device.VolumePercent ?? 50; // Default to 50 if it can't find it
                }
            }
            catch (Exception ex)
            {
                // If it fails (usually if no device is active), just leave it at a safe default
                CurrentVolume = 50;
            }
        }
    }
}
