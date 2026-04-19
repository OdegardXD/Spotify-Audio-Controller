# Spotify Audio Controller

A tray app that lets you control Spotify volume and skip songs with global hotkeys, using native Windows APIs. No premium or developer account required!

## Features
- Global hotkeys to raise/lower Spotify volume directly via the Windows Volume Mixer
- Global hotkeys to skip to the next or previous song
- Runs quietly in the system tray
- Config + log files stored in your local AppData folder

## Requirements
- A Windows PC
- Spotify desktop application running

## Setup & Run
Simply download the executable and run it! There is no setup, authentication, or login required.

## Configuration & Logs
All app data is stored here:

`%LOCALAPPDATA%\SpotifyAudioController`

Files:
- `config.txt` — Stores your configured volume step size and keybinds
- `log.txt` — Runtime logging for troubleshooting

## Notes
- The app runs in the system tray.
- You can change hotkeys and step size from the tray menu (the current configured values are displayed in parentheses next to the menu options).

## Copyright
I don’t care. Do whatever you want with it.  
If you copy it, please give me a shoutout in your repo.
