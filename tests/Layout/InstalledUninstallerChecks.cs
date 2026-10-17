using System.IO;
using System.Windows.Automation;
using DivineLabs.IdleSlayer.Launcher;

internal static class InstalledUninstallerChecks
{
    // Inspect the real installed EXE's child window, not a separately constructed test window.
    internal static void Run(string output,nint handle,string expectedFolder)
    {
        var window=AutomationElement.FromHandle(handle);
        AutomationElement Id(string id)=>window.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id))??throw new Exception("Missing control: "+id);
        void Invoke(AutomationElement element)=>((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        var nextNames=new HashSet<string>();var removeNames=new HashSet<string>();
        foreach(string language in SetupText.Languages){SetupText.Current.Select(language);nextNames.Add(SetupText.Current["Next"]);removeNames.Add(SetupText.Current["Remove"]);}
        var primary=Id("Primary");
        if(nextNames.Contains(primary.Current.Name))Invoke(primary);
        else if(!removeNames.Contains(primary.Current.Name))throw new Exception("Unexpected wizard stage; no action invoked");
        AutomationElement? group=null;
        for(int count=0;count<50&&group==null;count++){
            foreach(string language in SetupText.Languages){
                SetupText.Current.Select(language);
                var matches=window.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,SetupText.Current["OtherCopies"]));
                foreach(AutomationElement candidate in matches)if(candidate.TryGetCurrentPattern(ExpandCollapsePattern.Pattern,out _)){group=candidate;break;}
                if(group!=null)break;
            }
            if(group==null)Thread.Sleep(100);
        }
        if(group==null||group.Current.IsOffscreen)throw new Exception("Installed uninstaller does not show other installations");
        var expansion=(ExpandCollapsePattern)group.GetCurrentPattern(ExpandCollapsePattern.Pattern);
        if(expansion.Current.ExpandCollapseState!=ExpandCollapseState.Expanded)throw new Exception("Installed uninstaller list is not expanded");
        var target=(ValuePattern)Id("TargetPath").GetCurrentPattern(ValuePattern.Pattern);
        if(!target.Current.IsReadOnly||!string.Equals(target.Current.Value,expectedFolder,StringComparison.OrdinalIgnoreCase))throw new Exception("Installed uninstaller target is not fixed");
        File.WriteAllText(Path.Combine(output,"installed-uninstaller.txt"),"PASS: root uninstaller -> real maintenance window -> Options -> visible expanded copies; original target read-only. UI Automation, no physical clicks.");
        Invoke(Id("Cancel"));
        for(int count=0;count<50;count++){
            try{Invoke(Id("ConfirmExit"));return;}catch(ElementNotAvailableException){}catch(InvalidOperationException){}
            Thread.Sleep(100);
        }
        throw new Exception("Could not close test uninstaller cleanly");
    }
}
