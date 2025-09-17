# Repository Guidelines

## Project Structure & Module Organization
LinguaLink Client is a WPF MVVM application targeting `net8.0-windows`. `App.xaml` and `MainWindow.xaml` bootstrap the shell, while `Views/` holds page XAML and `ViewModels/` hosts CommunityToolkit.Mvvm-based logic. Domain models sit in `Models/`, shared helpers in `Services/` and `Converters/`, and reusable assets in `Assets/`. Architecture notes and feature rationales live in `docs/`.

## Build, Test, and Development Commands
Run all commands from the repository root:
```bash
dotnet restore
dotnet build lingualink_client.csproj -c Release
dotnet run --project lingualink_client.csproj -c Debug
dotnet format
```
`restore` pulls NuGet packages, `build` verifies the WPF project, `run` launches a debug session, and `dotnet format` keeps C# styling consistent.

## Coding Style & Naming Conventions
Use four-space indentation and the standard .NET naming scheme (PascalCase for classes/properties, camelCase for fields, leading `_` only for private readonly backing fields). Prefer `var` for obvious types and nullable-aware APIs. View models should raise observable properties via CommunityToolkit `[ObservableProperty]` to keep bindings consistent. Group localized strings in the existing `Properties/Lang.*.resx` files; add matching designer entries via Visual Studio tooling.

## Testing Guidelines
Automated tests are not yet in place; when adding them prefer `dotnet test`-compatible projects (xUnit or NUnit) and mirror the production namespace. Name scenario tests `<Feature>_<Condition>_<Expectation>`. Until a test suite exists, document manual verification steps in the PR and validate speech capture, translation flow, VRChat OSC publishing, and localization toggles.

## Commit & Pull Request Guidelines
Follow the conventional commit prefixes already in history (`feat:`, `chore:`, `refactor:`, etc.) and keep subject lines under 72 characters. One logical change per commit. PRs should include: concise summary, linked issue or task ID, screenshots or clips for UI updates, and steps to reproduce or test results. Rebase onto `main` before opening and confirm `dotnet build` succeeds locally, especially after touching view models or XAML bindings.

## Configuration & Security Notes
Store LinguaLink Server endpoints and API keys in user settings; never hard-code secrets or commit sample keys. When updating network code, ensure default URLs remain local (`http://localhost:8080/api/v1/`) and guard OSC ports behind user-configurable settings to avoid collisions.
