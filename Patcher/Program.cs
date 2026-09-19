using System;
using System.Linq;
using Mono.Cecil;

namespace Patcher
{
    class MainClass
    {
        // -------------------------------------------------------------
        // The only reason we're referencing ICanShowYou... is because we need the symbol names to patch into assembly_valheim.dll
        // The procedure is to start with the patcher if there's been a STEAM PATCH, becausae your assembly_valheim.dll has been overwritten.
        // 1. Scripts/Download.sh
        // 2. mono Patcher.exe
        // 3. Scripts/copy..
        // 4. Rebuild ICanShowYou...
        // 5. Scripts/Copy... (both assemblies to deck)
        static readonly string appPath = @"./assembly_valheim.dll.org";
        static readonly string injPath = @"./ICanShowYouTheWorld.dll"; // todo: You might say that this is a stale reference, but it mostly woirks because it already has the required symbols. Maybe change this.
        static readonly string donePath = @"./patched/assembly_valheim.dll";

        // Stamped into the patched assembly so a caller can tell WHICH entry point it was
        // patched with. Scanning for "NotACheater" only answers "patched at all", which is
        // why a mod-only install could leave a stale injection sitting in the game.
        // THREE things look for this name and all three must agree:
        //   this file, dist/windows/Install-Mod.ps1, Scripts/config.sh (check_injections).
        // Change the entry point and you change the name — that is the point of it.
        const string EntryPointMarker = "ICSYTW_EntryPoint_FejdStartup_Start";

        static void Main(string[] args)
        {
            // Optional overrides: Patcher.exe [input.dll] [output.dll] [mod.dll]
            // The third argument matters when the working directory is not the
            // Patcher's own folder, which is the case for the Windows installer.
            var inPath = args.Length > 0 ? args[0] : appPath;
            var outPath = args.Length > 1 ? args[1] : donePath;
            var symPath = args.Length > 2 ? args[2] : injPath;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(outPath)));

            // Resolve dependencies from the folders the assemblies actually
            // live in. Cecil's default resolver only searches the working
            // directory, which made patching succeed or fail depending on
            // where it was invoked from.
            // A 4th argument names the folder holding the game's other
            // assemblies — needed when the input is a backup kept outside the
            // game directory, where UnityEngine.CoreModule and friends are not
            // sitting next to it.
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(inPath)));
            resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(symPath)));
            if (args.Length > 3) resolver.AddSearchDirectory(System.IO.Path.GetFullPath(args[3]));
            var readParams = new ReaderParameters { AssemblyResolver = resolver };

            var app = AssemblyDefinition.ReadAssembly(inPath, readParams);
            var inj = AssemblyDefinition.ReadAssembly(symPath, readParams);

            // Set scope of m_pins on the minimap to public static
            //
            var fieldPin = app.MainModule.Types.Single(ct => ct.Name == "Minimap")
           .Fields
           .First(f => f.Name == "m_pins");

            // Set field visibliy to public
            fieldPin.IsPrivate = false;
            fieldPin.IsPublic = true;
            fieldPin.IsStatic = true;
            //

            var injType = inj.MainModule.Types.Single(t => t.Name == "NotACheater");
            var injMethod = injType.Methods.Single(m => m.Name == "Run");
            var appType = app.MainModule.Types.Single(t => t.Name == "FejdStartup");

            // Run() is called from Start, not only from OnCredits, because a saga item is
            // only known to the game while the mod is loaded: load a character without
            // visiting Credits and Thor's bow is an unresolved name, dropped from the pack,
            // and the next save writes it gone. Start rather than Awake because every
            // Awake in the menu scene has run by then — including UnifiedPopup's, which
            // owns the version popup. OnCredits is kept: the second call is a no-op and it
            // costs one instruction to leave the old door in the wall.
            var startMethod = InjectEntryCall(app, appType, "Start", injMethod);
            var creditsMethod = InjectEntryCall(app, appType, "OnCredits", injMethod);

            // Second injection: Character.OnDeath -> GameEvents.CharacterDied(this)
            var charType = app.MainModule.Types.Single(t => t.Name == "Character");
            var onDeath = charType.Methods.Single(m => m.Name == "OnDeath" && !m.HasParameters);
            var evType = inj.MainModule.Types.Single(t => t.Name == "GameEvents");
            var evMethod = evType.Methods.Single(m => m.Name == "CharacterDied");

            var deathIl = onDeath.Body.GetILProcessor();
            var deathFirst = deathIl.Body.Instructions[0];
            Console.Write("Patching {0}->{1}.. ", charType.Name, onDeath.Name);
            deathIl.InsertBefore(deathFirst, deathIl.Create(Mono.Cecil.Cil.OpCodes.Ldarg_0));
            deathIl.InsertBefore(deathFirst, deathIl.Create(Mono.Cecil.Cil.OpCodes.Call,
                app.MainModule.ImportReference(evMethod.Resolve())));
            Console.WriteLine("done");

            StampEntryPointMarker(app);

            Dump(startMethod);
            Dump(creditsMethod);

            Console.WriteLine("Writing patched library to {0}", outPath);
            app.Write(outPath);

            Console.WriteLine("Have fun!");
        }

        /// <summary>
        /// Insert a parameterless static call at the very start of one of the game's methods.
        /// Index 0 is the true method entry for all of our targets — no prologue, nothing
        /// branching to it — and the lookup is .Single so a renamed method throws here,
        /// while the patcher is watching, rather than producing a quietly dead mod.
        /// </summary>
        static MethodDefinition InjectEntryCall(AssemblyDefinition app, TypeDefinition appType,
            string methodName, MethodDefinition injMethod)
        {
            var appMethod = appType.Methods.Single(m => m.Name == methodName && !m.HasParameters);
            var ipl = appMethod.Body.GetILProcessor();

            Console.Write("Patching {0}->{1}.. ", appType.Name, appMethod.Name);
            ipl.InsertBefore(ipl.Body.Instructions[0],
                ipl.Create(Mono.Cecil.Cil.OpCodes.Call, app.MainModule.ImportReference(injMethod.Resolve())));
            Console.WriteLine("done");

            return appMethod;
        }

        /// <summary>
        /// Add an empty type whose NAME is the whole payload. Deliberately bare: no members,
        /// no attributes, no reference to anything outside the module, so there is nothing
        /// for the game's own reflection to trip over and nothing new to resolve at load.
        /// </summary>
        static void StampEntryPointMarker(AssemblyDefinition app)
        {
            if (app.MainModule.Types.Any(t => t.Name == EntryPointMarker)) return;

            Console.Write("Stamping {0}.. ", EntryPointMarker);
            app.MainModule.Types.Add(new TypeDefinition(
                "", EntryPointMarker,
                TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Abstract,
                app.MainModule.TypeSystem.Object));
            Console.WriteLine("done");
        }

        static void Dump(MethodDefinition method)
        {
            Console.WriteLine("\n{0} instructions:\n", method.Name);
            foreach (var instruction in method.Body.Instructions)
                Console.WriteLine($"\t{instruction.Offset:X2}: {instruction.OpCode} \"{instruction.Operand}\"");
        }
    }
}