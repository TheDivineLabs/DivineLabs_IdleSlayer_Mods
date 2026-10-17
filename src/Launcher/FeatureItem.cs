using System.ComponentModel;
using System.Globalization;
namespace DivineLabs.IdleSlayer.Launcher;
public sealed class FeatureItem:INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public FeatureDefinition Definition {get;}
    public string Id=>Definition.Id;
    public string Title=>SetupText.Current.Translate(Definition.Title);
    public string Description=>SetupText.Current.Translate(Definition.Description);
    public string Category=>Definition.Category;
    public System.Windows.Visibility MultiplierVisibility=>Definition.HasMultiplier?System.Windows.Visibility.Visible:System.Windows.Visibility.Collapsed;
    public bool Enabled {get;private set;}
    public string Value {get;private set;}
    public string FavoriteLabel {get;private set;}
    public string KeyLabel {get;private set;}
    public string Error {get;set;}="";
    public FeatureItem(FeatureDefinition d)=>Definition=d;
    public void Refresh(Settings s){Enabled=s.FeatureEnabled(Id);Value=Definition.HasMultiplier?s.Multiplier(Id).ToString(CultureInfo.InvariantCulture):"";FavoriteLabel=s.Favorites.Contains(Id)?"★":"☆";KeyLabel=SetupText.Current.Translate(s.Bindings[Id].ToString());Notify();}
    public void Notify()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(""));
}
