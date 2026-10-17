using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DivineLabs.IdleSlayer.Runtime;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class ResourceView:UserControl
{
    private SettingsClient client;
    private GameState state;
    private SessionRequest pending;
    private bool busy;
    public ResourceView(){InitializeComponent();DivineLabs.DesktopUI.ThemeManager.Attach(this);RefreshLanguage();}
    internal void RefreshLanguage()
    {
        var kind=(Resource.SelectedItem as ResourceChoice)?.Kind;
        Resource.ItemsSource=ResourceChoice.All.Select(choice=>choice with{Title=SetupText.Current.Translate(choice.Title)}).ToArray();
        Resource.SelectedItem=Resource.Items.Cast<ResourceChoice>().FirstOrDefault(choice=>choice.Kind==kind)??Resource.Items[0];
        if(state!=null)ShowState(state);
    }
    internal void Attach(SettingsClient value)=>client=value;
    internal async void RefreshIfIdle(){if(!busy&&pending==null)await RefreshBalance();}
    private async void RefreshClick(object sender,RoutedEventArgs e){if(pending!=null){Result.Text=SetupText.Current["CheckPrevious"];return;}await RefreshBalance();}
    private async Task RefreshBalance()
    {
        if(busy||client==null)return;SetBusy(true);
        try{var reply=await client.Execute(client.Command(new()));if(reply.Error!=null)throw new InvalidDataException(reply.Error);ShowState(reply.Result?.State);Result.Text=SetupText.Current["BalanceUpdated"];}
        catch(Exception error){Product.Record(error);Result.Text=error is InvalidDataException?error.Message:SetupText.Current["NoScene"];}
        finally{SetBusy(false);}
    }
    private static string N(double value)=>value.ToString("G8",CultureInfo.InvariantCulture);
    private void ShowState(GameState value)
    {
        if(value==null)return;state=value;
        Balances.Text=$"{SetupText.Current["Coins"]}: {N(state.Coins)}\nSP: {N(state.SlayerPoints)}    DP: {N(state.DivinityPoints)}    USP: {N(state.UltraSlayerPoints)}\n{SetupText.Current["Jewels"]}: {N(state.JewelsOfSoul)} ({SetupText.Current["LocalBalance"]})";FilterMaterials();
    }
    private void FilterMaterials(){if(Material==null)return;string id=(Material.SelectedItem as MaterialState)?.Id;Material.ItemsSource=state?.Materials.Where(m=>m.Title.Contains(MaterialSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();Material.SelectedItem=Material.Items.Cast<MaterialState>().FirstOrDefault(m=>m.Id==id);if(Material.SelectedIndex<0&&Material.Items.Count>0)Material.SelectedIndex=0;}
    private void SearchChanged(object sender,TextChangedEventArgs e)=>FilterMaterials();
    private void MaterialChanged(object sender,SelectionChangedEventArgs e){if(MaterialHint!=null)MaterialHint.Text=Material.SelectedItem is MaterialState m?string.Format(SetupText.Current["InBag"],N(m.Amount),N(m.Maximum)):SetupText.Current["ChooseMaterial"];}
    private void ResourceChanged(object sender,SelectionChangedEventArgs e)
    {
        if(MaterialPanel==null)return;var kind=(Resource.SelectedItem as ResourceChoice)?.Kind;MaterialPanel.Visibility=kind==ResourceKind.Material?Visibility.Visible:Visibility.Collapsed;
        Hint.Text=kind switch{ResourceKind.JewelsOfSoul=>SetupText.Current["JewelsHint"],ResourceKind.UltraSlayerPoints=>SetupText.Current["UspHint"],ResourceKind.SlayerPoints=>SetupText.Current["SpHint"],_=>SetupText.Current["ResourceNotMultiplied"]};
    }
    private void SetBusy(bool value){busy=value;Editor.IsEnabled=!value&&pending==null;Give.IsEnabled=!value;Give.Content=pending==null?SetupText.Current["Grant"]:SetupText.Current["CheckResult"];}
    private async void GiveClick(object sender,RoutedEventArgs e)
    {
        if(busy||client==null)return;
        if(pending==null){
            try{if(state==null)throw new InvalidDataException(SetupText.Current["RefreshFirst"]);if(!double.TryParse(Amount.Text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double n))throw new InvalidDataException(SetupText.Current["InvalidAmount"]);var command=new GameCommand{Operation="grant",Resource=((ResourceChoice)Resource.SelectedItem).Kind,Amount=n,MaterialId=(Material.SelectedItem as MaterialState)?.Id??""};command.Validate();pending=client.Command(command);}
            catch(Exception error){Result.Text=error.Message;return;}
        }
        SetBusy(true);bool completed=false;
        try{var reply=await client.Execute(pending);Result.Text=reply.Error??reply.Result?.Message??SetupText.Current["ReplyReceived"];pending=null;completed=true;}
        catch(Exception error){Product.Record(error);Result.Text=SetupText.Current["ReplyMissing"];}
        finally{SetBusy(false);}
        if(completed){string result=Result.Text;await RefreshBalance();Result.Text=result;}
    }
}
