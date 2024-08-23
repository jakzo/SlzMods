#!/bin/bash
set -eu
cd "$(dirname $0)/.."

NAME="$1"
VALUE="$2"

VALUE_ESCAPED=$(printf '%s\n' "$VALUE" | sed -e 's/[&/\]/\\&/g')

FILENAME="MelonPreferences.cfg"
PREFS_PATH="/sdcard/Android/data/com.StressLevelZero.BONELAB/files/UserData/$FILENAME"
CATEGORY="HandTracking"
LINE_TO_INSERT="$NAME = $VALUE_ESCAPED"

adb pull "$PREFS_PATH"

if grep -q "^$NAME = .*\$" "$FILENAME"; then
  sed -i '' "s/^$NAME = .*\$/$LINE_TO_INSERT/" "$FILENAME"
elif grep -q "^\[$CATEGORY\]$" "$FILENAME"; then
  sed -i '' -e "/^\[$CATEGORY\]$/a\\
$LINE_TO_INSERT" "$FILENAME"
else
  echo "Error: No category with name \"$CATEGORY\" found in $FILENAME"
  exit 1
fi

adb shell run-as com.StressLevelZero.BONELAB sh -c "
cat > '$PREFS_PATH' <<'EOF'
$(cat "$FILENAME")
EOF
"
rm "$FILENAME"

./scripts/build-and-start.sh
