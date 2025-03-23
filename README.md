# valheim-gate
In Valheim, teleport to your bind spot or anywhere on the map. Binaries are cross-platform.

# TODO
* Better overview of various update scenarios
* Scripts to automate various scenarios
  * Standard update: dll fetch from deck, rebuild, dll push
  * Auto-detect unity version change
  * More...

# Set up SSH on steamdeck
https://shendrick.net/Gaming/2022/05/30/sshonsteamdeck.html

* Open Terminal on Deck
* `passwd` to set a password
* `sudo systemctl start sshd`
* `Then: sudo systemctl enable sshd`

# When and what to update

_Simple scenario:_
* New release of Valheim is updated through Steam, updating assembly_valheim.dll, overriding your patched version, requiring a re-patch. Sometimes it's a simple patch that can be done by a) copying `assembly_valheim.dll` and running it through `Patcher.exe`, b) Uploading the dll to steamdeck, leaving ICanShowYouTheWorld.dll in place.
* New feature of ICanShowYouTheWorld.dll only requires a rebuild and upload of dll.

**Perform these steps:**
1. Patcher/Scripts/download.sh
2. Mono Patcher.exe
3. Patcher/Scripts/copy.sh (copies to libraries)
4. Rebuild hax against new valheim dll
5. Scripts/upload_hax.sh
6. Scripts/upload_valheim.sh

_More complicated scenarios:_
* New release of Unity causes ICanShowYouTheWorld project to be relinked and possibly changed if breaking changes (check here if version was updated: https://valheim.fandom.com/wiki/Version_History)
* New feature in ICanShowYouTheWorld project needs a modified version of assembly_valheim.dll (e.g. due to `m_pins`)

_TL;DR_ 
* Complete all steps, no matter the scenario.
  * Check patchnotes for new unity version. e.g. valheim 0.219.13 introduced uinity 2022.3.50.
  * From deck, download valheim assembly and UnityEngine.UI.dll to the libraries folder. From bespinx, download new unity bianries and extract those in the 'binaries' folder to allow proper linking (these do not need to go to deck).
  * Remember to patch assembly_valheim.dll so that it actually calls Icanshowyoutheworld

## Commands
* msvs cli build: e.g. $/Users/martinkjeldsen/Projects/Valheim 
`/Applications/"Visual Studio".app/Contents/MacOS/vstool build -t:Build -c:"Debug" "Valheim.sln";`

## Patcher
     
  ```
   ➜  mono Patcher.exe
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

# Working with Unity

In order to work with Unity you need the unstripped Unity assemblies.

Find the version you need by inspecting the Valheim installation folder:

```
In folder: /home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data

(deck@steamdeck valheim_Data)$ strings globalgamemanagers | head -n1
2020.3.33f1
```

Get the assemblies here. e.g.:

https://unity.bepinex.dev/corlibs/2020.3.33.zip
https://unity.bepinex.dev/libraries/2020.3.33.zip

Extract both into the same folder. They will contain the same files as in `/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed` just unstripped!

Copy the libraries you extracted to the folder you currently have the Valheim libraries in, overwrite — restart Visual Studio so it’ll reload them. Now you should be able to use GUI. functions in VS.

Copy the libraries you extracted to the Deck in `/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data` where you also copy your ICanShowYouTheWorld DLL, overwrite existing.

## Examples

https://docs.unity3d.com/ScriptReference/GUI.Window.html

Some Simple GUI extension of `DiscoverThings` class for reference:

```
public class DiscoverThings : MonoBehaviour
{
	private Rect MainWindow;

	public bool visible = true;

	private void Start()
	{
		MainWindow = new Rect(10f, 10f, 250f, 150f);
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Backspace))
		{
			Console.instance.Print("Gui!");
			visible = !visible;
		}
	}

	private void OnGUI()
	{
		GUI.Label(new Rect(20, 20, 200, 60), "New label, haxor - always active as it's above the return statement below");

		if (!visible)
			return;

		MainWindow = GUILayout.Window(0, MainWindow, new GUI.WindowFunction(RenderUI), "Haxor Menu", new GUILayoutOption[0]);
	}

	public void RenderUI(int id)
	{
		GUILayout.Box("Some box?");
		GUILayout.Label("Some inner label", new GUILayoutOption[0]);

		if (GUILayout.Button("Menu", new GUILayoutOption[0]))
			visible = !visible;

		if (GUILayout.Button("Test button"))
			Console.instance.Print("Test!!");

		GUILayout.Space(20f);
		GUILayout.Label("Slide it?", new GUILayoutOption[0]);
		int testSlider = (int)GUILayout.HorizontalSlider(10, 40, 120);

		GUI.DragWindow();
	}
}
```

Other examples:

https://github.com/Astropilot/ValheimTooler/blob/c48078ecc72fbddffd7398a37841f811020e78cb/ValheimTooler/Core/ESP.cs#L259

Other functions to try:

```
Vector3 playerPos = Player.m_localPlayer.transform.position;

foreach (Character toon in Character.GetAllCharacters()) {
  float dist = Vector3.Distance(playerPos, toon.transform.position);
  if (dist >= somenumber && !toon.isDead()) {
     Label txt color: GUI.color = Color.red;
     A label: GUILayout.Label("Name: " + toon.GetHoverName());
     Health min/max: Mathf.RoundToInt(toon.GetHealth()) .. / .. toon.GetMaxHealth()
     Enemy level: toon.GetLevel()
     Distance to enemy: Mathf.RoundToInt(dist)
  }
}
```

# Key-bindings

- `HOME` =  "Gate" / Teleport to bind spot.
- `INS`   = Teleport to where your mouse cursor is while having the map open (obviously difficult on a steam deck). (edited)

<screenshots>
