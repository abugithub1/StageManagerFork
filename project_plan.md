# Project Plan: Stage Manager Multi-Monitor Support

## Phase 1: Multi-Monitor Core Functionality

### Project Scope

The goal is to add multi-monitor support to the existing Stage Manager for Windows application. This involves enabling the application to detect and manage windows across multiple displays, similar to how the native macOS Stage Manager feature operates on a per-monitor basis.

The initial implementation focuses on ensuring windows are correctly associated with their respective monitors and that scene switching properly restores windows to the correct display. Further enhancements regarding UI placement and cross-monitor interactions can be considered later phases.

### Changes Made So Far (as of 2025-05-01)

1.  **Enabled WinForms Integration:** Added `<UseWindowsForms>true</UseWindowsForms>` to `StageManager.csproj` to allow usage of `System.Windows.Forms.Screen`.
2.  **Monitor Detection:**
    *   Added `GetAllMonitorInfo()` method to `Native/VisualHelper.cs` using `System.Windows.Forms.Screen.AllScreens` to retrieve information about all connected displays (Bounds, WorkingArea, IsPrimary).
    *   Added `ScreenInfo` class in `Native/VisualHelper.cs` to store information for each monitor.
3.  **Main Window Positioning:**
    *   Modified `MainWindow.xaml.cs` to call `GetAllMonitorInfo()` on startup.
    *   Updated `MainWindow.xaml.cs` (`PositionWindowOnPrimaryMonitor` method) to position the main Stage Manager UI on the primary monitor's working area instead of assuming `(0,0)`.
4.  **Window Monitor Tracking:**
    *   Added `Monitor` property of type `ScreenInfo` to the `Native/Window/IWindow.cs` interface.
    *   Added `Monitor` property implementation to `Native/WindowsWindow.cs`.
    *   Modified `Native/WindowsWindow.cs` constructor to accept `List<ScreenInfo>` and added `GetMonitorForWindow` helper method to determine and store the monitor a window belongs to (based on intersection or proximity).
    *   Modified `Native/WindowsManager.cs` constructor to accept `List<ScreenInfo>` and pass it down to the `WindowsWindow` constructor when registering windows.
    *   Updated `MainWindow.xaml.cs` to pass the `_allScreens` list to the `WindowsManager` constructor.
    *   Updated `MainWindow.xaml.cs` (`FindSceneByPoint` method) to pass `_allScreens` when creating a temporary `WindowsWindow` instance.
5.  **Build Error Fixes (Initial):**
    *   Removed explicit `<Reference Include="System.Windows.Forms" />` from `.csproj`.
    *   Fixed `System.Windows.Rect` vs `System.Drawing.Rectangle` usage in `WindowsWindow.cs` (`CalculateIntersectionArea` method).
    *   Added `using System.Windows;` to `WindowsWindow.cs` to resolve `Rect` type errors.
    *   Added `using ControlzEx.Standard;` to `WindowsWindow.cs` to resolve `NativeMethods` and `MonitorOptions` accessibility errors from the `ControlzEx` library.
    *   Fixed `Win32.GetWindowRect` call in `WindowsWindow.cs` to use `ref` instead of `out`.
    *   Fixed `Win32.Rect` width/height calculation in `WindowsWindow.cs`.
    *   Resolved `WindowState` ambiguity in `WindowsWindow.cs` by using aliases (`InternalWindowState`).
    *   Restructured `GetMonitorForWindow` in `WindowsWindow.cs` to call `GetWindowRect` earlier, anticipating potential "use of unassigned local variable" error for `windowRect`.
6.  **Build Error Fixes (CS0165 - `windowRect`):**
    *   Attempted `dotnet publish` which failed with `CS0165: Use of unassigned local variable 'windowRect'` despite prior restructuring.
    *   Verified code changes, cleaned build artifacts, and re-attempted build; error persisted.
    *   Isolated the issue by temporarily simplifying `GetMonitorForWindow`, which resulted in a successful build.
    *   Restored original `GetMonitorForWindow` logic but added explicit initialization (`windowRect = default;`) which resolved the `CS0165` error.
    *   Confirmed successful `dotnet publish` with the final changes.

### Next Steps / TODO (Phase 1)

1.  **Confirm Manual Edits & Build:** (DONE - Build successful after fixing CS0165 in `GetMonitorForWindow`)
2.  **Test Core Functionality:** (DONE - Basic multi-monitor restore confirmed working)
3.  **Address Nullability Warnings (Optional but Recommended):** Fix the numerous `CS86xx` warnings identified during the build process to improve code robustness. This involves checking for potential nulls and adjusting type declarations (e.g., using `?` for nullable reference types) or adding null checks/initializers.
4.  **Enhance UI/UX (Future Work):**
    *   Allow the main Stage Manager UI to be placed on monitors other than the primary one.
    *   Consider if/how to implement per-monitor Stage Manager instances (more complex).
    *   Refine drag-and-drop behavior for moving windows between scenes across different monitors.
5.  **Refine Monitor Handling (Future Work):**
    *   Improve fallback logic in `WindowsWindow.GetMonitorForWindow` (e.g., better mapping from `HMONITOR` if needed).
    *   Add handling for dynamic display changes (connecting/disconnecting monitors while the application is running) using system events like `SystemEvents.DisplaySettingsChanged`.
    *   Define behavior for windows that span multiple monitors.

## Phase 2: Cross-App Grouping

### Project Scope

Implement the ability to group windows from different applications together into a single "scene" or "group", similar to macOS Stage Manager.

### Features / Requirements

1.  **Multi-App Selection:** Allow selecting multiple different application windows/scenes from the sidebar simultaneously (e.g., using Ctrl+Click or Shift+Click).
2.  **Simultaneous Display:** When a multi-app group is selected/active, all windows belonging to that group should be shown concurrently.
3.  **Group Persistence:** The created multi-app groups should be remembered when minimized (i.e., another group/scene is activated) and persist in the sidebar.
4.  **Sidebar Representation:** Visually represent multi-app groups in the sidebar. This might involve clustering the icons of the grouped applications together.
5.  **Activation Behavior:** Clicking a multi-app group in the sidebar should hide/minimize the currently active windows and restore/show all windows belonging to the selected group.
6.  **Group Modification (Removal):** Provide a mechanism to remove a specific window from a multi-app group (e.g., Ctrl+Clicking the minimize button on the window itself, TBC).

### Next Steps / TODO (Phase 2)

1.  **Data Structure Design:** Determine how to represent multi-app groups within the existing `SceneManager` / `Scene` structure or design a new structure.
2.  **UI Interaction:** Implement Ctrl/Shift+Click logic in `MainWindow.xaml.cs` for sidebar item selection.
3.  **Group Visualization:** Update `MainWindow.xaml` and related view models to visually represent these groups in the sidebar.
4.  **Window Management Logic:** Modify the window hide/show logic (`SceneManager`, `MainWindow`, potentially `WindowsManager`) to handle activating/deactivating multi-app groups instead of just single scenes.
5.  **Group Removal Logic:** Implement the defined mechanism for removing a window from a group. 