using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace DivineLabs.IdleSlayer.Setup;
internal static class Program
{
    private const uint Command=0x111,Close=0x10,Destroy=2,Update=0x8001;
    private static IntPtr window,pathBox,status,bar,install,browse;
    private static bool busy,done,failed;
    private static string latest="",installed="";private static int percentage;
    private static readonly WndProc Callback=WindowProcedure;
    private static readonly IntPtr Background=CreateSolidBrush(0x1c120e);
    [STAThread] public static int Main(string[] args)
    {
        string Arg(string name,string fallback=""){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        bool silent=args.Contains("--silent"),filesOnly=args.Contains("--files-only");string target=Arg("--dir",SetupOperations.DefaultDirectory);
        try
        {
            if(args.Contains("--cleanup")){SetupOperations.Cleanup(target,int.Parse(Arg("--parent")),filesOnly);return 0;}
            if(args.Contains("--uninstall"))
            {
                target=Arg("--dir",Path.GetDirectoryName(Environment.ProcessPath)!);
                if(!silent&&MessageBoxW(0,"Удалить программу DivineLabs IdleSlayer Mods?\nНастройки и резервные копии сохранятся.","DivineLabs",0x24)!=6)return 0;
                SetupOperations.BeginUninstall(target,filesOnly,silent);return 0;
            }
            if(silent){SetupOperations.Install(target,!args.Contains("--no-integrations"),(_,_)=>{});if(!args.Contains("--no-launch"))SetupOperations.Launch(target);return 0;}
            RunWindow(target);return failed?2:0;
        }
        catch(Exception e)
        {
            try{Directory.CreateDirectory(Path.Combine(target,"Data"));File.AppendAllText(Path.Combine(target,"Data","setup.log"),DateTime.UtcNow+" "+e+Environment.NewLine);}catch{}
            if(!silent)MessageBoxW(0,Russian(e),"Установка DivineLabs",0x10);return 2;
        }
    }
    private static string Russian(Exception e)=>e.Message.Length>0&&e.Message[0]>='А'&&e.Message[0]<='я'?e.Message:"Не удалось завершить установку. Подробности сохранены в Data/setup.log.";
    private static void RunWindow(string target)
    {
        CoInitializeEx(0,2);IntPtr module=GetModuleHandleW(null);
        var cls=new WindowClass{Size=(uint)Marshal.SizeOf<WindowClass>(),Instance=module,Procedure=Marshal.GetFunctionPointerForDelegate(Callback),ClassName="DivineLabs.IdleSlayer.Setup",Background=Background,Cursor=LoadCursorW(0,new IntPtr(32512)),Icon=LoadIconW(module,new IntPtr(32512))};
        if(RegisterClassExW(ref cls)==0)throw new IOException("Не удалось создать окно установщика.");
        var controls=new CommonControls{Size=(uint)Marshal.SizeOf<CommonControls>(),Classes=0x20};InitCommonControlsEx(ref controls);
        window=CreateWindowExW(0,cls.ClassName,"DivineLabs • Установка IdleSlayer Mods",0x00CA0000,unchecked((int)0x80000000),unchecked((int)0x80000000),760,410,0,0,module,0);
        if(window==0)throw new IOException("Не удалось открыть установщик.");
        IntPtr font=CreateFontW(-18,0,0,0,400,0,0,0,1,0,0,5,0,"Segoe UI");
        IntPtr Control(string type,string text,uint style,int x,int y,int width,int height,int id=0){var h=CreateWindowExW(type=="EDIT"?0x200u:0,type,text,0x50000000|style,x,y,width,height,window,new IntPtr(id),module,0);SendMessageW(h,0x30,font,new IntPtr(1));return h;}
        Control("STATIC","DIVINELABS  /  IDLE SLAYER MODS",0,28,24,680,28);
        Control("STATIC","Лаунчер, загрузчик и все компоненты будут установлены автоматически.\nУстановленная игра Idle Slayer должна быть доступна в Steam.",0,28,62,680,48);
        Control("STATIC","Папка программы",0,28,122,680,24);
        pathBox=Control("EDIT",target,0x80,28,150,554,30);browse=Control("BUTTON","Выбрать…",0,596,150,116,30,102);
        status=Control("STATIC","Выберите папку и нажмите «Установить».",0,28,202,684,38);
        bar=Control("msctls_progress32","",0,28,246,684,16);SendMessageW(bar,0x406,0,new IntPtr(100));
        install=Control("BUTTON","Установить",1,522,292,190,38,103);
        Control("STATIC","DivineLabs 0.2.0  ·  Windows x64  ·  Steam",0,28,300,464,28);
        ShowWindow(window,5);UpdateWindow(window);
        while(GetMessageW(out var message,0,0,0)>0){TranslateMessage(ref message);DispatchMessageW(ref message);}
    }
    private static IntPtr WindowProcedure(IntPtr handle,uint message,IntPtr w,IntPtr l)
    {
        switch(message)
        {
            case Command:
                int id=(int)(w.ToInt64()&0xffff);
                if(id==102&&!busy){string selected=BrowseFolder(handle);if(selected!=null)SetWindowTextW(pathBox,selected);return 0;}
                if(id==103)
                {
                    if(done){DestroyWindow(handle);return 0;}
                    if(busy)return 0;var text=new StringBuilder(32768);GetWindowTextW(pathBox,text,text.Capacity);installed=text.ToString();busy=true;failed=false;EnableWindow(install,false);EnableWindow(pathBox,false);EnableWindow(browse,false);
                    Task.Run(()=>
                    {
                        try{SetupOperations.Install(installed,true,(s,p)=>{latest=s;percentage=p;PostMessageW(window,Update,0,0);});SetupOperations.Launch(installed);done=true;}
                        catch(Exception e){latest=Russian(e);failed=true;try{Directory.CreateDirectory(Path.Combine(installed,"Data"));File.AppendAllText(Path.Combine(installed,"Data","setup.log"),e+Environment.NewLine);}catch{}}
                        finally{busy=false;PostMessageW(window,Update,0,0);}
                    });return 0;
                }
                break;
            case Update:
                SetWindowTextW(status,latest);SendMessageW(bar,0x402,new IntPtr(percentage),0);
                if(!busy){EnableWindow(install,true);SetWindowTextW(install,done?"Готово":"Повторить");EnableWindow(pathBox,!done);EnableWindow(browse,!done);}return 0;
            case Close:if(busy){MessageBoxW(handle,"Дождитесь завершения установки.","DivineLabs",0x40);return 0;}DestroyWindow(handle);return 0;
            case Destroy:PostQuitMessage(0);return 0;
            case 0x138:SetTextColor(w,0xF3E8DC);SetBkMode(w,1);return Background;
        }
        return DefWindowProcW(handle,message,w,l);
    }
    private static string BrowseFolder(IntPtr owner)
    {
        var info=new BrowseInfo{Owner=owner,Title="Выберите папку программы DivineLabs",Flags=0x51};IntPtr list=SHBrowseForFolderW(ref info);if(list==0)return null;
        try{var path=new StringBuilder(32768);return SHGetPathFromIDListW(list,path)?path.ToString():null;}finally{CoTaskMemFree(list);}
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]private delegate IntPtr WndProc(IntPtr window,uint message,IntPtr w,IntPtr l);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct WindowClass{public uint Size,Style;public IntPtr Procedure;public int ClassExtra,WindowExtra;public IntPtr Instance,Icon,Cursor,Background;public string MenuName,ClassName;public IntPtr SmallIcon;}
    [StructLayout(LayoutKind.Sequential)]private struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct Message{public IntPtr Window;public uint Id;public IntPtr W,L;public uint Time;public Point Point;public uint Private;}
    [StructLayout(LayoutKind.Sequential)]private struct CommonControls{public uint Size,Classes;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct BrowseInfo{public IntPtr Owner,Root,Display;public string Title;public uint Flags;public IntPtr Callback,Parameter;public int Image;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern ushort RegisterClassExW(ref WindowClass c);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CreateWindowExW(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll")]private static extern IntPtr DefWindowProcW(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]private static extern int GetMessageW(out Message m,IntPtr h,uint min,uint max);
    [DllImport("user32.dll")]private static extern bool TranslateMessage(ref Message m);
    [DllImport("user32.dll")]private static extern IntPtr DispatchMessageW(ref Message m);
    [DllImport("user32.dll")]private static extern bool ShowWindow(IntPtr h,int command);
    [DllImport("user32.dll")]private static extern bool UpdateWindow(IntPtr h);
    [DllImport("user32.dll")]private static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")]private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")]private static extern bool EnableWindow(IntPtr h,bool value);
    [DllImport("user32.dll")]private static extern IntPtr SendMessageW(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]private static extern bool PostMessageW(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool SetWindowTextW(IntPtr h,string text);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowTextW(IntPtr h,StringBuilder text,int length);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr LoadIconW(IntPtr h,IntPtr name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr LoadCursorW(IntPtr h,IntPtr name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int MessageBoxW(IntPtr h,string text,string title,uint type);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandleW(string name);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")]private static extern uint SetTextColor(IntPtr h,uint color);
    [DllImport("gdi32.dll")]private static extern int SetBkMode(IntPtr h,int mode);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CreateFontW(int height,int width,int angle,int orient,int weight,uint italic,uint underline,uint strike,uint charset,uint output,uint clip,uint quality,uint pitch,string face);
    [DllImport("comctl32.dll")]private static extern bool InitCommonControlsEx(ref CommonControls controls);
    [DllImport("ole32.dll")]private static extern int CoInitializeEx(IntPtr reserved,uint flags);
    [DllImport("ole32.dll")]private static extern void CoTaskMemFree(IntPtr pointer);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SHBrowseForFolderW(ref BrowseInfo info);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern bool SHGetPathFromIDListW(IntPtr list,StringBuilder path);
}
