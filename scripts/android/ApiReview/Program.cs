using System.Text.Json;
using Mono.Cecil;
if(args.Length!=2)throw new ArgumentException("Нужны Android Interop и новый путь отчёта.");
using var game=AssemblyDefinition.ReadAssembly(Path.Combine(args[0],"Assembly-CSharp.dll"));
var checks=new List<string>();
TypeDefinition Type(string name)=>game.MainModule.Types.Single(t=>t.FullName=="Il2Cpp."+name);
MethodDefinition Method(string type,string name,string first=null,bool multiple=false){
 var methods=Type(type).Methods.Where(m=>m.IsPublic&&m.Name==name&&(first==null||m.Parameters.Count>0&&m.Parameters[0].ParameterType.FullName==first)).ToArray();
 if(methods.Length==0||methods.Length!=1&&!multiple)throw new InvalidDataException($"Неоднозначный/отсутствующий Android метод: {type}.{name}");
 foreach(var method in methods)checks.Add(method.FullName);
 if(multiple&&methods.Select(m=>m.ReturnType.FullName).Distinct().Count()!=1)throw new InvalidDataException("Разные результаты перегрузок: "+type+"."+name);
 return methods[0];
}
void Returns(MethodDefinition method,string result){if(method.ReturnType.FullName!=result)throw new InvalidDataException("Другой результат: "+method.FullName);}
Returns(Method("PlayerInventory","AddCoins","System.Double"),"System.Void");
var souls=Method("SlayerPoints","AddSouls","System.Double");
if(souls.Parameters.Count<2||souls.Parameters[1].ParameterType.FullName!="System.Boolean")throw new InvalidDataException("Другой контракт AddSouls");
Returns(souls,"System.Double");
Method("EnemyGameObject","TakeDamage","System.Double");
Returns(Method("InputManager","Update"),"System.Void");
foreach(string name in new[]{"OnPointerDown","OnPointerUp"})Method("JumpPanel",name);
foreach(string name in new[]{"CheckAction","TryJump"})Method("PlayerMovement",name);
Returns(Method("Power","CalculateCost",multiple:true),"System.Double");
Returns(Method("Power","CalculateMaxCost",multiple:true),"Il2Cpp.Power/MaxCost");
Returns(Method("Upgrade","GetCost",multiple:true),"System.Double");
Method("DivinitiesManager","ActivateDivinity","System.String",multiple:true);
foreach(string name in new[]{"DeactivateDivinity","ResetDivinities"})Method("DivinitiesManager",name,multiple:true);
foreach(string name in new[]{"Unlock","Deactivate"})Method("UpgradeOfTime",name,multiple:true);
Method("SkillTreeManager","BuyAscensionSkill",multiple:true);
foreach(string type in new[]{"PermanentCraftableItem","TemporaryCraftableItem"}){
 Returns(Method(type,"GetRequirements",multiple:true),"Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1<Il2Cpp.MaterialRequirement>");
 Method(type,"Craft",multiple:true);
}
Returns(Method("TemporaryCraftableItem","HowManyCanCraft",multiple:true),"System.Double");
// Native-backed members used by the shared implementation are also checked by compiling
// AndroidMod against this exact Interop. This review covers the reflection-only patch targets.
var report=new{SchemaVersion=1,StaticApiChecked=true,RuntimeVerified=false,Targets=checks};
using var output=new FileStream(args[1],FileMode.CreateNew,FileAccess.Write,FileShare.None);
JsonSerializer.Serialize(output,report,new JsonSerializerOptions{WriteIndented=true});
Console.WriteLine($"Android patch targets checked: {checks.Count}. Runtime not verified.");
