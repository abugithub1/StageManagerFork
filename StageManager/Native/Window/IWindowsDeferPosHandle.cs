using System;
using StageManager.Native.PInvoke;

namespace StageManager.Native.Window
{
    public interface IWindowsDeferPosHandle : IDisposable
    {
        void DeferWindowPos(IWindow window, IWindowLocation location);

        // Update method signature to use Win32.SWP enum
        void DeferStateChange(IntPtr hwnd, Win32.SWP uFlags);
    }
}
