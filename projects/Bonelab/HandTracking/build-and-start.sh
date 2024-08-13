set -eux
cd "$(dirname $0)"

dotnet build ./Project.csproj

# TIP: Enable wireless mode from SideQuest
# adb connect 192.168.0.69

FILE=HandTracking.P4.ML5.dll
adb shell am force-stop com.StressLevelZero.BONELAB
adb push "./bin/Debug/Patch4_MelonLoader0.5/$FILE" /sdcard/Download/
adb shell mv "/sdcard/Download/$FILE" /sdcard/Android/data/com.StressLevelZero.BONELAB/files/mods/
adb shell chmod 644 "/sdcard/Android/data/com.StressLevelZero.BONELAB/files/mods/$FILE"
adb shell am start -n com.StressLevelZero.BONELAB/com.unity3d.player.UnityPlayerActivity

# adb shell uiautomator dump /sdcard/ui_dump.xml && adb pull /sdcard/ui_dump.xml .

# adb shell ls /sdcard/Android/data/com.StressLevelZero.BONELAB/files/melonloader
# adb pull /sdcard/Android/data/com.StressLevelZero.BONELAB/files/melonloader/etc/managed/Assembly-CSharp.dll
# adb pull /sdcard/Android/data/com.StressLevelZero.BONELAB/files/melonloader/etc/managed/SLZ.Marrow.dll

# adb logcat -v time MelonLoader:D CRASH:D Mono:W mono:D mono-rt:D Zygote:D A64_HOOK:V DEBUG:D Binder:D AndroidRuntime:D "*:S"
