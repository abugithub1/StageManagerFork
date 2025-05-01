using StageManager.Native.PInvoke;
using StageManager.Native.Window;
using System;

namespace StageManager.Strategies
{
	/// <summary>
	/// Shows and hides windows, which makes them disappear completely until they are showed again.
	/// The user cannot bring them back without some advanced tricks.
	/// </summary>
	internal class ShowAndHideWindowStrategy : IWindowStrategy
	{
		public void Show(IWindow window)
		{
			window.ShowInCurrentState();
		}

		public void Hide(IWindow window)
		{
			Win32.ShowWindow(window.Handle, Win32.SW.SW_HIDE);
		}

		// Add placeholder implementations for the interface
		public void DeferShow(IWindow window, IWindowsDeferPosHandle handle)
		{
			// Placeholder: Calls original Show method
			Show(window);
		}

		public void DeferHide(IWindow window, IWindowsDeferPosHandle handle)
		{
			// Placeholder: Calls original Hide method
			Hide(window);
		}
	}
}
