# ReachIT

ReachIT is a productivity-enhancing desktop application that provides workspace management, task tracking, local database storage, and quick access to tools via side panels and global hotkeys. 

## Features
- Global hotkey support (Ctrl+Alt+R) with fallback keybindings inside the app.
- Focus mode, statistics tracking, and overlay panels.
- File system project explorer.
- Offline-first architecture (no cloud dependency required).

## Architecture Summary
- **UI Framework:** WPF (.NET 8.0)
- **Design Pattern:** MVVM (Model-View-ViewModel)
- **Dependency Injection:** Custom minimal `AppHost` DI container.
- **Persistence:** Local database service built without blocking operations on startup.
- **Logging:** Basic production-safe text logger (`ILocalLogger`) writing to `LocalAppData/ReachIT/logs`.
- **Global Hotkeys:** Uses Windows API `RegisterHotKey`.

## Local Development Steps
1. **Requirements:** Visual Studio 2022 or .NET 8 CLI.
2. Clone the repository and CD into the root folder.
3. Open `ReachIT.sln` if present or right-click the workspace in VS to build.
4. Hit `F5` to build and run the application. 
5. Local logs are available at `%LOCALAPPDATA%\ReachIT\logs`. App settings are stored locally.

## Known Assumptions
- App explicitly manages synchronization contexts internally. 
- Windows-only app given its WPF requirement and reliance on `user32.dll` APIs for global hotkey hooks and tray icons.
- Hotkey conflicts (e.g. if Ctrl+Alt+R is taken) are handed by warning the user and reverting to a safe in-app command binding.
