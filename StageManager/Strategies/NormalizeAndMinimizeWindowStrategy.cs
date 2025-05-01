using StageManager.Native.PInvoke;
using StageManager.Native.Window;
using System;

namespace StageManager.Strategies
{
    internal class NormalizeAndMinimizeWindowStrategy : IWindowStrategy
	{
		public void Show(IWindow window)
		{
			Win32.ShowWindow(window.Handle, Win32.SW.SW_RESTORE);
		}

		public void Hide(IWindow window)
		{
			Win32.ShowWindow(window.Handle, Win32.SW.SW_MINIMIZE);
		}

		public void DeferShow(IWindow window, IWindowsDeferPosHandle handle)
		{
			Show(window);
		}

		public void DeferHide(IWindow window, IWindowsDeferPosHandle handle)
		{
			Hide(window);
		}
	}
}
