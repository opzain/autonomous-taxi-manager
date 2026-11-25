#!/usr/bin/env bash
# Verifies the project WITHOUT Unity installed (Linux / WSL / macOS with dotnet, python3, curl, glslang):
#   1. simulation-core unit tests (real .NET)
#   2. every Unity C# script type-checked against real UnityEngine/UnityEditor reference assemblies
#      (both "new Input System" and "legacy input" code paths)
#   4. every procedural sound rendered and checked (no NaN / silence / clipping)
#   5. the ESP32 console firmware run on the PC against the game's real protocol stream
#   3. every shader pass compiled (vertex + fragment, basic + full-feature variants) against
#      Unity 6's real URP shader library using glslang's HLSL front-end
# Requirements: dotnet 8 SDK, python3, curl, glslang-tools (apt install glslang-tools).
set -euo pipefail
cd "$(dirname "$0")"
CACHE=.cache; mkdir -p "$CACHE"
fail=0

echo "== 1. Simulation core tests"
dotnet test ../../sim-tests --nologo -v q || fail=1

echo "== 2. Unity C# type-check"
if [ ! -d "$CACHE/unityengine" ]; then
  curl -sSL -o "$CACHE/ue.nupkg" https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  python3 -c "import zipfile; zipfile.ZipFile('$CACHE/ue.nupkg').extractall('$CACHE/unityengine')"
fi
if [ ! -f "$CACHE/unitysdk/lib/UnityEditor.dll" ]; then
  curl -sSL -o "$CACHE/sdk.nupkg" https://api.nuget.org/v3-flatcontainer/unity3d.sdk/2021.1.14.1/unity3d.sdk.2021.1.14.1.nupkg
  python3 -c "import zipfile; zipfile.ZipFile('$CACHE/sdk.nupkg').extract('lib/UnityEditor.dll','$CACHE/unitysdk')"
fi
for D in ENABLE_INPUT_SYSTEM ENABLE_LEGACY_INPUT_MANAGER; do
  echo "   -- runtime scripts with $D"
  dotnet build csharp/World.csproj --no-incremental -nologo -v q -p:ExtraDefines=$D || fail=1
done
echo "   -- USB console link (.NET Framework API level: NET_4_6 + System.IO.Ports)"
dotnet build csharp/Serial.csproj --no-incremental -nologo -v q || fail=1
echo "   -- editor scripts"
dotnet build csharp/Editor.csproj --no-incremental -nologo -v q || fail=1

echo "== 3. Shader compile (glslang + Unity 6 URP library)"
if [ ! -d "$CACHE/graphics" ]; then
  python3 shaders/fetch_urp_library.py
  python3 shaders/patch_for_glslang.py
fi
python3 shaders/compile_shaders.py || fail=1

echo "== 4. Procedural city sounds (synthesised outside Unity; WAVs in .cache/audio)"
dotnet run --project audio/AudioCheck.csproj -c Release -- "$CACHE/audio" || fail=1

echo "== 5. ESP32 console firmware runs the game's real line stream (PC harness, ASan/UBSan)"
dotnet run --project protocol/ProtoDump.csproj -c Release -- "$CACHE/protocol_stream.txt" || fail=1
python3 firmware/arduino_prototypes.py ../../firmware/wokwi/sketch.ino "$CACHE/sketch_ide.cpp" && g++ -std=gnu++17 -fsyntax-only -I firmware/host -x c++ "$CACHE/sketch_ide.cpp" || fail=1
g++ -std=gnu++17 -Wall -Wextra -fsanitize=address,undefined -g -DBUZZER_ACTIVE=0 -I firmware/host -o "$CACHE/fw_harness" firmware/harness.cpp || fail=1
"$CACHE/fw_harness" "$CACHE/protocol_stream.txt" | grep -E "PASS|FAIL|HARNESS" || fail=1

if [ $fail -eq 0 ]; then echo "ALL CHECKS PASSED"; else echo "SOME CHECKS FAILED"; fi
exit $fail
