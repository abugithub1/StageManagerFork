using AsyncAwaitBestPractices;
using StageManager.Native;
using StageManager.Native.PInvoke;
using StageManager.Native.Window;
using StageManager.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace StageManager
{
	public class SceneManager
	{
		private readonly Desktop _desktop;
		private List<Scene> _scenes;
		private Scene _current;
		private bool _suspend = false;
		private Guid? _reentrancyLockSceneId;

		public event EventHandler<SceneChangedEventArgs> SceneChanged;
		public event EventHandler<CurrentSceneSelectionChangedEventArgs> CurrentSceneSelectionChanged;

		private IWindowStrategy WindowStrategy { get; } = new NormalizeAndMinimizeWindowStrategy(); // new WindowNormalizeStrategy/OpacityWindowStrategy/ShowAndHideWindowStrategy

		public WindowsManager WindowsManager { get; }

		public SceneManager(WindowsManager windowsManager)
		{
			WindowsManager = windowsManager ?? throw new ArgumentNullException(nameof(windowsManager));
			_desktop = new Desktop();
			_desktop.HideIcons();
		}

		public async Task Start()
		{
			if (Thread.CurrentThread.ManagedThreadId != 1)
				throw new NotSupportedException("Start has to be called on the main thread, otherwise events won't be fired.");

			WindowsManager.WindowCreated += WindowsManager_WindowCreated;
			WindowsManager.WindowUpdated += WindowsManager_WindowUpdated;
			WindowsManager.WindowDestroyed += WindowsManager_WindowDestroyed;
			WindowsManager.UntrackedFocus += WindowsManager_UntrackedFocus;

			await WindowsManager.Start();
		}

		internal void Stop()
		{
			WindowsManager.Stop();

			foreach (var scene in _scenes)
			{
				foreach (var w in scene.Windows)
					WindowStrategy.Show(w);
			}

			_desktop.ShowIcons();
		}

		private void WindowsManager_WindowUpdated(IWindow window, WindowUpdateType type)
		{
			if (_suspend)
				return;

			if (type == WindowUpdateType.Foreground)
			{
				SwitchToSceneByWindow(window).SafeFireAndForget();
			}
			else if (type == WindowUpdateType.MinimizeStart)
			{
				bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
				if (isCtrlPressed)
				{
					HandleUngroupMinimize(window);
				}
				// else: Normal minimize, do nothing specific here for now
			}
		}

		private void WindowsManager_UntrackedFocus(object? sender, IntPtr e)
		{
			if (_suspend)
				return;

			if (!_desktop.HasDesktopView)
				_desktop.TrySetDesktopView(e);

			if (_desktop.HasDesktopView && _desktop.DesktopViewHandle == e)
				SwitchTo(null).SafeFireAndForget();
		}

		private void WindowsManager_WindowDestroyed(IWindow window)
		{
			var scene = FindSceneForWindow(window);

			if (scene is not null)
			{
				scene.Remove(window);

				if (scene.Windows.Any())
				{
					SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Updated));
				}
				else
				{
					_scenes.Remove(scene);
					SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Removed));
				}
			}
		}

		public Scene FindSceneForWindow(IWindow window) => FindSceneForWindow(window.Handle);

		public Scene FindSceneForWindow(IntPtr handle) => _scenes?.FirstOrDefault(s => s.Windows.Any(w => w.Handle == handle));

		private Scene FindSceneForProcess(string processName) => _scenes.FirstOrDefault(s => string.Equals(s.Key, processName, StringComparison.OrdinalIgnoreCase));

		private async void WindowsManager_WindowCreated(IWindow window, bool firstCreate)
		{
			SwitchToSceneByNewWindow(window).SafeFireAndForget();
		}

		private async Task SwitchToSceneByWindow(IWindow window)
		{
			var scene = FindSceneForWindow(window);
			if (scene is null)
			{
				scene = new Scene(GetWindowGroupKey(window), window);
				_scenes.Add(scene);
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Created));
			}

			await SwitchTo(scene);
		}

		private async Task SwitchToSceneByNewWindow(IWindow window)
		{
			var existentScene = FindSceneForProcess(GetWindowGroupKey(window));
			var scene = existentScene ?? new Scene(window.ProcessName, window);

			if (existentScene is null)
			{
				_scenes.Add(scene);
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Created));
			}
			else
			{
				scene.Add(window);
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(scene, window, ChangeType.Updated));
			}

			await SwitchTo(scene).ConfigureAwait(true);
		}

		/// <summary>
		/// Determines if a scene is switched back to shortly after it has been hidden.
		/// This can happen if an app activates one of it's windows after being hidde,
		/// like Microsoft Teams does if there's a small floating window for a current call.
		/// </summary>
		/// <param name="scene"></param>
		/// <returns></returns>
		private bool IsReentrancy(Scene? scene)
		{
			if (scene is null)
				return false;

			if (Guid.Equals(scene.Id, _reentrancyLockSceneId))
				return true;

			if (_current is object)
			{
				_reentrancyLockSceneId = _current.Id;

				Task.Run(async () =>
				{
					await Task.Delay(1000).ConfigureAwait(false);
					_reentrancyLockSceneId = null;
				}).SafeFireAndForget();
			}

			return false;
		}

		public async Task ActivateGroup(List<Scene> scenesToGroup)
		{
			if (scenesToGroup == null || scenesToGroup.Count < 2) 
			{
				// Cannot group less than two scenes, activate the first one if available
				if (scenesToGroup?.Count == 1)
				{
					await SwitchTo(scenesToGroup.First());
				}
				return;
			}

			// Combine windows from all scenes
			var allWindowsInGroup = scenesToGroup.SelectMany(s => s.Windows).Distinct().ToArray();
			if (!allWindowsInGroup.Any()) return; // Cannot create an empty group

			// Create a unique key for the group (could be improved)
			string groupKey = $"group-{Guid.NewGuid()}"; 
			var newGroupScene = new Scene(groupKey, allWindowsInGroup);

			try
			{
				_suspend = true; // Prevent interference while modifying lists and switching

				// Add the new group scene
				_scenes.Add(newGroupScene);
				// Use the first window for the Created event's context (arbitrary choice)
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(newGroupScene, allWindowsInGroup.First(), ChangeType.Created)); 

				// Remove original scenes that were grouped
				foreach (var sceneToRemove in scenesToGroup)
				{
					if (_scenes.Remove(sceneToRemove))
					{
						// Use the first window (if any) for the Removed event's context
						var contextWindow = sceneToRemove.Windows.FirstOrDefault();
						SceneChanged?.Invoke(this, new SceneChangedEventArgs(sceneToRemove, contextWindow, ChangeType.Removed));
					}
				}
			}
			finally
			{
				_suspend = false;
			}

			// Switch to the new group scene
			await SwitchTo(newGroupScene);
		}

		public async Task SwitchTo(Scene? scene)
		{
			if (object.Equals(scene, _current))
				return;

			if (IsReentrancy(scene))
				return;

			try
			{
				_suspend = true;

				// Determine windows to hide and show
				var windowsToShow = scene?.Windows ?? Array.Empty<IWindow>();
				var allManageableWindows = GetSceneableWindows().ToArray(); // Get all windows once
				var windowsToHide = allManageableWindows.Except(windowsToShow).ToArray();

				var prior = _current;
				_current = scene;

				// Use DeferWindowPos for batch update
				int count = windowsToHide.Count() + windowsToShow.Count();
				if (count > 0)
				{
					using (var hdwp = WindowsManager.DeferWindowsPos(count))
					{
						// Queue hiding windows
						foreach (var window in windowsToHide)
						{
							WindowStrategy.DeferHide(window, hdwp); // Use deferred hide
						}

						// Queue showing windows
						foreach (var window in windowsToShow)
						{
							WindowStrategy.DeferShow(window, hdwp); // Use deferred show
						}
					}
					// EndDeferWindowPos is called automatically by using statement's Dispose
				}

				// Focus one of the windows in the new scene (after batch update)
				// This still happens after the windows are shown/hidden by EndDeferWindowPos
				if (scene != null)
				{
					var windowToFocus = scene.Windows.FirstOrDefault(w => !w.IsMinimized);
					if (windowToFocus != null)
					{
						windowToFocus.Focus();
					}
				}

				CurrentSceneSelectionChanged?.Invoke(this, new CurrentSceneSelectionChangedEventArgs(prior, scene));
			}
			finally
			{
				_suspend = false;
			}
		}

		public Task MoveWindow(Scene sourceScene, IWindow window, Scene targetScene)
		{
			try
			{
				_suspend = true;

				if (sourceScene is null || sourceScene.Equals(targetScene))
					return Task.CompletedTask;

				sourceScene.Remove(window);
				targetScene.Add(window);

				SceneChanged?.Invoke(this, new SceneChangedEventArgs(sourceScene, window, ChangeType.Updated));
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(targetScene, window, ChangeType.Updated));

				if (!sourceScene.Windows.Any())
				{
					_scenes.Remove(sourceScene);
					SceneChanged?.Invoke(this, new SceneChangedEventArgs(sourceScene, window, ChangeType.Removed));
				}

				if (targetScene.Equals(_current))
				{
					WindowStrategy.Show(window);
					window.Focus();
				}
				else
				{
					WindowStrategy.Hide(window);

					// reset window position after move so that the window is back at the starting position on the new scene
					if (window is WindowsWindow w && w.PopLastLocation() is IWindowLocation l)
						Win32.SetWindowPos(window.Handle, IntPtr.Zero, l.X, l.Y, 0, 0, Win32.SetWindowPosFlags.IgnoreResize);
				}

				return Task.CompletedTask;
			}
			finally
			{
				_suspend = false;
			}
		}

		public async Task MoveWindow(IntPtr handle, Scene targetScene)
		{
			var source = FindSceneForWindow(handle);

			if (source is null || source.Equals(targetScene))
				return;

			var window = source.Windows.First(w => w.Handle == handle);
			await MoveWindow(source, window, targetScene);
		}

		public async Task PopWindowFrom(Scene sourceScene)
		{
			if (sourceScene is null || _current is null || sourceScene.Equals(_current))
				return;

			var window = sourceScene.Windows.LastOrDefault();

			if (window is object)
				await MoveWindow(sourceScene, window, _current).ConfigureAwait(false);
		}

		private IEnumerable<IWindow> GetSceneableWindows() => WindowsManager?.Windows?.Where(w => !string.IsNullOrEmpty(w.ProcessFileName) && !string.IsNullOrEmpty(w.Title));

		public IEnumerable<Scene> GetScenes()
		{
			if (_scenes is null)
			{
				_scenes = GetSceneableWindows()
							.GroupBy(GetWindowGroupKey)
							.Select(group => new Scene(group.Key, group.ToArray()))
							.ToList();
			}

			return _scenes;
		}

		public IEnumerable<IWindow> GetCurrentWindows() => _current?.Windows ?? GetSceneableWindows();

		private string GetWindowGroupKey(IWindow window) => window.ProcessName;

		private async void HandleUngroupMinimize(IWindow window)
		{
			var currentScene = FindSceneForWindow(window);

			// Only ungroup if the window is in the currently active scene and that scene is a multi-app group
			if (currentScene != null && currentScene == _current && currentScene.IsMultiAppGroup)
			{
				Scene? sceneToSwitchTo = null; // Variable to hold scene to switch to after suspend=false
				Scene? originalScene = null; // To store the result of FindOrCreateOriginalScene
				try
				{
					_suspend = true;

					// Remove window from the group
					currentScene.Remove(window);
					SceneChanged?.Invoke(this, new SceneChangedEventArgs(currentScene, window, ChangeType.Updated));

					// Find or create the original scene for the window
					originalScene = FindOrCreateOriginalScene(window);
					// Add window back to its original scene (event raised by FindOrCreateOriginalScene if needed)
					if (!originalScene.Windows.Contains(window))
					{
						originalScene.Add(window);
						// Raise updated event for original scene if it already existed
						if (_scenes.Contains(originalScene)) // Check if it wasn't newly created
						{
							SceneChanged?.Invoke(this, new SceneChangedEventArgs(originalScene, window, ChangeType.Updated));
						}
					}

					// Check if the group scene is now empty
					if (!currentScene.Windows.Any())
					{
						if (_scenes.Remove(currentScene))
						{
							SceneChanged?.Invoke(this, new SceneChangedEventArgs(currentScene, null, ChangeType.Removed));
							sceneToSwitchTo = originalScene; // Schedule switch to the window's new scene
						}
					}
				}
				finally
				{
					_suspend = false;
				}

				// Perform switch outside the suspend block if needed
				if (sceneToSwitchTo != null)
				{
					await SwitchTo(sceneToSwitchTo);
				}
			}
		}

		private Scene FindOrCreateOriginalScene(IWindow window)
		{
			string originalKey = GetWindowGroupKey(window);
			var existingScene = FindSceneForProcess(originalKey);

			if (existingScene != null)
			{
				return existingScene;
			}
			else
			{
				var newOriginalScene = new Scene(originalKey, window); // Create with only this window initially
				_scenes.Add(newOriginalScene);
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(newOriginalScene, window, ChangeType.Created));
				return newOriginalScene;
			}
		}

		public async Task AddSceneToCurrentGroup(Scene sceneToAdd)
		{
			if (sceneToAdd == null || _current == null || sceneToAdd.Id == _current.Id)
			{
				// Cannot add null, nothing to add to, or adding the current scene to itself
				return; 
			}

			// Get windows to move (avoid modifying original list while iterating)
			var windowsToMove = sceneToAdd.Windows.ToList(); 
			if (!windowsToMove.Any()) 
			{
				// Scene to add is already empty, maybe remove it?
				if (_scenes.Remove(sceneToAdd))
				{
					// Use the first window (if any) for the Removed event's context - won't have one here
					SceneChanged?.Invoke(this, new SceneChangedEventArgs(sceneToAdd, null, ChangeType.Removed));
				}
				return;
			}

			try
			{
				_suspend = true; // Prevent interference

				// Move each window to the current scene
				foreach (var window in windowsToMove)
				{
					// Add window to the current scene's list
					// Note: Scene.Add handles duplicates if necessary
					_current.Add(window); 
				
					// Remove window from the source scene's list
					// (Scene.Remove doesn't exist, but the window is effectively moved by adding to _current)
					// We just need to ensure the source scene is eventually removed.
				}
			
				// Use the first moved window for the Updated event's context
				SceneChanged?.Invoke(this, new SceneChangedEventArgs(_current, windowsToMove.First(), ChangeType.Updated));

				// Remove the now empty source scene
				if (_scenes.Remove(sceneToAdd))
				{
					// Use the first moved window for the Removed event's context
					SceneChanged?.Invoke(this, new SceneChangedEventArgs(sceneToAdd, windowsToMove.First(), ChangeType.Removed));
				}

			}
			finally
			{
				_suspend = false;
			}

			// Ensure the current (now merged) scene is visually active
			// Re-apply the window strategy to ensure the newly added windows are shown correctly
			foreach (var window in windowsToMove)
			{
				WindowStrategy.Show(window);
			}
		}
	}
}
