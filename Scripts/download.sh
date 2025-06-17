#scp deck@192.168.86.42:/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/ICanShowYouTheWorld.dll .  
# download valheim assemblies
scp deck@192.168.86.42:/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll ../libraries  
scp deck@192.168.86.42:/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/UnityEngine.UI.dll ../libraries  

#patch
cp ../libraries/assembly_valheim.dll ../Patcher/bin/Debug/assembly_valheim.dll.org
cd ../Patcher/bin/Debug/
mono Patcher.exe

# ready for upload in /patched/