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
            var appMethod = appType.Methods.Single(m => m.Name == "OnCredits");

            var ipl = appMethod.Body.GetILProcessor();
            var firstInstruction = ipl.Body.Instructions[0];
            var injectInstruction = ipl.Create(Mono.Cecil.Cil.OpCodes.Call, app.MainModule.ImportReference(injMethod.Resolve()));

            Console.Write("Patching {0}->{1}.. ", appType.Name, appMethod.Name);
            ipl.InsertBefore(firstInstruction, injectInstruction);
            Console.WriteLine("done\n");

            Console.WriteLine("Instructions:\n");
            foreach (var instruction in appMethod.Body.Instructions)
                Console.WriteLine($"\t{instruction.Offset:X2}: {instruction.OpCode} \"{instruction.Operand}\"");

            Console.WriteLine("\n");
            Console.WriteLine("Writing patched library to {0}", outPath);
            app.Write(outPath);

            Console.WriteLine("Have fun!");
        }
    }
}