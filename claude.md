# Basement Manager Pro - Claude Documentation

This document provides a comprehensive overview of the Basement Manager Pro application (formerly Solus-Manifest-App) to facilitate future development and maintenance.

---

## Application Overview

**Basement Manager Pro** is a WPF desktop application for managing Steam game manifests, backups, and related tools. It provides a store interface for downloading game manifests and utilities for Steam data management.

**Key Technologies:**
- **.NET 8.0** with WPF for UI
- **MVVM Pattern** using CommunityToolkit.Mvvm
- **Dependency Injection** via Microsoft.Extensions.DependencyInjection
- **SQLite** for local caching
- **Newtonsoft.Json** for JSON serialization

---

## Project Structure

```
Solus-Manifest-App/
├── App.xaml.cs                 # Application entry point & DI configuration
├── Views/                      # XAML UI pages and dialogs
│   ├── MainWindow.xaml        # Main application window with navigation
│   ├── HomePage.xaml          # Dashboard/welcome page
│   ├── StorePage.xaml         # Browse & download games from store
│   ├── LibraryPage.xaml       # Manage installed manifests
│   ├── DownloadsPage.xaml     # Download queue & progress
│   ├── DataBackupPage.xaml    # Steam data backup & restore
│   ├── LuaInstallerPage.xaml  # Lua script management
│   ├── ToolsPage.xaml         # Integrated tools access
│   ├── SettingsPage.xaml      # Application configuration
│   ├── SupportPage.xaml       # Help & support links
│   └── Dialogs/              # Various dialog windows
├── ViewModels/                # MVVM view models
│   ├── MainViewModel.cs       # Main navigation & app state
│   ├── StoreViewModel.cs      # Store page logic
│   ├── SettingsViewModel.cs   # Settings management
│   ├── DataBackupViewModel.cs # Backup functionality
│   └── ... (other ViewModels)
├── Services/                  # Business logic services
│   ├── ManifestApiService.cs  # Morrenus store API client
│   ├── BasementApiService.cs  # Basement store API client
│   ├── StoreApiFactory.cs     # Store provider selector
│   ├── DataBackupService.cs   # Steam backup operations
│   ├── SettingsService.cs     # Settings persistence
│   ├── SteamService.cs        # Steam path detection
│   ├── DownloadService.cs     # Download management
│   ├── CacheService.cs        # SQLite caching layer
│   ├── NotificationService.cs # Windows notifications
│   └── ... (30+ services total)
├── Models/                    # Data models
│   ├── AppSettings.cs         # Application settings model
│   ├── Manifest.cs            # Game manifest model
│   ├── LibraryResponse.cs     # Store API response models
│   ├── GameStatus.cs          # Game status model
│   ├── SteamUser.cs           # Steam user model
│   └── ... (other models)
├── Interfaces/                # Service contracts
│   ├── IManifestApiService.cs # Store API interface
│   └── ... (other interfaces)
├── Helpers/                   # Utility classes
└── Tools/                     # Integrated tools
    ├── ConfigVdfKeyExtractor/ # Extract depot keys from Steam
    ├── DepotDumper/           # Steam depot analysis
    └── SteamAuthPro/          # Steam authentication utilities
```

---

## Core Architecture

### Dependency Injection (App.xaml.cs)

All services and view models are registered in `App.xaml.cs` using Microsoft's DI container:

```csharp
services.AddSingleton<SettingsService>();
services.AddSingleton<ManifestApiService>();
services.AddSingleton<BasementApiService>();
services.AddSingleton<StoreApiFactory>();
services.AddSingleton<IManifestApiService>(sp => sp.GetRequiredService<StoreApiFactory>());
// ... more services
services.AddTransient<StoreViewModel>();
// ... view models
```

**Key Registration Pattern:**
- **Singletons:** Services that maintain state (SettingsService, CacheService, etc.)
- **Transients:** ViewModels created fresh for each navigation
- **Interfaces:** Enable testability and swappable implementations

### Navigation System (MainViewModel.cs)

Navigation is centralized in `MainViewModel` using RelayCommands:

```csharp
[RelayCommand]
private void NavigateToStore()
{
    if (!CanNavigateAway()) return;
    CurrentPage = GetOrCreateView("Store", () => new StorePage { DataContext = StoreViewModel });
    CurrentPageName = "Store";
    StoreViewModel.OnNavigatedTo();
}
```

**Navigation Flow:**
1. User clicks navigation button in MainWindow.xaml
2. Command binding invokes corresponding method in MainViewModel
3. View is created or retrieved from cache
4. CurrentPage property updates, triggering UI change via binding
5. ViewModel lifecycle methods are called (OnNavigatedTo, etc.)

**Adding New Pages:**
1. Create View (XAML + code-behind) in `Views/`
2. Create ViewModel in `ViewModels/`
3. Register ViewModel in `App.xaml.cs`
4. Inject ViewModel into MainViewModel constructor
5. Add navigation command in MainViewModel
6. Add navigation button in MainWindow.xaml
7. Add case to MainViewModel.NavigateTo() switch statement

---

## Store Provider System

### Two Store Implementations

The application supports switching between two store providers:

#### 1. **Basement Store** (Default)
- **Service:** `BasementApiService.cs`
- **Base URL:** Configurable (default: `http://localhost:5000/api/v1`)
- **API Key:** Any non-empty string
- **Use Case:** Self-hosted or alternative manifest store

#### 2. **Morrenus Store** (Original)
- **Service:** `ManifestApiService.cs`
- **Base URL:** `https://manifest.morrenus.xyz/api/v1`
- **API Key:** Must start with `smm` prefix
- **Use Case:** Official Morrenus manifest repository

### Store Selection Logic

**StoreApiFactory** (`Services/StoreApiFactory.cs`) delegates to the correct implementation:

```csharp
private IManifestApiService GetActiveService()
{
    var settings = _settingsService.LoadSettings();
    return settings.SelectedStore == StoreProvider.Basement
        ? _basementService
        : _morrenusService;
}
```

**Settings Model** (`Models/AppSettings.cs`):
```csharp
public enum StoreProvider { Basement, Morrenus }

public StoreProvider SelectedStore { get; set; } = StoreProvider.Basement;
public string BasementApiKey { get; set; } = string.Empty;
public string BasementApiUrl { get; set; } = "http://localhost:5000/api/v1";
```

**User Configuration:**
- Settings → Store Provider dropdown
- Basement: Configure API URL + API Key
- Morrenus: Configure API Key only (must start with "smm")

### Store API Endpoints

Both stores implement the same `IManifestApiService` interface:

```csharp
Task<LibraryResponse?> GetLibraryAsync(string apiKey, int limit = 100, int offset = 0, string? search = null, string sortBy = "updated");
Task<Manifest?> GetManifestAsync(string appId, string apiKey);
Task<GameStatus?> GetGameStatusAsync(string appId, string apiKey);
Task<SearchResponse?> SearchLibraryAsync(string query, string apiKey, int limit = 50);
Task<bool> TestApiKeyAsync(string apiKey);
bool ValidateApiKey(string apiKey);
```

**Key Methods:**
- `GetLibraryAsync()` - Paginated game list (used by StorePage)
- `GetManifestAsync()` - Fetch individual game manifest details
- `GetGameStatusAsync()` - Check if manifest is ready for download (polls during download)
- `TestApiKeyAsync()` - Validate API key credentials

**Full API Documentation:** See `STORE_API_DOCUMENTATION.md`

---

## Settings System

### Settings Storage

**Location:** `%AppData%\BasementManagerPro\settings.json`

**Service:** `SettingsService.cs`
- `LoadSettings()` - Reads from JSON file
- `SaveSettings(AppSettings)` - Persists to JSON file
- Thread-safe singleton

**Model:** `AppSettings.cs` (90+ properties)

**Key Settings Categories:**
- **Store Configuration:** Selected provider, API keys, API URLs
- **Steam Paths:** Steam directory, depot downloader paths
- **Downloads:** Download directory, auto-install, delete after install
- **Display:** Theme (8 themes available), window size/position
- **Behavior:** Notifications, confirmations, auto-update mode
- **Pagination:** Store/Library page sizes
- **Tool Modes:** SteamTools vs DepotDownloader

### Settings UI (SettingsPage.xaml)

**Tabbed Interface:**
1. **General Tab:**
   - Store Provider selection (Basement vs Morrenus)
   - API key configuration (conditional based on selected store)
   - Theme selection
   - Pagination settings

2. **Additional Tabs:** (Implementation varies)
   - Steam configuration
   - Download preferences
   - Notifications
   - Advanced options

**Validation:**
- API keys validated via `TestApiKeyAsync()` before saving
- Steam paths verified via `SteamService.ValidateSteamPath()`
- Unsaved changes indicator (`HasUnsavedChanges` property)

---

## Steam Data Backup System

### Overview

**DataBackupService** provides backup/restore functionality for Steam user data.

**Location:** `Services/DataBackupService.cs`
**ViewModel:** `ViewModels/DataBackupViewModel.cs`
**View:** `Views/DataBackupPage.xaml`

### Backup Categories

#### 1. **Playtime Backup**
- **Source:** `<SteamPath>/userdata/<AccountID>/config/localconfig.vdf`
- **Contains:** Game playtime data, last played timestamps
- **Format:** ZIP archive (`<AccountID>_playtime_backup_<timestamp>.zip`)
- **Methods:** `BackupPlaytime()`, `RestorePlaytime()`

#### 2. **Achievements Backup**
- **Source:** `<SteamPath>/appcache/stats/UserGameStats_<AccountID>_*.bin`
- **Contains:** Achievement unlock data per game
- **Format:** ZIP archive (`<AccountID>_achievements_backup_<timestamp>.zip`)
- **Methods:** `BackupAchievements()`, `RestoreAchievements()`

#### 3. **Added Games Backup** (GreenLuma/SteamTools)
- **Source:** `<SteamPath>/config/stplug-in/*.lua`
- **Contains:** Lua scripts for added games
- **Format:** ZIP archive (`<AccountID>_st_games_backup_<timestamp>.zip`)
- **Methods:** `BackupAddedGames()`, `RestoreAddedGames()`

### Steam User Detection

**Method:** `GetCurrentSteamUser()`
- Parses `<SteamPath>/config/loginusers.vdf`
- Extracts SteamID64, AccountName, PersonaName, Timestamp
- Finds user with MostRecent flag or newest timestamp
- Converts SteamID64 to AccountID: `SteamID64 - 76561197960265728`

**Model:** `SteamUser.cs`
```csharp
public class SteamUser
{
    public string SteamId64 { get; set; }
    public string AccountId { get; set; }
    public string AccountName { get; set; }
    public string PersonaName { get; set; }
    public bool IsMostRecent { get; set; }
    public long Timestamp { get; set; }
}
```

### Achievement Management

**List Games:** `GetGamesWithAchievements()`
- Scans for `UserGameStats_<AccountID>_*.bin` files
- Returns list of Game objects with AppId
- ViewModel resolves game names via SteamApiService

**Reset Achievements:**
- `ResetAchievements(List<appIds>)` - Delete specific games' achievement files
- `ResetAllAchievements()` - Delete all achievement files for current user

---

## Download System

### Download Service (DownloadService.cs)

**Key Method:** `DownloadGameFileOnlyAsync(Manifest, string outputDirectory)`

**Download Flow:**
1. Check game status via `GetGameStatusAsync()` - wait if `UpdateInProgress` is true
2. Poll status every 5 seconds until ready
3. Download ZIP file from manifest.DownloadUrl
4. Track progress with `DownloadProgressChanged` event
5. Optionally auto-install (extract ZIP)
6. Optionally delete ZIP after install
7. Update `ManifestStorageService` with installation info

**Installation Tracking:**
- **Service:** `ManifestStorageService.cs`
- **Storage:** `%AppData%\SolusManifestApp\Manifests\manifest_index.json`
- **Data:** AppId, GameName, InstallDate, InstallPath, DepotIds
- **Purpose:** Track installed manifests, detect updates

### Download Page UI

**ViewModel:** `DownloadsViewModel.cs`
**View:** `DownloadsPage.xaml`

**Features:**
- Queue display with progress bars
- Pause/Resume/Cancel buttons
- Concurrent download limit (configurable)
- Auto-install toggle per download
- Notifications on completion/errors

---

## Caching System

### CacheService (CacheService.cs)

**Database:** SQLite (`%AppData%\SolusManifestApp\cache.db`)

**Cached Data:**
1. **Game Icons** - Header images from Steam CDN
2. **Game Status** - Status responses (5-minute TTL)

**Methods:**
- `GetIconAsync(url)` - Retrieve cached icon or download if missing
- `CacheGameStatus(appId, json)` - Store status response with timestamp
- `IsGameStatusCacheValid(appId, expiration)` - Check if cached status is still valid

**Performance Benefits:**
- Reduces API calls during status polling
- Prevents re-downloading static images
- Faster page loads on revisit

---

## Notification System

### NotificationService (NotificationService.cs)

**Technology:** `Microsoft.Toolkit.Uwp.Notifications` (Windows Toast)

**Methods:**
- `ShowNotification(title, message, NotificationType)` - General notification
- `ShowSuccess(message, title)` - Success notification
- `ShowError(message, title)` - Error notification
- `ShowWarning(message)` - Warning notification

**Settings Control:**
- `DisableAllNotifications` - Master switch
- `ShowNotifications` - General notifications toggle
- `ShowGameAddedNotification` - Specific notification type

**Usage Pattern:**
```csharp
_notificationService.ShowSuccess("Download completed!", "Success");
_notificationService.ShowError($"Failed: {ex.Message}", "Download Error");
```

---

## Theme System

### ThemeService (ThemeService.cs)

**Available Themes:**
1. Default
2. Dark
3. Light
4. Cherry
5. Sunset
6. Forest
7. Grape
8. Cyberpunk

**Resource Dictionaries:** Located in `Resources/Themes/`

**Dynamic Brushes:** All themes use the same DynamicResource names:
- `PrimaryBackgroundBrush`
- `SecondaryDarkBrush`
- `CardBackgroundBrush`
- `TextPrimaryBrush`
- `TextSecondaryBrush`
- `AccentBrush`
- `BorderBrush`

**Applying Theme:**
```csharp
_themeService.ApplyTheme(AppTheme.Dark);
```

**Persistence:** Selected theme saved in `AppSettings.Theme`

---

## Logging System

### LoggerService (LoggerService.cs)

**Location:** `%AppData%\SolusManifestApp\logs\`
**Format:** `app_YYYYMMDD.log`

**Log Levels:**
- INFO
- ERROR
- WARNING

**Usage:**
```csharp
_logger.Log("INFO", "Operation completed successfully");
_logger.Log("ERROR", $"Failed to process: {ex.Message}");
```

**Log Rotation:** New file created daily

---

## Update System

### UpdateService (UpdateService.cs)

**Update Source:** GitHub Releases API

**Auto-Update Modes:**
1. **Disabled** - No update checking
2. **CheckOnly** - Notify user, prompt for update
3. **AutoDownloadAndInstall** - Silent auto-update on startup

**Flow:**
1. Check for updates via GitHub API
2. Compare version numbers
3. If update available:
   - CheckOnly: Show dialog
   - AutoDownloadAndInstall: Download silently
4. Download update EXE to temp folder
5. Show notification when ready
6. Launch installer and exit app

**Configuration:** `AppSettings.AutoUpdate` (enum)

---

## Protocol Handler

### Purpose

Enables `basement://` URL scheme for external application launching.

**Service:** `ProtocolHandlerService.cs`
**Registration:** `ProtocolRegistrationHelper.cs` (auto-registers on startup)

**Example URL:**
```
basement://install?appid=480
basement://download?url=...
```

**Handler Flow:**
1. Windows launches app with protocol URL as argument
2. `App.OnStartup()` detects URL argument
3. `ProtocolHandlerService.HandleProtocolAsync()` processes URL
4. Navigate to appropriate page or trigger action

---

## Key Dependencies

### NuGet Packages (from MorrenusApp.csproj)

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.10" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.1" />
<PackageReference Include="Microsoft.Toolkit.Uwp.Notifications" Version="7.1.3" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="protobuf-net" Version="3.2.56" />
<PackageReference Include="QRCoder" Version="1.6.0" />
<PackageReference Include="SteamKit2" Version="3.3.1" />
```

**Key Libraries:**
- **CommunityToolkit.Mvvm:** MVVM helpers (ObservableObject, RelayCommand)
- **Newtonsoft.Json:** JSON serialization
- **Microsoft.Data.Sqlite:** SQLite database access
- **SteamKit2:** Steam server communication
- **Microsoft.Extensions:** Dependency injection & hosting

---

## Common Development Tasks

### Adding a New Page

**Example: Adding a "Statistics" page**

1. **Create View:**
   ```xml
   <!-- Views/StatisticsPage.xaml -->
   <UserControl x:Class="SolusManifestApp.Views.StatisticsPage"
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
       <Grid>
           <!-- Page content -->
       </Grid>
   </UserControl>
   ```

2. **Create ViewModel:**
   ```csharp
   // ViewModels/StatisticsViewModel.cs
   public partial class StatisticsViewModel : ObservableObject
   {
       public StatisticsViewModel() { }
   }
   ```

3. **Register in DI:**
   ```csharp
   // App.xaml.cs
   services.AddTransient<StatisticsViewModel>();
   ```

4. **Add to MainViewModel:**
   ```csharp
   // ViewModels/MainViewModel.cs
   public StatisticsViewModel StatisticsViewModel { get; }

   public MainViewModel(..., StatisticsViewModel statisticsViewModel)
   {
       StatisticsViewModel = statisticsViewModel;
   }

   [RelayCommand]
   private void NavigateToStatistics()
   {
       if (!CanNavigateAway()) return;
       CurrentPage = GetOrCreateView("Statistics", () => new StatisticsPage { DataContext = StatisticsViewModel });
       CurrentPageName = "Statistics";
   }

   public void NavigateTo(string pageName)
   {
       switch (pageName.ToLower())
       {
           // ... existing cases
           case "statistics":
               NavigateToStatistics();
               break;
       }
   }
   ```

5. **Add Navigation Button:**
   ```xml
   <!-- Views/MainWindow.xaml -->
   <Button Style="{StaticResource NavButtonStyle}"
           Command="{Binding NavigateToStatisticsCommand}"
           Tag="{Binding CurrentPageName, Converter={StaticResource PageNameToTagConverter}, ConverterParameter=Statistics}">
       <StackPanel Orientation="Horizontal">
           <TextBlock Text="📊" FontSize="14" Margin="0,0,6,0"/>
           <TextBlock Text="Statistics" FontSize="13"/>
       </StackPanel>
   </Button>
   ```

### Adding a New Service

**Example: Adding a "StatisticsService"**

1. **Create Service:**
   ```csharp
   // Services/StatisticsService.cs
   public class StatisticsService
   {
       private readonly SettingsService _settingsService;
       private readonly LoggerService _logger;

       public StatisticsService(SettingsService settingsService, LoggerService logger)
       {
           _settingsService = settingsService;
           _logger = logger;
       }

       public int GetTotalDownloads()
       {
           // Implementation
       }
   }
   ```

2. **Register in DI:**
   ```csharp
   // App.xaml.cs
   services.AddSingleton<StatisticsService>();
   ```

3. **Inject into ViewModels:**
   ```csharp
   // ViewModels/StatisticsViewModel.cs
   private readonly StatisticsService _statisticsService;

   public StatisticsViewModel(StatisticsService statisticsService)
   {
       _statisticsService = statisticsService;
   }
   ```

### Adding a New Setting

**Example: Adding "MaxDownloadSpeed" setting**

1. **Add to Model:**
   ```csharp
   // Models/AppSettings.cs
   public int MaxDownloadSpeed { get; set; } = 0; // 0 = unlimited
   ```

2. **Add ViewModel Property:**
   ```csharp
   // ViewModels/SettingsViewModel.cs
   [ObservableProperty]
   private int _maxDownloadSpeed;

   partial void OnMaxDownloadSpeedChanged(int value) => MarkAsUnsaved();
   ```

3. **Load/Save:**
   ```csharp
   // ViewModels/SettingsViewModel.cs - LoadSettings()
   MaxDownloadSpeed = Settings.MaxDownloadSpeed;

   // ViewModels/SettingsViewModel.cs - SaveSettings()
   Settings.MaxDownloadSpeed = MaxDownloadSpeed;
   ```

4. **Add UI Control:**
   ```xml
   <!-- Views/SettingsPage.xaml -->
   <TextBlock Text="Max Download Speed (KB/s, 0 = unlimited)"/>
   <TextBox Text="{Binding MaxDownloadSpeed, UpdateSourceTrigger=PropertyChanged}"/>
   ```

---

## Important Code Patterns

### MVVM Observable Properties

Using CommunityToolkit.Mvvm:

```csharp
[ObservableProperty]
private string _myProperty = "default value";

// Generated: public string MyProperty { get; set; }
// Generated: Implements INotifyPropertyChanged automatically
```

### Relay Commands

```csharp
[RelayCommand]
private async Task DoSomethingAsync()
{
    // Async command implementation
    await Task.Delay(1000);
}

// Generated: public IAsyncRelayCommand DoSomethingCommand { get; }
// XAML: Command="{Binding DoSomethingCommand}"
```

### Partial Methods for Property Changes

```csharp
[ObservableProperty]
private string _apiKey = string.Empty;

partial void OnApiKeyChanged(string value)
{
    // Called automatically when ApiKey changes
    HasUnsavedChanges = true;
}
```

### Service Injection Pattern

```csharp
private readonly SettingsService _settingsService;
private readonly LoggerService _logger;

public MyViewModel(SettingsService settingsService, LoggerService logger)
{
    _settingsService = settingsService;
    _logger = logger;
}
```

### Safe Navigation

```csharp
private bool CanNavigateAway()
{
    if (HasUnsavedChanges)
    {
        var result = MessageBoxHelper.Show(
            "You have unsaved changes. Discard them?",
            "Unsaved Changes",
            MessageBoxButton.YesNo);
        return result == MessageBoxResult.Yes;
    }
    return true;
}
```

---

## File Paths & Data Locations

**AppData Directory:** `%AppData%\BasementManagerPro\`

```
BasementManagerPro/
├── settings.json              # Application settings
├── Cache/                     # Cache directory
│   ├── Icons/                # Cached game icons
│   └── Data/                 # Cached API responses
├── logs/                      # Application logs (deprecated - now BasementManagerPro.log)
├── BasementManagerPro.log     # Main application log
├── Manifests/                 # Installed manifest tracking
│   └── manifest_index.json
└── ... (other data files)
```

**Steam Paths (typical):**
- Windows: `C:\Program Files (x86)\Steam\`
- Config: `<SteamPath>\config\`
- UserData: `<SteamPath>\userdata\<AccountID>\`
- Stats: `<SteamPath>\appcache\stats\`

---

## Troubleshooting & Common Issues

### Issue: Store API Returns 401 Unauthorized
**Cause:** Invalid API key
**Solution:**
1. Verify API key in Settings → Store Provider
2. Test key using "Validate" button
3. For Morrenus: Ensure key starts with "smm"
4. For Basement: Ensure Basement API URL is correct

### Issue: Downloads Stuck in "Waiting" State
**Cause:** Game manifest is being updated (`UpdateInProgress = true`)
**Solution:** Service will auto-retry every 5 seconds. Wait for server-side update to complete.

### Issue: Steam Path Not Detected
**Cause:** Non-standard Steam installation
**Solution:**
1. Settings → General → Browse for Steam path
2. Select `steam.exe` file
3. Service will extract directory path

### Issue: Backup Fails with "User Not Found"
**Cause:** No logged-in Steam user or loginusers.vdf missing
**Solution:**
1. Launch Steam at least once
2. Log in to create user profile
3. Verify `<SteamPath>\config\loginusers.vdf` exists

### Issue: Theme Not Applying
**Cause:** Theme resources not loaded
**Solution:**
1. Check `Resources/Themes/` directory exists
2. Verify selected theme name matches available themes
3. Restart application

---

## Build & Deployment

**Target Framework:** net8.0-windows10.0.19041.0
**Output Type:** WinExe (Windows executable)
**Self-Contained:** Yes (includes .NET runtime)
**Single-File:** Yes (all dependencies bundled)
**Platform:** win-x64

**Build Command:**
```bash
dotnet build
```

**Publish Command:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Output:** `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\`

---

## API Integration Notes

### Switching Store Providers at Runtime

Users can switch between Basement and Morrenus stores without restarting:

1. Settings → Store Provider → Select provider
2. Configure API key for selected provider
3. Click "Save Settings"
4. Navigate to Store page
5. **Factory automatically uses new provider**

**Implementation:**
- `StoreApiFactory` reads settings on every call
- No restart required
- All existing code using `IManifestApiService` works transparently

### Creating Custom Store Implementation

To add a third store provider:

1. **Create Service:**
   ```csharp
   public class MyCustomApiService : IManifestApiService
   {
       // Implement all interface methods
   }
   ```

2. **Update Enum:**
   ```csharp
   // Models/AppSettings.cs
   public enum StoreProvider { Basement, Morrenus, MyCustom }
   ```

3. **Register Service:**
   ```csharp
   // App.xaml.cs
   services.AddSingleton<MyCustomApiService>();
   ```

4. **Update Factory:**
   ```csharp
   // Services/StoreApiFactory.cs
   private IManifestApiService GetActiveService()
   {
       var settings = _settingsService.LoadSettings();
       return settings.SelectedStore switch
       {
           StoreProvider.Basement => _basementService,
           StoreProvider.MyCustom => _myCustomService,
           _ => _morrenusService
       };
   }
   ```

5. **Update Settings UI:**
   ```xml
   <!-- Views/SettingsPage.xaml -->
   <ComboBoxItem Content="My Custom Store" Tag="MyCustom"/>
   ```

---

## Naming Conventions

**Preserved from Original Project:**
- Namespace: `SolusManifestApp` (unchanged for code compatibility)
- Assembly: MorrenusApp (unchanged)
- Protocol: `basement://` (unchanged)

**User-Facing Branding:**
- Application Title: "Basement Manager Pro"
- Window Title: "Basement Manager Pro"
- About/Support: "Basement Manager Pro"
- AppData folder: `BasementManagerPro` (changed)
- Downloads folder: `Documents/BasementManagerPro/Downloads` (changed)
- User-Agent: `BasementManagerPro/1.0` (changed)

**Rationale:** Keeping technical identifiers (namespaces, assembly names) unchanged allows easier merging of updates from the original Solus-Manifest-App repository while providing a distinct user-facing brand and separate data storage.

---

## Future Enhancement Ideas

1. **Multi-Account Support:** Support multiple Steam accounts simultaneously
2. **Scheduled Backups:** Auto-backup on schedule (daily/weekly)
3. **Cloud Backup:** Upload backups to cloud storage (OneDrive, Google Drive)
4. **Backup Comparison:** Compare two backups to see changes
5. **Game Statistics:** Track most played games, total playtime
6. **Backup Encryption:** Encrypt backup ZIP files with password
7. **Custom Stores:** Allow users to add custom store URLs easily
8. **Plugin System:** Load third-party extensions
9. **Multi-Language:** Internationalization support

---

## Version History & Changelog

**v1.0 - Basement Manager Pro**
- ✅ Dual store support (Basement + Morrenus)
- ✅ Steam data backup & restore system
- ✅ Rebranded to Basement Manager Pro
- ✅ Comprehensive API documentation
- ✅ Achievement reset functionality
- ✅ Enhanced settings with store provider selection

**Original (Solus-Manifest-App)**
- Morrenus store integration
- Manifest download & install
- Steam tools integration
- Theme system
- Auto-update system

---

## Contributing & Maintenance

### Code Style
- Use C# 10+ features (file-scoped namespaces, nullable reference types)
- Follow MVVM pattern strictly
- Use dependency injection for all services
- Prefer async/await for I/O operations
- Use CommunityToolkit.Mvvm attributes for MVVM boilerplate

### Testing Checklist
- [ ] All navigation buttons work
- [ ] Settings save/load correctly
- [ ] Store provider switching works
- [ ] API key validation works
- [ ] Downloads complete successfully
- [ ] Backups create valid ZIP files
- [ ] Restores work correctly
- [ ] Theme switching works
- [ ] Notifications display properly
- [ ] Logs are created

### Pre-Commit Checklist
- [ ] No hardcoded paths
- [ ] No API keys in code
- [ ] All exceptions logged
- [ ] User-facing strings are clear
- [ ] Settings properly saved
- [ ] No memory leaks (dispose resources)
- [ ] XAML bindings correct
- [ ] DI registrations complete

---

## Support & Resources

**Documentation:**
- This file (`claude.md`)
- API Documentation (`STORE_API_DOCUMENTATION.md`)
- Original README (`README.md`)

**External Links:**
- Morrenus Store: https://manifest.morrenus.xyz/
- .NET 8 Docs: https://docs.microsoft.com/dotnet
- WPF Documentation: https://docs.microsoft.com/wpf
- CommunityToolkit.Mvvm: https://docs.microsoft.com/windows/communitytoolkit/mvvm

---

**Last Updated:** 2024-01-20
**Maintained By:** Development Team
**Application Version:** 1.0 (Basement Manager Pro)
