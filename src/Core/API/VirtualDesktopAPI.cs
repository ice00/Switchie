using System;
namespace Switchie
{

    public class WindowsVirtualDesktop
    {
        private static IWindowsVirtualDesktop _instance;
        public static void Restart()
        {
            WindowsVirtualDesktop._instance = null;
            GetInstance();
            WindowsVirtualDesktop._instance.Restart();
        }

        public static IWindowsVirtualDesktop GetInstance()
        {
            if (WindowsVirtualDesktop._instance == null)
            {
                if (Program.WindowsVersion.IsWin11_23H2())
                    _instance = new Switchie.VirtualDesktopAPI.Win11_.WindowsVirtualDesktop();
                else if (Program.WindowsVersion.IsWin11_22H2())
                    _instance = new Switchie.VirtualDesktopAPI.Win11_.WindowsVirtualDesktop();                    
                else if (Program.WindowsVersion.IsWin11_21H2())
                    _instance = new Switchie.VirtualDesktopAPI.Win11.WindowsVirtualDesktop();
                else if (Program.WindowsVersion.IsWin10())
                    _instance = new Switchie.VirtualDesktopAPI.Win10.WindowsVirtualDesktop();
                else if (Program.WindowsVersion.IsWin10LTSC())
                    _instance = new Switchie.VirtualDesktopAPI.Win10LTSC.WindowsVirtualDesktop();
                else
                    throw new PlatformNotSupportedException();
            }
            return WindowsVirtualDesktop._instance;
        }

    }

    public class WindowsVirtualDesktopManager
    {
        private static IWindowsVirtualDesktopManager _instance;
        public static void Restart() => WindowsVirtualDesktopManager._instance = null;

        public static IWindowsVirtualDesktopManager GetInstance()
        {
            if (WindowsVirtualDesktopManager._instance == null)
            {
                if (Program.WindowsVersion.IsWin11_23H2())
                    _instance = new Switchie.VirtualDesktopAPI.Win11_.WindowsVirtualDesktopManager();
                else if (Program.WindowsVersion.IsWin11_22H2())
                    _instance = new Switchie.VirtualDesktopAPI.Win11_.WindowsVirtualDesktopManager();                    
                else if (Program.WindowsVersion.IsWin11_21H2())
                    _instance = new Switchie.VirtualDesktopAPI.Win11.WindowsVirtualDesktopManager();
                else if (Program.WindowsVersion.IsWin10())
                    _instance = new Switchie.VirtualDesktopAPI.Win10.WindowsVirtualDesktopManager();
                else if (Program.WindowsVersion.IsWin10LTSC())
                    _instance = new Switchie.VirtualDesktopAPI.Win10LTSC.WindowsVirtualDesktopManager();
                else
                    throw new PlatformNotSupportedException();
            }
            return WindowsVirtualDesktopManager._instance;
        }
    }

}