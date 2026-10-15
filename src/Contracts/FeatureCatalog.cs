namespace DivineLabs.IdleSlayer;
public sealed record FeatureDefinition(string Id,string Title,string Category,string Description,string Action,bool HasMultiplier=true);
public static class FeatureCatalog
{
    public static readonly FeatureDefinition[] All = {
        new("coins","Монеты","Экономика","Умножает положительный доход. Стоимость покупок остаётся обычной.","coins"),
        new("damage","Урон","Бой","Усиливает урон по обычным врагам.","damage"),
        new("souls","Души","Экономика","Умножает начисление душ после игровых бонусов. Не меняет предпросмотр и камни душ.","souls")
    };
    public static readonly string[] Actions={"menu","widget","all","coins","damage","souls"};
    public static string ActiveTitle(string id)=>id switch{"coins"=>"Множитель монет","damage"=>"Множитель урона","souls"=>"Множитель душ",_=>ActionTitle(id)};
    public static string ActionTitle(string id)=>id switch{"menu"=>"Открыть меню","widget"=>"Показать виджет","all"=>"Все функции","coins"=>"Монеты","damage"=>"Урон","souls"=>"Души",_=>id};
}
public sealed record KeyBinding
{
    public int Key {get;set;}
    public int Modifiers {get;set;} // Ctrl=1, Alt=2, Shift=4.
    public void Validate()
    {
        if(Key==0){if(Modifiers!=0)throw new InvalidDataException("Укажите основную клавишу.");return;}
        if(Modifiers<0||Modifiers>7||Key<3||Key>254||Key is 16 or 17 or 18 or 91 or 92 or 93 or 160 or 161 or 162 or 163 or 164 or 165)
            throw new InvalidDataException("Выберите клавишу или дополнительную кнопку мыши.");
        if((Key==115&&(Modifiers&2)!=0)||(Key==46&&(Modifiers&3)==3)||(Key==9&&(Modifiers&2)!=0)||(Key==27&&(Modifiers&1)!=0))
            throw new InvalidDataException("Это сочетание используется Windows.");
    }
    public override string ToString()=>Key==0?"Не назначено":
        ((Modifiers&1)!=0?"Ctrl + ":"")+((Modifiers&2)!=0?"Alt + ":"")+((Modifiers&4)!=0?"Shift + ":"")+KeyName(Key);
    public static string KeyName(int key)=>key switch {45=>"Insert",46=>"Delete",36=>"Home",35=>"End",33=>"Page Up",34=>"Page Down",27=>"Esc",32=>"Space",9=>"Tab",13=>"Enter",4=>"Mouse 3",5=>"Mouse 4",6=>"Mouse 5",>=112 and <=135=>"F"+(key-111),>=48 and <=90=>((char)key).ToString(),_=>"Клавиша "+key};
}