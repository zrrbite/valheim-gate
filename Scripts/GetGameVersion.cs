// Prints the Valheim game version (e.g. "0.221.12") embedded in an
// assembly_valheim.dll, by reading the GameVersion ctor args stored into
// Version.CurrentVersion. Compiled on demand by game_version.sh.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class GetGameVersion
{
    static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("usage: GetGameVersion.exe <assembly_valheim.dll>");
            return 2;
        }

        var asm = AssemblyDefinition.ReadAssembly(args[0]);
        foreach (var method in asm.MainModule.GetTypes().SelectMany(t => t.Methods).Where(m => m.HasBody))
        {
            var ins = method.Body.Instructions;
            for (int i = 0; i < ins.Count; i++)
            {
                var fr = ins[i].Operand as FieldReference;
                if (ins[i].OpCode != OpCodes.Stsfld || fr == null || !fr.Name.Contains("CurrentVersion"))
                    continue;

                // Expect: ldc major, ldc minor, ldc patch, newobj GameVersion, stsfld
                var nums = new System.Collections.Generic.List<int>();
                for (int j = Math.Max(0, i - 8); j < i; j++)
                {
                    int? v = LdcValue(ins[j]);
                    if (v.HasValue) nums.Add(v.Value);
                }
                if (nums.Count >= 3)
                {
                    var n = nums.Count;
                    Console.WriteLine($"{nums[n - 3]}.{nums[n - 2]}.{nums[n - 1]}");
                    return 0;
                }
            }
        }
        Console.Error.WriteLine("version not found");
        return 1;
    }

    static int? LdcValue(Instruction ins)
    {
        if (ins.OpCode == OpCodes.Ldc_I4) return (int)ins.Operand;
        if (ins.OpCode == OpCodes.Ldc_I4_S) return (sbyte)ins.Operand;
        if (ins.OpCode.Code >= Code.Ldc_I4_0 && ins.OpCode.Code <= Code.Ldc_I4_8)
            return ins.OpCode.Code - Code.Ldc_I4_0;
        if (ins.OpCode == OpCodes.Ldc_I4_M1) return -1;
        return null;
    }
}
