# Repository Guidelines

## Project Structure & Module Organization
LinguaLink Client is a WPF MVVM application targeting `net8.0-windows`.

- Shell & entry
  - `App.xaml` / `App.xaml.cs`: application bootstrap, resource dictionaries, global converters.
  - `MainWindow.xaml` / `MainWindow.xaml.cs`: root window hosting the main navigation frame.
- Presentation layer
  - `Views/Pages/`: XAML pages for each feature area (Index, Settings, Service, Account, TextEntry, MessageTemplate, etc.).
  - `Views/Components/`: reusable visual components (e.g., conversation history, log panel, main control, microphone selector).
  - `Converters/`: value converters used directly from XAML.
- Presentation logic (ViewModels)
  - `ViewModels/Pages/`: page-level view models that drive `Views/Pages` and coordinate feature-specific state.
  - `ViewModels/Components/`: component-level view models backing `Views/Components`.
  - `ViewModels/Managers/`: UI-facing managers that implement `INotifyPropertyChanged` and encapsulate selection/state workflows (e.g., microphone & target-language managers) used by view models.
  - `ViewModels/SharedStateViewModel` and `ViewModels/ViewModelBase`: shared observable state and the common MVVM base type.
- Domain models
  - `Models/`: core domain types such as `AppSettings`, API/translation models, logging and conversation history types.
  - `Models/Updates/`: models related to update discovery and sessions.
- Application services
  - `Services/Managers/`: high-level orchestrators and managers (audio/text translation pipelines, conversation history, logging).
  - `Services/Events/`: the event aggregator implementation and cross-layer event contracts used by services and view models.
  - `Services/Interfaces/`: service interfaces used for DI/service location.
  - `Services/Logging/`: adapters that bridge to external logging infrastructure (e.g., Velopack).
- Infrastructure & integration
  - `Services/Audio/`: low-level audio capture/encoding helpers and microphone access.
  - `Services/Api/`: LinguaLink API client, factory and response processing.
  - `Services/Localization/`: language-related helpers and display-name mapping for backend language codes.
  - `Services/Infrastructure/`: service container/initializer and settings persistence.
  - `Services/Updates/`: update service implementations and Velopack integration.
  - `Services/Ui/`: non-XAML UI helpers such as the modern message box wrapper.
  - `Assets/`: icons and other static assets.
- Documentation
  - `docs/`: project-level docs (`Summary.md`, `ReleaseGuide.md`, and `docs/README.md` as the index).
  - `docs/reference/`: cross-project or external-system docs (e.g., Lingualink Core API, web download link guide).

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

## Clipboard Usage Notes
- Clipboard writes should go through the Win32-based helper in `Services/Ui/ClipboardHelper.cs` (e.g., `ClipboardHelper.TrySetText(...)`) instead of direct `System.Windows.Clipboard` / `System.Windows.Forms.Clipboard` calls.
- When adding new copy-to-clipboard features, prefer the existing async helper patterns in `ViewModels/Components/LogViewModel.cs` and `ViewModels/Components/ConversationHistoryViewModel.cs` to avoid `ExternalException` when third-party tools temporarily lock the clipboard (e.g., remote desktop or clipboard sync apps).
