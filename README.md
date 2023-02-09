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

Copy `assembly_valheim.dll` and `ICanShowYouTheWorld.dll` to...

- Linux / Steam-deck:  `/home/deck/.local/share/Steam/steamapps`
- Windows:  `Steam\steamapps\common\Valheim\valheim_Data\Managed` 
(The assemblies are cross-platform compliant due to .NET IL and Mono/Proton)

After starting up the game go to the  Credits menu (If  the "Valheim" logo appears during game startup, the `assembly_valheim.dll` is compliant. Otherwise it may be corrupted somehow and you'll need to either repair or replace the patched version with your backup). This registers the patched assemblies / code to be called by the main game loop.

Key-bindings:

- HOME =  "Gate" / Teleport to bind spot.
- INS   = Teleport to where your mouse cursor is while having the map open (obviously difficult on a steam deck). (edited)

