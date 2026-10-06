// Lists every type and member the mod DLL references in the game's assemblies that no longer resolves
// against a given Managed folder. Run it through Scripts/check_refs.sh after a game update, before compiling.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

static class CheckRefs
{
    static int Main(string[] args)
    {
        var resolver = new DefaultAssemblyResolver();
        foreach (var d in resolver.GetSearchDirectories()) resolver.RemoveSearchDirectory(d);
        resolver.AddSearchDirectory(args[1]);
        var mod = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver });

        // The game's side is every assembly the mod references except the .NET framework. Decided from the
        // mod's own reference list, so an assembly missing from the folder counts as broken, not skipped.
        Func<string, bool> framework = n => n == "mscorlib" || n == "netstandard" || n == "System" || n.StartsWith("System.");
        var game = new HashSet<string>(mod.AssemblyReferences.Select(r => r.Name).Where(n => !framework(n)),
                                       StringComparer.OrdinalIgnoreCase);
        Func<IMetadataScope, bool> fromGame = s => s != null && game.Contains(s.Name.Replace(".dll", ""));
        Console.WriteLine("Game assemblies referenced: " + string.Join(", ", game.OrderBy(n => n)));

        int types = 0, members = 0;
        var broken = new List<string>();
        foreach (var t in mod.GetTypeReferences().Where(t => fromGame(t.Scope)))
        {
            types++;
            TypeDefinition def = null;
            try { def = t.Resolve(); } catch { }
            if (def == null) broken.Add("TYPE   " + t.FullName + "  [" + t.Scope.Name + "]");
        }
        foreach (var m in mod.GetMemberReferences())
        {
            var dt = m.DeclaringType;
            while (dt is TypeSpecification spec) dt = spec.ElementType;
            if (dt == null || !fromGame(dt.Scope)) continue;
            members++;
            IMemberDefinition def = null;
            try { def = m.Resolve(); } catch { }
            if (def == null) broken.Add("MEMBER " + m.FullName + "  [" + dt.Scope.Name + "]");
        }
        Console.WriteLine($"{types} type and {members} member references into the game; {broken.Count} unresolved.");
        foreach (var b in broken.Distinct().OrderBy(x => x)) Console.WriteLine("  " + b);
        return broken.Count == 0 ? 0 : 1;
    }
}
