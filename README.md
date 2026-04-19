# Spotify Audio Controller

A tray app that lets you control Spotify volume and skip tracks with global hotkeys. 

The application now supports **Two Control Modes**:
1. **API Mode**: Uses the official Spotify Web API to control playback and volume. (Requires Spotify Premium and a Spotify Developer app).
2. **Windows Audio Mode**: Changes Spotify's volume locally in Windows (similar to changing it in volume mixer) and uses virtual media keys to control playback. (Does not require any Developer setup, works instantly).

## Features
- Selectable operating mode (API vs Local Windows Audio).
- Global hotkeys to raise/lower Spotify volume and skip next/prev.
- Runs quietly in the system tray.
- Change modes at any time from the tray menu.
- Config + log files stored in your local AppData folder.

## First Run
On first launch, the application will prompt you to select **Mode: API** or **Mode: Windows Audio**.
If you select **Windows Audio**, the app will immediately configure itself and start working.
If you select **API**, the app will open the Spotify Developer Dashboard and prompt for your Client ID and Client Secret.

## API Mode Setup
*If you are using API Mode, follow these steps:*
1. Open the Spotify Developer Dashboard: https://developer.spotify.com/dashboard  
2. Create a new app.
3. Add the Redirect URI: `http://127.0.0.1:5000/callback`
4. Make sure **API Used** includes **Web API**.

Example setup screenshot:  
<img width="1429" height="731" alt="image" src="https://github.com/user-attachments/assets/fb717c15-1eb2-42d3-847d-e3da3eb50c3d" />

## Configuration & Logs
All app data is stored here:

`%LOCALAPPDATA%\SpotifyAudioController`

Files:
- `config.txt` — Stores selected Mode, Client ID/Secret, refresh token, volume step size, and keybinds
- `log.txt` — Runtime logging for troubleshooting

## Notes
- The app runs in the system tray.
- You can change hotkeys and step size from the tray menu (the current configured values are displayed in parentheses next to the menu options).
- You can wipe your configuration and select a different mode by clicking "Change Mode" in the tray menu.

## Copyright
I don’t care. Do whatever you want with it.  
If you copy it, please give me a shoutout in your repo.
