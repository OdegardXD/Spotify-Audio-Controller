# Spotify Audio Controller

A tray app that lets you control Spotify volume with global hotkeys using the Spotify Web API

## Features
- Global hotkeys to raise/lower Spotify volume
- Runs quietly in the system tray
- Config + log files stored in your local AppData folder

## Requirements
- A Spotify account
- A Spotify Developer app with Web API access
- Redirect URI set to `http://127.0.0.1:5000/callback`

## Setup
1. Open the Spotify Developer Dashboard: https://developer.spotify.com/dashboard  
2. Create a new app.
3. Add the Redirect URI: `http://127.0.0.1:5000/callback`
4. Make sure **API Used** includes **Web API**.

Example setup screenshot:  
<img width="1429" height="731" alt="image" src="https://github.com/user-attachments/assets/fb717c15-1eb2-42d3-847d-e3da3eb50c3d" />

## First Run
On first launch, the app will open the Spotify Developer Dashboard and prompt for:
- Client ID
- Client Secret

These are saved locally for future use.

## Configuration & Logs
All app data is stored here:

`%LOCALAPPDATA%\SpotifyAudioController`

Files:
- `config.txt` — Stores Client ID/Secret, refresh token, volume step size, and keybinds
- `log.txt` — Runtime logging for troubleshooting

## Notes
- The app runs in the system tray.
- You can change hotkeys and step size from the tray menu (the current configured values are displayed in parentheses next to the menu options).

## Copyright
I don’t care. Do whatever you want with it.  
If you copy it, please give me a shoutout in your repo.
