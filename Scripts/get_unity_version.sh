## Write current version of unity in a version file
#
# After an update on deck check its version:
# strings globalgamemanagers | head -n1 in /home/deck/.local/share/Steam/steamapps/common/Valheim/valheim_Data
# => 2020.3.33f1 (what does the f1 mean?)
#
# If its different (should always be later version) ->
#
# Now use that output to download corlibs and libraries from:
# - https://unity.bepinex.dev/corlibs/2020.3.33.zip
# - https://unity.bepinex.dev/libraries/2020.3.33.zip
#
# Now extract those to /libraries
