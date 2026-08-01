using System.Reflection;

static void Dump(Type? type)
{
    if (type == null) return;
    Console.WriteLine($"\nTYPE {type.FullName} base={type.BaseType}");
    foreach (var c in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        Console.WriteLine("CTOR " + c);
    foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        Console.WriteLine($"PROP {p.PropertyType} {p.Name} get={p.GetMethod?.IsVirtual} set={p.SetMethod?.IsVirtual}");
    foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        Console.WriteLine("METHOD " + m);
    foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        Console.WriteLine($"FIELD {f.FieldType} {f.Name}");
}

var sts = Assembly.LoadFrom(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.godot/mono/temp/bin/Debug/sts2.dll")));
var ritsu = Assembly.LoadFrom(@"C:\Users\LU\.nuget\packages\sts2.ritsulib\0.5.2\lib\net9.0\STS2-RitsuLib.dll");

foreach (string name in new[]
{
    "MegaCrit.Sts2.Core.Models.CharacterModel",
    "MegaCrit.Sts2.Core.Models.CardModel",
    "MegaCrit.Sts2.Core.Entities.Cards.CardPlay",
    "MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState",
    "MegaCrit.Sts2.Core.Combat.PlayerTurnController",
    "MegaCrit.Sts2.Core.Combat.CombatState",
}) Dump(sts.GetType(name));

foreach (string name in new[]
{
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceDefinition",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceUse",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceCardUse",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourcePaymentLine",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourcePaymentPlan",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceStateStore",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceCmd",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceCardCost",
    "STS2RitsuLib.Combat.SecondaryResources.ICardSecondaryResourceUseContributor",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceTurnStartPolicy",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourcePlayUse",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceCost",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceUseKind",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceMaxContext",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceSpendContext",
    "STS2RitsuLib.Combat.SecondaryResources.ISecondaryResourceHookListener",
    "STS2RitsuLib.Combat.SecondaryResources.SecondaryResourceModelHookRegistry",
}) Dump(ritsu.GetType(name));

Dump(sts.GetType("MegaCrit.Sts2.Core.Models.AbstractModel"));

foreach (var type in ritsu.GetTypes().Where(t => t.Namespace == "STS2RitsuLib.Combat.SecondaryResources" &&
    (t.Name.Contains("Use") || t.Name.Contains("Cost") || t.Name.Contains("Command") || t.Name.Contains("StateStore"))))
    Console.WriteLine("RITSU_TYPE " + type.FullName);
