#####################################
# Downlods, patches- and uploads main game assembly again
# - This is almost always something you'd want to do, unless the patcher needs to change, which is raare.
# - This leaves only the hax binary to be recompiled against the new game bin and maybe unity. 
#      - Then upload manually.
##########################################

# Download important binaries

## Main game
scp deck@192.168.86.42:/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll ../libraries  
## Unity things
scp deck@192.168.86.42:/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/UnityEngine.UI.dll ../libraries  

# Patch main assembly and push it back
cp ../libraries/assembly_valheim.dll ../Patcher/bin/Debug/assembly_valheim.dll.org
cd ../Patcher/bin/Debug/
mono Patcher.exe

# ready for upload
scp patched/assembly_valheim.dll deck@192.168.86.42:/home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/

