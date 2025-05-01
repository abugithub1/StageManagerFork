using StageManager.Native.Window;

namespace StageManager.Strategies
{
    internal interface IWindowStrategy
	{
		void Show(IWindow window);

		void Hide(IWindow window);

		// Add methods for deferred positioning
		void DeferShow(IWindow window, IWindowsDeferPosHandle handle);
		void DeferHide(IWindow window, IWindowsDeferPosHandle handle);
	}
}