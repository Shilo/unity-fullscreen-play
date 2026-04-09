# Unity Fullscreen Play

Test your game in true fullscreen directly from the Unity Editor — no build required.

Adds a **Play Fullscreen** toggle that launches the Game view as a borderless fullscreen window when you enter Play mode. Press **Esc** or **F11** to return to the normal editor.

## Features

- **Play Fullscreen** toggle — automatically fullscreen every time you press Play
- **F11 hotkey** — toggle fullscreen on/off during Play mode (rebindable via Edit > Shortcuts)
- **Esc to exit** — press Escape to leave fullscreen without stopping Play
- **Toast notification** — brief overlay showing exit instructions (configurable)
- **Fullscreen Windowed** or **Exclusive Fullscreen** modes
- **Settings panel** in Edit > Preferences > Fullscreen Play
- **Windows** supported (macOS/Linux: fullscreen windowed only)

## Requirements

- Unity 6 (6000.0) or later
- Git (for package installation)

## Installation

### Option A — Git URL (recommended)

1. In Unity, open **Window > Package Manager**
2. Click the **+** button > **Install package from git URL...**
3. Paste:
   ```
   https://github.com/Shilo/unity-fullscreen-play.git
   ```

To pin a specific version, append a tag:
```
https://github.com/Shilo/unity-fullscreen-play.git#v0.1.0
```

### Option B — Edit manifest.json

Add this line to your project's `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.shilo.fullscreen-play": "https://github.com/Shilo/unity-fullscreen-play.git"
  }
}
```

## Usage

### Auto-fullscreen on Play
1. Go to **Edit > Fullscreen Play > Play Fullscreen** (toggle)
2. Press the Play button as usual — the Game view fills the entire screen
3. Press **Esc** or **F11** to exit fullscreen

### Manual fullscreen
- Press **F11** during Play mode to toggle fullscreen
- Or use **Edit > Fullscreen Play > Enter Fullscreen Now**

### Settings
Open **Edit > Preferences > Fullscreen Play** to configure:

| Setting | Default | Description |
|---------|---------|-------------|
| Play Fullscreen | Off | Auto-fullscreen on entering Play mode |
| Fullscreen Mode | Fullscreen Windowed | Borderless window or exclusive fullscreen |
| Enable F11 Hotkey | On | Allow F11 to toggle fullscreen |
| Show Toast | On | Show exit instructions overlay |
| Toast Duration | 3s | How long the toast is visible |

The F11 hotkey can be rebound in **Edit > Shortcuts** under "Fullscreen Play".

## How It Works

The package creates a second borderless `GameView` window via Unity's internal API and positions it to cover the entire screen. The original Game tab remains untouched — closing the fullscreen window simply returns you to the normal editor layout.

On Windows, native Win32 APIs ensure the window covers the taskbar and stays on top.

## License

[MIT](LICENSE)
