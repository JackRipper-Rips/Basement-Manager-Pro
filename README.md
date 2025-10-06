# Morrenus Steam Mod Manager

A modern, Steam-like WPF application for managing Steam mods and tools via the Morrenus API.

## Features

- **Modern Steam UI** - Beautiful dark theme with rounded corners and gradients
- **Library Management** - View and manage installed Steam mods
- **Store Browser** - Search and download mods from the Morrenus API
- **Download Manager** - Track download progress with real-time updates
- **Auto-Installation** - Seamlessly install downloaded ZIP files
- **Steam Integration** - Auto-detect Steam installation and manage stplug-in folder
- **Settings** - Configure API keys, paths, and application behavior
- **Auto-Updates** - Check for new versions on GitHub

## Requirements

- **Windows 10/11**
- **.NET 8.0** or later
- **Steam** installed
- **Morrenus API Key** (starts with `smm`)

## Installation

### Option 1: Build from Source

1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

2. Clone or download this repository

3. Navigate to the project folder:
   ```bash
   cd MorrenusApp
   ```

4. Restore dependencies:
   ```bash
   dotnet restore
   ```

5. Build the project:
   ```bash
   dotnet build --configuration Release
   ```

6. Run the application:
   ```bash
   dotnet run --configuration Release
   ```

### Option 2: Publish Standalone Executable

1. Publish as a single executable:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

2. Find the executable in:
   ```
   bin\Release\net8.0-windows\win-x64\publish\MorrenusApp.exe
   ```

## Usage

### First Time Setup

1. **Launch the application**

2. **Go to Settings** (gear icon in sidebar)

3. **Enter your API Key** (must start with `smm`)
   - Click "Validate" to test the key

4. **Configure Steam Path** (usually auto-detected)
   - Click "Auto-Detect" to find Steam
   - Or use "Browse" to select manually

5. **Set Downloads Folder** (optional)
   - Default: `Documents\MorrenusApp\Downloads`

6. **Save Settings**

### Using the Store

1. Navigate to **Store** (shopping cart icon)

2. **Search for games** by name or App ID
   - Or click "Load All Games" to browse everything

3. **Click "Download"** on any game card
   - Downloads go to your configured downloads folder

### Managing Downloads

1. Navigate to **Downloads** (down arrow icon)

2. **Active Downloads** section shows current downloads
   - Progress bars update in real-time
   - Cancel or remove downloads

3. **Ready to Install** section shows downloaded ZIPs
   - Click "Install" to extract and install to Steam
   - Click trash icon to delete

### Viewing Your Library

1. Navigate to **Library** (home icon)

2. See all installed mods as game cards

3. **Uninstall** games by clicking the button on cards

4. **Restart Steam** to apply changes

## Project Structure

```
MorrenusApp/
├── Models/              # Data models (Game, Manifest, DownloadItem, Settings)
├── Services/            # Business logic services
│   ├── SteamService.cs         # Steam path detection & management
│   ├── ManifestApiService.cs   # Morrenus API integration
│   ├── DownloadService.cs      # Download management
│   ├── FileInstallService.cs   # File extraction & installation
│   ├── SettingsService.cs      # Settings persistence
│   └── UpdateService.cs        # Auto-update checker
├── ViewModels/          # MVVM ViewModels
│   ├── MainViewModel.cs
│   ├── LibraryViewModel.cs
│   ├── StoreViewModel.cs
│   ├── DownloadsViewModel.cs
│   └── SettingsViewModel.cs
├── Views/               # XAML UI pages
│   ├── MainWindow.xaml
│   ├── LibraryPage.xaml
│   ├── StorePage.xaml
│   ├── DownloadsPage.xaml
│   └── SettingsPage.xaml
├── Converters/          # Value converters for data binding
├── Resources/           # Styles and assets
│   └── Styles/
│       └── SteamTheme.xaml  # Modern Steam theme
└── App.xaml             # Application entry point
```

## API Integration

### Endpoint Format

```
https://manifest.morrenus.xyz/api/v1/manifest/{appid}?api_key={key}
```

### Expected Response

```json
{
  "appid": "480",
  "name": "Game Name",
  "description": "Game description",
  "version": "1.0",
  "size": 1048576,
  "icon_url": "https://...",
  "last_updated": "2025-01-01T00:00:00Z",
  "download_url": "https://..."
}
```

### Download Format

- ZIP file containing:
  - `{appid}.lua` - The Steam mod file
  - `manifest.json` - Metadata (optional)

## Configuration

### Settings File Location

```
%AppData%\MorrenusApp\settings.json
```

### Settings Schema

```json
{
  "SteamPath": "C:\\Program Files (x86)\\Steam",
  "ApiKey": "smm_your_key_here",
  "DownloadsPath": "C:\\Users\\...\\Downloads",
  "AutoCheckUpdates": true,
  "MinimizeToTray": true,
  "AutoInstallAfterDownload": false,
  "ShowNotifications": true,
  "ApiKeyHistory": ["smm_key1", "smm_key2"]
}
```

## Dependencies

- **Microsoft.Extensions.DependencyInjection** - Dependency injection
- **Microsoft.Extensions.Hosting** - Application hosting
- **CommunityToolkit.Mvvm** - MVVM helpers and commands
- **Newtonsoft.Json** - JSON serialization
- **System.IO.Compression** - ZIP file handling

## Troubleshooting

### Steam Not Detected

1. Go to Settings
2. Click "Auto-Detect"
3. If still not found, click "Browse" and select Steam folder manually
   - Default location: `C:\Program Files (x86)\Steam`

### API Key Invalid

1. Ensure key starts with `smm`
2. Click "Validate" to test connection
3. Check internet connection
4. Contact API provider if issues persist

### Downloads Not Installing

1. Check Steam path is correct
2. Ensure `stplug-in` folder exists:
   - Should be at: `{Steam}\config\stplug-in`
3. Run application as Administrator if permission errors occur

### Games Not Showing After Install

1. Restart Steam (use the button in Library page)
2. Wait 30-60 seconds for Steam to reload
3. Check if `.lua` file exists in `stplug-in` folder

## Development

### Adding New Features

1. **Services**: Add to `Services/` folder
2. **ViewModels**: Add to `ViewModels/` folder
3. **Views**: Add XAML to `Views/` folder
4. **Register in DI**: Update `App.xaml.cs`

### Modifying Theme

Edit `Resources/Styles/SteamTheme.xaml` to change:
- Colors
- Fonts
- Button styles
- Card styles

### Building for Release

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Future Enhancements

- [ ] Drag & drop for ZIP and .lua files
- [ ] System tray integration
- [ ] Game icons from API
- [ ] Batch install/uninstall
- [ ] Backup/restore functionality
- [ ] Statistics dashboard
- [ ] Download queue management
- [ ] Notification system
- [ ] Multi-language support

## License

This project is provided as-is. Modify and distribute as needed.

## Credits

- Built with WPF and .NET 8.0
- Inspired by Steam's modern UI
- Integrated with Morrenus API

## Support

For issues or questions:
1. Check the Troubleshooting section
2. Open an issue on GitHub
3. Contact the Morrenus API support

---

**Version**: 1.0.0
**Last Updated**: 2025-01-01
