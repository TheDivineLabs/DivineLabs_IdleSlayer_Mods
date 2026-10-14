using System.Runtime.InteropServices;
namespace DivineLabs.IdleSlayer;
internal static class WindowHotkey
{
    private static bool held;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    internal static bool F8Pressed()
    {
        GetWindowThreadProcessId(GetForegroundWindow(),out uint processId);
        if(processId!=(uint)Environment.ProcessId){held=false;return false;}
        short state=GetAsyncKeyState(0x77);
        bool down=(state & 0x8000)!=0;
        // The low bit also catches a short press that ended between two Unity frames.
        bool edge=(down&&!held)||(!down&&(state&1)!=0);
        held=down;return edge;
    }
}
