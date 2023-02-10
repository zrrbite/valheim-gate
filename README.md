# valheim-gate
In Valheim, teleport to your bind spot or anywhere on the map. Binaries are cross-platform.

# Set up SSH on steamdeck
https://shendrick.net/Gaming/2022/05/30/sshonsteamdeck.html

* Open Terminal on Deck
* `passwd` to set a password
* `sudo systemctl start sshd`
* `Then: sudo systemctl enable sshd`

# Usage

There are two usage scenarios.
1. Game is updated through Steam, updating assembly_valheim.dll, overriding your patched version, requiring a re-patch
2. ICanShowYouTheWorld.dll requires an update based on new release

<more here>
  
## Patcher
<Create scripts to do all this automatically. Create Patched folder. setup.sh>
  1.
  
TODO:  List of files in common folder, but this should really be done using scripts or something packaged up in a release?
  
  ```
  total 4328
-rw-r--r--  1 martinkjeldsen  staff     0B Feb  7 20:02 0.213.4
-rwxr-xr-x@ 1 martinkjeldsen  staff    23K Feb  4 17:07 Assembly-CSharp.dll
-rw-r--r--@ 1 martinkjeldsen  staff   7.0K Feb  7 15:17 ICanShowYouTheWorld.dll
-rw-r--r--@ 1 martinkjeldsen  staff   3.8K Feb  7 15:17 ICanShowYouTheWorld.pdb
-rwxr--r--@ 1 martinkjeldsen  staff    42K Jul  1  2021 Mono.Cecil.Mdb.dll
-rwxr--r--@ 1 martinkjeldsen  staff    19K Jul  1  2021 Mono.Cecil.Mdb.pdb
-rwxr--r--@ 1 martinkjeldsen  staff    88K Jul  1  2021 Mono.Cecil.Pdb.dll
-rwxr--r--@ 1 martinkjeldsen  staff    26K Jul  1  2021 Mono.Cecil.Pdb.pdb
-rwxr--r--@ 1 martinkjeldsen  staff    27K Jul  1  2021 Mono.Cecil.Rocks.dll
-rwxr--r--@ 1 martinkjeldsen  staff   8.7K Jul  1  2021 Mono.Cecil.Rocks.pdb
-rwxr--r--@ 1 martinkjeldsen  staff   350K Jul  1  2021 Mono.Cecil.dll
-rwxr--r--@ 1 martinkjeldsen  staff   180K Jul  1  2021 Mono.Cecil.pdb
-rwxr-xr-x@ 1 martinkjeldsen  staff   7.0K Feb  7 12:42 Patcher.exe
-rw-r--r--@ 1 martinkjeldsen  staff   1.8K Feb  7 12:42 Patcher.pdb
-rwxr-xr-x@ 1 martinkjeldsen  staff    47K Feb  4 17:07 assembly_utils.dll
-rwxr-xr-x  1 martinkjeldsen  staff   1.3M Feb  7 16:09 assembly_valheim.dll.org
drwxr-xr-x  2 martinkjeldsen  staff    64B Feb  7 11:01 deck_backup
drwxr-xr-x  4 martinkjeldsen  staff   128B Feb  7 20:00 patched
```
  

  ```
   ➜  valheim_patcher mono Patcher.exe
Patching FejdStartup->OnCredits.. done

Instructions:

	00: call "System.Void ICanShowYouTheWorld.NotACheater::Run()"
	00: ldarg.0 ""
	01: ldfld "UnityEngine.GameObject FejdStartup::m_creditsPanel"
	06: ldc.i4.1 ""
	07: callvirt "System.Void UnityEngine.GameObject::SetActive(System.Boolean)"
	0C: ldarg.0 ""
	0D: ldfld "UnityEngine.GameObject FejdStartup::m_mainMenu"
	12: ldc.i4.0 ""
	13: callvirt "System.Void UnityEngine.GameObject::SetActive(System.Boolean)"
	18: ldstr "Screen"
	1D: ldstr "Enter"
	22: ldstr "Credits"
	27: ldc.i4.0 ""
	28: conv.i8 ""
	29: call "System.Void Gogan::LogEvent(System.String,System.String,System.String,System.Int64)"
	2E: ldarg.0 ""
	2F: ldfld "UnityEngine.RectTransform FejdStartup::m_creditsList"
	34: ldc.r4 "0"
	39: ldc.r4 "0"
	3E: newobj "System.Void UnityEngine.Vector2::.ctor(System.Single,System.Single)"
	43: callvirt "System.Void UnityEngine.RectTransform::set_anchoredPosition(UnityEngine.Vector2)"
	48: ret ""


Writing patched library to ./patched/assembly_valheim.dll`
```
   
Copy `assembly_valheim.dll` and `ICanShowYouTheWorld.dll` to...

- Linux / Steam-deck:  `/home/deck/.local/share/Steam/steamapps`
- Windows:  `Steam\steamapps\common\Valheim\valheim_Data\Managed` 
(The assemblies are cross-platform compliant due to .NET IL and Mono/Proton)

After starting up the game go to the  Credits menu (If  the "Valheim" logo appears during game startup, the `assembly_valheim.dll` is compliant. Otherwise it may be corrupted somehow and you'll need to either repair or replace the patched version with your backup). This registers the patched assemblies / code to be called by the main game loop.

Through SSH, example:
```
➜  valheim_patcher scp /Users/martinkjeldsen/Development/valheim_patcher/patched/assembly_valheim.dll deck@192.168.0.160:/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/
deck@192.168.0.160's password: 
assembly_valheim.dll                                                                  100% 1314KB  19.1MB/s   00:00    
➜  valheim_patcher cd ~/Projects/Valheim/ICanShowYouTheWorld/bin/Debug 
➜  Debug git:(main) ✗ scp ICanShowYouTheWorld.dll deck@192.168.0.160:/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/                                          
deck@192.168.0.160's password: 
ICanShowYouTheWorld.dll       
```

# Key-bindings

- `HOME` =  "Gate" / Teleport to bind spot.
- `INS`   = Teleport to where your mouse cursor is while having the map open (obviously difficult on a steam deck). (edited)

<screenshots>
