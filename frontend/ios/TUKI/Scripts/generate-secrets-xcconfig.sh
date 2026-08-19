#!/bin/bash
set -e

FRONTEND_ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
DEFAULTS_PROPERTIES="$FRONTEND_ROOT/local.defaults.properties"
LOCAL_PROPERTIES="$FRONTEND_ROOT/local.properties"
OUTPUT="$FRONTEND_ROOT/ios/TUKI/Config/Secrets.generated.xcconfig"

mkdir -p "$(dirname "$OUTPUT")"

if [ ! -f "$LOCAL_PROPERTIES" ]; then
  echo "error: local.properties not found at $LOCAL_PROPERTIES"
  exit 1
fi

# Reads a property from local.properties, falling back to local.defaults.properties,
# mirroring the cascade in app/build.gradle.kts so Android and iOS stay in sync.
get_property() {
  local value
  value="$(grep "^$1=" "$LOCAL_PROPERTIES" 2>/dev/null | head -n 1 | cut -d'=' -f2-)"
  if [ -z "$value" ] && [ -f "$DEFAULTS_PROPERTIES" ]; then
    value="$(grep "^$1=" "$DEFAULTS_PROPERTIES" 2>/dev/null | head -n 1 | cut -d'=' -f2-)"
  fi
  echo "$value"
}

# xcconfig treats an unescaped "//" as the start of a comment, which would truncate a
# URL like http://host at the colon. Splitting the slashes with an empty macro
# reference ($()) keeps the literal text intact while resolving to "//" once Xcode
# expands the setting.
escape_xcconfig_url() {
  printf '%s' "$1" | sed 's#//#/$()/#g'
}

FACEBOOK_APP_ID="$(get_property FACEBOOK_APP_ID)"
FACEBOOK_CLIENT_TOKEN="$(get_property FACEBOOK_CLIENT_TOKEN)"
GOOGLE_IOS_CLIENT_ID="$(get_property GOOGLE_IOS_CLIENT_ID)"
GOOGLE_SERVER_CLIENT_ID="$(get_property GOOGLE_SERVER_CLIENT_ID)"
GOOGLE_REVERSED_CLIENT_ID="$(get_property GOOGLE_REVERSED_CLIENT_ID)"
BACKEND_BASE_URL="$(get_property BACKEND_BASE_URL)"

if [ -z "$BACKEND_BASE_URL" ]; then
  echo "error: BACKEND_BASE_URL missing from both local.properties and local.defaults.properties"
  exit 1
fi

if [ -z "$FACEBOOK_APP_ID" ]; then
  echo "error: FACEBOOK_APP_ID missing from local.properties"
  exit 1
fi

if [ -z "$FACEBOOK_CLIENT_TOKEN" ]; then
  echo "error: FACEBOOK_CLIENT_TOKEN missing from local.properties"
  exit 1
fi

cat > "$OUTPUT" <<EOF
FACEBOOK_APP_ID = $FACEBOOK_APP_ID
FACEBOOK_CLIENT_TOKEN = $FACEBOOK_CLIENT_TOKEN
FACEBOOK_URL_SCHEME = fb$FACEBOOK_APP_ID
GOOGLE_IOS_CLIENT_ID = $GOOGLE_IOS_CLIENT_ID
GOOGLE_SERVER_CLIENT_ID = $GOOGLE_SERVER_CLIENT_ID
GOOGLE_REVERSED_CLIENT_ID = $GOOGLE_REVERSED_CLIENT_ID
BACKEND_BASE_URL = $(escape_xcconfig_url "$BACKEND_BASE_URL")
EOF

echo "Generated Secrets.generated.xcconfig"
