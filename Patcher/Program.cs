﻿using System;
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
            var app = AssemblyDefinition.ReadAssembly(appPath);
            var inj = AssemblyDefinition.ReadAssembly(injPath);

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
            Console.WriteLine("Writing patched library to {0}", donePath);
            app.Write(donePath);

            Console.WriteLine("Have fun!");
            Environment.Exit(1);
        }
    }
}