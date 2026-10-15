using System.Runtime.InteropServices;
namespace DivineLabs.IdleSlayer;
internal static class WindowHotkey
{
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    [DllImport("user32.dll")]private static extern bool SystemParametersInfoW(uint action,uint parameter,out bool value,uint flags);
    private static readonly bool[] held=new bool[256];
    private static bool focused;
    internal static bool ReducedMotion {get;private set;}
    internal static void Initialize(){if(SystemParametersInfoW(0x1042,0,out bool animations,0))ReducedMotion=!animations;}
    internal static bool Foreground {get;private set;}
    internal static bool Down(int key)=>(GetAsyncKeyState(key)&0x8000)!=0;
    internal static int Modifiers=>(Down(17)?1:0)|(Down(18)?2:0)|(Down(16)?4:0);
    internal static int Poll(Settings config,bool suppress,bool capture,out KeyBinding captured)
    {
        captured=null;GetWindowThreadProcessId(GetForegroundWindow(),out uint pid);Foreground=pid==(uint)Environment.ProcessId;
        if(!Foreground){focused=false;return 0;}
        if(!focused){for(int key=0;key<256;key++)held[key]=Down(key);focused=true;return 0;}
        int result=0,modifiers=Modifiers;
        if(capture){
            for(int key=3;key<255;key++){
                bool down=Down(key),edge=down&&!held[key];held[key]=down;
                if(edge&&key is not (16 or 17 or 18 or 91 or 92 or 160 or 161 or 162 or 163 or 164 or 165)&&captured==null)
                    captured=new(){Key=key,Modifiers=modifiers};
            }
            return 0;
        }
        for(int i=0;i<FeatureCatalog.Actions.Length;i++){
            var binding=config.Bindings[FeatureCatalog.Actions[i]];if(binding.Key==0)continue;
            bool down=Down(binding.Key),edge=down&&!held[binding.Key];
            if(edge&&binding.Modifiers==modifiers&&(!suppress||(i==0&&(binding.Key==45||binding.Key>=112&&binding.Key<=135||binding.Modifiers!=0)))&&!Down(91)&&!Down(92))result|=1<<i;
        }
        // Update after all actions so distinct chords sharing a key remain independent.
        foreach(var action in FeatureCatalog.Actions){int key=config.Bindings[action].Key;if(key!=0)held[key]=Down(key);}
        return result;
    }
    internal static void Reset(){focused=false;}
}