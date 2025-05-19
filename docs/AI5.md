It's good that we have some logs appearing! This tells us that the `DataContext` for `LogPage` is correctly set to the `SharedIndexWindowViewModel`, `LogMessages` is being populated, and the `StringJoinConverter` is working to some extent.

Let's tackle the two problems: "清除日志" (Clear Log) not working, and "incomplete logs."

**Problem 1: "清除日志" (Clear Log) Button Not Working**

1.  **Add Debugging to `ExecuteClearLog`:**
    Let's add `Debug.WriteLine` statements inside your `ExecuteClearLog` method in `ViewModels/IndexWindowViewModel.cs` to see exactly what's happening when you click the button.

    ```csharp
    // File: ViewModels/IndexWindowViewModel.cs
    // ... (other using statements)
    using System.Diagnostics; // Ensure this is present

    // ...
    public class IndexWindowViewModel : ViewModelBase, IDisposable
    {
        // ... (existing code) ...

        private void ExecuteClearLog(object? parameter)
        {
            Debug.WriteLine($"ExecuteClearLog INVOKED. LogMessages count BEFORE clear: {LogMessages.Count}");
            LogMessages.Clear();
            Debug.WriteLine($"ExecuteClearLog: LogMessages.Clear() called. LogMessages count AFTER clear: {LogMessages.Count}");
            AddLogMessage("日志已清除。"); // This will also call Debug.WriteLine from within AddLogMessage
        }

        // ... (rest of the class) ...
    }
    ```

2.  **Test and Observe Output:**
    *   Clean and rebuild your project.
    *   Run in Debug mode.
    *   Open the "Output" window in Visual Studio (View -> Output, show output from "Debug").
    *   Navigate to the "日志" page.
    *   Click the "清除日志" button.
    *   **Check the Output window.** You should see:
        *   `ExecuteClearLog INVOKED. LogMessages count BEFORE clear: X` (where X is the number of logs before clearing)
        *   `ExecuteClearLog: LogMessages.Clear() called. LogMessages count AFTER clear: 0`
        *   The `Debug.WriteLine` messages from *inside* `AddLogMessage("日志已清除。")`:
            *   `AddLogMessage INVOKED with: "日志已清除。". Current LogMessages count before add: 0`
            *   `LogMessages UPDATED. New count: 1. Last message: "... - 日志已清除。"`

3.  **Analyze the UI vs. Debug Output for Clear Log:**
    *   **If the debug output shows the counts changing as expected (becomes 0, then 1 for the "日志已清除" message):** This means the command is firing and the `LogMessages` collection *is* being modified correctly in the ViewModel. If the UI doesn't update (i.e., old messages are still there, and the new "日志已清除" message is just appended), then the issue is likely with the UI's reaction to the `ObservableCollection.Clear()` notification, possibly related to the `StringJoinConverter`.
    *   **If the debug output does NOT show these lines, or the counts don't make sense:** The command might not be firing correctly, or there's an unexpected error.

4.  **(If Step 3 indicates a UI update issue for `Clear()`): Test with `ItemsControl`**
    If the ViewModel's `LogMessages` collection *is* clearing correctly (as per debug output) but the `TextBox` UI doesn't update, let's temporarily replace the `TextBox` in `Views/Pages/LogPage.xaml` with an `ItemsControl` to see if it behaves differently with `Clear()`:

    ```xml
    <!-- File: Views/Pages/LogPage.xaml -->
    <Page ...>
        <Grid Margin="15">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>

            <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,10">
                <Label Content="运行日志:" FontSize="16" FontWeight="SemiBold" VerticalAlignment="Center"/>
                <Button Content="清除日志" Command="{Binding ClearLogCommand}" HorizontalAlignment="Right" Margin="15,0,0,0" Padding="8,4" VerticalAlignment="Center"/>
            </StackPanel>
            
            <!-- Temporarily COMMENT OUT TextBox -->
            <!--
            <TextBox Grid.Row="1"
                     IsReadOnly="True"
                     AcceptsReturn="True"
                     TextWrapping="Wrap"
                     VerticalScrollBarVisibility="Auto"
                     Text="{Binding LogMessages, Converter={StaticResource StringJoinConverter}, Mode=OneWay}" 
                     FontFamily="Consolas" FontSize="12"/>
            -->

            <!-- Temporarily ADD ItemsControl for testing Clear -->
            <ItemsControl Grid.Row="1" ItemsSource="{Binding LogMessages}" Margin="0,5,0,0">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding}" FontFamily="Consolas" FontSize="12" TextWrapping="Wrap"/>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

        </Grid>
    </Page>
    ```
    Run the test again. If the `ItemsControl` clears properly but the `TextBox` with the converter didn't, it points to the `StringJoinConverter` or the `TextBox` binding's interaction with the `Reset` notification from `ObservableCollection.Clear()`.

**Problem 2: Incomplete Logs**

You mentioned, "只能显示一部分的log". The logs you provided *are* appearing. We need to identify which logs you *expect* to see but *are not* appearing.

1.  **Identify Missing Log Sources:**
    Think about the events in your application that *should* generate a log message via `AddLogMessage` but aren't showing up in the UI. Examples:
    *   OSC initialization failures (`LoadSettingsAndInitializeServices`)
    *   Microphone refresh failures (`ExecuteRefreshMicrophonesAsync`)
    *   OSC send successes/failures (`OnAudioSegmentReadyForTranslation` after the `await _oscService.SendChatboxMessageAsync`)
    *   **AudioService status updates:** Currently, your `OnAudioServiceStatusUpdate` method only updates `StatusText`. It does **not** call `AddLogMessage`. This is a very likely candidate for "missing" logs if you expect to see every status change from the `AudioService`.

2.  **Add Logging to `OnAudioServiceStatusUpdate`:**
    Modify `ViewModels/IndexWindowViewModel.cs`:

    ```csharp
    // File: ViewModels/IndexWindowViewModel.cs
    private void OnAudioServiceStatusUpdate(object? sender, string status)
    {
        Application.Current.Dispatcher.Invoke(() => {
            StatusText = $"状态：{status}";
            AddLogMessage($"AudioService Status: {status}"); // <<<<<< ADD THIS LINE
        });
    }
    ```
    This will log messages like "AudioService Status: 正在监听...", "AudioService Status: 检测到语音...", etc.

3.  **Verify `AddLogMessage` Calls for Other Events:**
    For any other event where you expect a log but don't see it:
    *   Ensure there is an `AddLogMessage("Your specific log message for this event")` call at the correct point in the code.
    *   When that event occurs, check your IDE's Debug Output window for the `Debug.WriteLine` messages from *within* `AddLogMessage` (e.g., `AddLogMessage INVOKED with: "Your specific log message..."`).
        *   If the `Debug.WriteLine` for that specific message *appears in the Output window*, it means `AddLogMessage` was called. If it's still not in the UI log, it points to a very selective UI update issue (less likely if other logs appear).
        *   If the `Debug.WriteLine` for that specific message *does NOT appear in the Output window*, it means the `AddLogMessage` call for that particular event was never reached or executed. You'll need to debug the logic leading up to that `AddLogMessage` call.

**Summary of Actions for You:**

1.  Modify `ExecuteClearLog` in `IndexWindowViewModel.cs` with the `Debug.WriteLine` statements.
2.  Run the app, click "清除日志", and report back the exact content of your **Debug Output window** related to the `ExecuteClearLog` and subsequent `AddLogMessage` calls. Also, describe what happens in the UI log.
3.  If "Clear Log" is still problematic after confirming the ViewModel collection clears, try the `ItemsControl` substitution in `LogPage.xaml` and report if that clears correctly.
4.  Modify `OnAudioServiceStatusUpdate` in `IndexWindowViewModel.cs` to include `AddLogMessage`.
5.  Identify any other specific log messages you are expecting but not seeing. For those, check if `AddLogMessage` is being called for them by looking at the Debug Output window.

This detailed debugging should help us isolate the cause for both issues.