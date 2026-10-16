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
    public ResourceView(){InitializeComponent();Resource.ItemsSource=ResourceChoice.All;Resource.SelectedIndex=0;}
    internal void Attach(SettingsClient value)=>client=value;
    internal async void RefreshIfIdle(){if(!busy&&pending==null)await RefreshBalance();}
    private async void RefreshClick(object sender,RoutedEventArgs e){if(pending!=null){Result.Text="Сначала проверьте результат предыдущей команды.";return;}await RefreshBalance();}
    private async Task RefreshBalance()
    {
        if(busy||client==null)return;SetBusy(true);
        try{var reply=await client.Execute(client.Command(new()));if(reply.Error!=null)throw new InvalidDataException(reply.Error);ShowState(reply.Result?.State);Result.Text="Баланс обновлён.";}
        catch(Exception error){Product.Record(error);Result.Text=error is InvalidDataException?error.Message:"Нет связи. Запустите игру и дождитесь игровой сцены.";}
        finally{SetBusy(false);}
    }
    private static string N(double value)=>value.ToString("G8",CultureInfo.InvariantCulture);
    private void ShowState(GameState value)
    {
        if(value==null)return;state=value;
        Balances.Text=$"Монеты: {N(state.Coins)}\nSP: {N(state.SlayerPoints)}    DP: {N(state.DivinityPoints)}    USP: {N(state.UltraSlayerPoints)}\nКамни душ: {N(state.JewelsOfSoul)} (локальный баланс)";FilterMaterials();
    }
    private void FilterMaterials(){if(Material==null)return;string id=(Material.SelectedItem as MaterialState)?.Id;Material.ItemsSource=state?.Materials.Where(m=>m.Title.Contains(MaterialSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();Material.SelectedItem=Material.Items.Cast<MaterialState>().FirstOrDefault(m=>m.Id==id);if(Material.SelectedIndex<0&&Material.Items.Count>0)Material.SelectedIndex=0;}
    private void SearchChanged(object sender,TextChangedEventArgs e)=>FilterMaterials();
    private void MaterialChanged(object sender,SelectionChangedEventArgs e){if(MaterialHint!=null)MaterialHint.Text=Material.SelectedItem is MaterialState m?$"В сумке: {N(m.Amount)} / {N(m.Maximum)}. Лимит сумки сохраняется.":"Выберите материал.";}
    private void ResourceChanged(object sender,SelectionChangedEventArgs e)
    {
        if(MaterialPanel==null)return;var kind=(Resource.SelectedItem as ResourceChoice)?.Kind;MaterialPanel.Visibility=kind==ResourceKind.Material?Visibility.Visible:Visibility.Collapsed;
        Hint.Text=kind switch{ResourceKind.JewelsOfSoul=>"Меняется локальный баланс заработанных камней. Покупка за реальные деньги и квитанция Steam не создаются. Проверка облака и другого устройства отдельно не подтверждена.",ResourceKind.UltraSlayerPoints=>"Количество ограничено конечностью игровой формулы следующего USP.",ResourceKind.SlayerPoints=>"Выдаются доступные SP; нераспределённые при восхождении очки сохраняются.",_=>"Выданное количество не умножается множителями тренера."};
    }
    private void SetBusy(bool value){busy=value;Editor.IsEnabled=!value&&pending==null;Give.IsEnabled=!value;Give.Content=pending==null?"Выдать":"Проверить результат";}
    private async void GiveClick(object sender,RoutedEventArgs e)
    {
        if(busy||client==null)return;
        if(pending==null){
            try{if(state==null)throw new InvalidDataException("Сначала обновите баланс.");if(!double.TryParse(Amount.Text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double n))throw new InvalidDataException("Укажите целое конечное количество от 1.");var command=new GameCommand{Operation="grant",Resource=((ResourceChoice)Resource.SelectedItem).Kind,Amount=n,MaterialId=(Material.SelectedItem as MaterialState)?.Id??""};command.Validate();pending=client.Command(command);}
            catch(Exception error){Result.Text=error.Message;return;}
        }
        SetBusy(true);bool completed=false;
        try{var reply=await client.Execute(pending);Result.Text=reply.Error??reply.Result?.Message??"Ответ получен.";pending=null;completed=true;}
        catch(Exception error){Product.Record(error);Result.Text="Ответ не получен. Нажмите «Проверить результат»: команда с тем же номером не выдаст ресурс повторно. При смене сессии выдача отклоняется.";}
        finally{SetBusy(false);}
        if(completed){string result=Result.Text;await RefreshBalance();Result.Text=result;}
    }
}
