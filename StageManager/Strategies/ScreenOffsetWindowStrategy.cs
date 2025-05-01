using StageManager.Native.Window;

namespace StageManager.Strategies
{
	internal class ScreenOffsetWindowStrategy : IWindowStrategy
	{
		public void Show(IWindow window)
		{
			throw new System.NotImplementedException();
		}

		public void Hide(IWindow window)
		{
			throw new System.NotImplementedException();
		}

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
