using StageManager.Native.PInvoke;
using StageManager.Native.Window;
using System;
using System.Collections.Generic;

namespace StageManager.Native
{
    public class WindowsDeferPosHandle : IWindowsDeferPosHandle
    {
        private IntPtr _info;

        public WindowsDeferPosHandle(IntPtr info)
        {
            _info = info;
        }

        public void Dispose()
        {
            if (_info != IntPtr.Zero)
            {
                Win32.EndDeferWindowPos(_info);
                _info = IntPtr.Zero;
            }
        }

        public void DeferWindowPos(IWindow window, IWindowLocation location)
        {
            var flags = Win32.SWP.SWP_FRAMECHANGED |
                        Win32.SWP.SWP_NOACTIVATE |
                        Win32.SWP.SWP_NOZORDER |
                        Win32.SWP.SWP_NOOWNERZORDER |
                        Win32.SWP.SWP_NOCOPYBITS;

            var offset = window.Offset;
            int X = location.X + offset.X;
            int Y = location.Y + offset.Y;
            int Width = location.Width + offset.Width;
            int Height = location.Height + offset.Height;

            var oldLocation = window.Location;
            if (oldLocation.X != X || oldLocation.Y != Y || oldLocation.Width != Width || oldLocation.Height != Height)
            {
                _info = Win32.DeferWindowPos(
                    _info,
                    window.Handle,
                    IntPtr.Zero,
                    X, Y, Width, Height,
                    flags);
            }

            if (_info == IntPtr.Zero)
            {
                Console.WriteLine($"Win32.DeferWindowPos failed when trying to position window {window.Handle}");
            }
        }

        public void DeferStateChange(IntPtr hwnd, Win32.SWP uFlags)
        {
            var finalFlags = uFlags |
                             Win32.SWP.SWP_NOMOVE |
                             Win32.SWP.SWP_NOSIZE |
                             Win32.SWP.SWP_NOZORDER |
                             Win32.SWP.SWP_NOACTIVATE;

            _info = Win32.DeferWindowPos(
                _info,
                hwnd,
                IntPtr.Zero,
                0, 0, 0, 0,
                finalFlags);

            if (_info == IntPtr.Zero)
            {
                Console.WriteLine($"Win32.DeferWindowPos failed when trying to change state for window {hwnd}");
            }
        }
    }
}
