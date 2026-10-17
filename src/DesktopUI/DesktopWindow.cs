using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DivineLabs.DesktopUI;

// Custom chrome must keep the client area inside the current monitor's work area.
public static class DesktopWindow
{
    public static void Attach(Window window)
    {
        HwndSource source=null;
        window.SourceInitialized+=(_,_)=>{
            var handle=new WindowInteropHelper(window).Handle;
            source=HwndSource.FromHwnd(handle);
            source?.AddHook(WindowProc);
            if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)){
                int doNotRound=1;
                DwmSetWindowAttribute(handle,33,ref doNotRound,sizeof(int));
            }
        };
        window.Closed+=(_,_)=>source?.RemoveHook(WindowProc);
    }
    private static IntPtr WindowProc(IntPtr handle,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message!=0x0024)return IntPtr.Zero; // WM_GETMINMAXINFO, physical desktop pixels.
        var monitor=MonitorFromWindow(handle,2);
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return IntPtr.Zero;
        var limits=Marshal.PtrToStructure<MinMaxInfo>(lParam);
        limits.MaxPosition=new PointI{X=info.Work.Left-info.Monitor.Left,Y=info.Work.Top-info.Monitor.Top};
        limits.MaxSize=new PointI{X=info.Work.Right-info.Work.Left,Y=info.Work.Bottom-info.Work.Top};
        Marshal.StructureToPtr(limits,lParam,false);
        handled=true;
        return IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)] private struct PointI{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] private struct RectI{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo{public PointI Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo{public int Size;public RectI Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
