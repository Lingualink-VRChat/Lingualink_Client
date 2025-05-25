由于这个是个.net应用，所以更改完成后我会让Visual Studio 编译后执行，然后我再给你执行结果

---

### LinguaLink Client Project Structural Summary

This is a WPF client application (`.NET 8.0-windows`) built using the MVVM (Model-View-ViewModel) architectural pattern, enhanced with `WPF-UI` for modern UI components and `CommunityToolkit.Mvvm` for MVVM capabilities (ObservableObject, RelayCommand). Its primary purpose is to capture microphone audio, send it to a translation server, display the translation, and optionally send it to VRChat via OSC.

**1. Core Architecture Pattern:**
*   **MVVM:** Strict separation of concerns.
    *   **Views (`Views/`):** XAML files defining the UI. They bind to properties and commands exposed by ViewModels.
    *   **ViewModels (`ViewModels/`):** C# classes that expose data and commands for the Views. They encapsulate presentation logic and orchestrate interactions with Services. They inherit from `ViewModelBase` (which extends `ObservableObject` from `CommunityToolkit.Mvvm`).
    *   **Models (`Models/`):** C# classes defining data structures and application configuration (`AppSettings`, `ServerResponse`, `MMDeviceWrapper`). They are plain data objects with no UI or business logic dependencies.

**2. Key Functional Modules (Services & their ViewModels):**

*   **`App.xaml.cs`:** The application entry point. It initializes `SharedIndexWindowViewModel` as a singleton to maintain global state across different pages (e.g., `IndexPage` and `LogPage`). It also sets the initial UI culture.

*   **Main Window & Navigation:**
    *   **`MainWindow.xaml` / `MainWindow.xaml.cs`:** The main shell of the application, using `WPF-UI`'s `NavigationView` for primary navigation.
    *   **`MainWindowViewModel.cs`:** Manages the main window's title and navigation menu item labels, providing localization for these elements.

*   **Core Translation Logic (Orchestrated by `IndexWindowViewModel`):**
    *   **`IndexPage.xaml` / `IndexPage.xaml.cs`:** The primary operational UI, displaying microphone selection, start/stop button, target language configuration, real-time status, and translation results.
    *   **`IndexWindowViewModel.cs`:** **The central orchestrator of the application's core functions.**
        *   Interacts with `MicrophoneManager` to list and select input devices.
        *   Initializes and controls `AudioService` (Start/Stop recording).
        *   Receives audio segments from `AudioService` via events.
        *   Sends audio segments to `TranslationService` for processing.
        *   Receives translation results and updates `TranslationResultText`.
        *   Optionally sends translated text to VRChat via `OscService`.
        *   Manages a real-time `LogMessages` collection for display on the `LogPage`.
        *   Handles dynamic target language selection using `SelectableTargetLanguageViewModel`.

*   **Microphone Management:**
    *   **`MicrophoneManager.cs`:** Provides functionality to enumerate available audio input devices using NAudio.

*   **Audio Capture & Voice Activity Detection (VAD):**
    *   **`AudioService.cs`:** Records audio from the selected microphone (`NAudio`), performs Voice Activity Detection (VAD) using `WebRtcVadSharp` to detect speech and silence. It segments audio based on VAD parameters (silence threshold, min/max voice duration) and raises `AudioSegmentReady` events when a segment is ready for translation. It also reports internal status updates.

*   **Translation Service Communication:**
    *   **`TranslationService.cs`:** Handles sending recorded audio segments (WAV files) to the configured HTTP translation server and deserializes the JSON responses (`ServerResponse`). It includes error handling for network and server-side issues.

*   **OSC Communication (for VRChat):**
    *   **`OscService.cs`:** Sends translated text messages to a configured OSC IP and Port (typically VRChat's chatbox input) using the `OscCore` library.

*   **Settings Management:**
    *   **`ServicePage.xaml` / `ServicePage.xaml.cs`:** UI for configuring server URL, VAD parameters, and OSC settings.
    *   **`ServicePageViewModel.cs`:** Binds directly to `AppSettings` properties. Responsible for validating user input, saving changes via `SettingsService`, and reverting to last saved settings.
    *   **`SettingsService.cs`:** Manages loading and saving `AppSettings` from/to a JSON file (`app_settings.json`) in the user's application data folder.
    *   **`SettingsChangedNotifier.cs`:** A static event handler to notify other parts of the application (e.g., `IndexWindowViewModel`) when `AppSettings` are saved, triggering re-initialization of services with new parameters.

*   **Localization:**
    *   **`SettingPage.xaml` / `SettingPage.xaml.cs`:** UI for selecting the interface language.
    *   **`SettingPageViewModel.cs`:** Binds to the `InterfaceLanguage` label.
    *   **`LanguageManager.cs`:** Provides static methods to change the application's `CurrentUICulture` and retrieve localized strings from `Properties.Lang.resx` and `Properties.Lang.zh-CN.resx`. It also broadcasts `LanguageChanged` events to update UI elements.
    *   **`Properties/Lang.resx` & `Properties/Lang.zh-CN.resx`:** Resource files containing localized strings for English and Simplified Chinese, respectively.

*   **Logging:**
    *   **`LogPage.xaml` / `LogPage.xaml.cs`:** Dedicated UI page to display real-time application logs.
    *   `LogMessages` property in `IndexWindowViewModel` acts as the data source for this page, showcasing the shared ViewModel pattern.

**3. Utility & Infrastructure:**
*   **`BooleanToVisibilityConverter.cs`:** A standard WPF `IValueConverter` to convert boolean values to `Visibility` enum (Visible/Collapsed), commonly used for conditional UI element visibility.
*   **`ViewModelBase.cs`:** Abstract base class for all ViewModels, inheriting `ObservableObject` to provide `INotifyPropertyChanged` implementation using source generators from `CommunityToolkit.Mvvm`.
*   **`lingualink_client.csproj`:** Project file managing dependencies (NAudio, OscCore, WebRtcVadSharp, WPF-UI, CommunityToolkit.Mvvm) and build configurations.

**4. Data Flow Overview:**
1.  **Startup:** `App.xaml.cs` loads settings, initializes `IndexWindowViewModel` and sets the language.
2.  **Mic Selection:** User selects microphone on `IndexPage` (updates `IndexWindowViewModel`).
3.  **Start Work:** User clicks "Start Work" (`ToggleWorkCommand` in `IndexWindowViewModel`).
4.  **Audio Capture:** `IndexWindowViewModel` tells `AudioService` to start recording.
5.  **VAD & Segmenting:** `AudioService` continuously captures audio, processes it through VAD, and segments it.
6.  **Audio Segment Ready:** `AudioService` raises `AudioSegmentReady` event with raw audio data.
7.  **Translation Request:** `IndexWindowViewModel` receives the segment, packages it, and sends it to the configured server via `TranslationService`.
8.  **Server Response:** `TranslationService` receives translated text from the server.
9.  **Display & OSC Send:** `IndexWindowViewModel` updates `TranslationResultText`, logs the event, and if enabled, sends the translated text to VRChat via `OscService`.
10. **Settings Change:** User modifies settings on `ServicePage` or `SettingPage`. `ServicePageViewModel` or `SettingPageViewModel` saves these via `SettingsService`. `SettingsChangedNotifier` broadcasts the change, prompting `IndexWindowViewModel` to re-initialize its services with updated parameters.
