#!/bin/bash
set -e

FRONTEND_ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
LOCAL_PROPERTIES="$FRONTEND_ROOT/local.properties"
OUTPUT="$FRONTEND_ROOT/ios/TUKI/Config/Secrets.generated.xcconfig"

mkdir -p "$(dirname "$OUTPUT")"

if [ ! -f "$LOCAL_PROPERTIES" ]; then
  echo "error: local.properties not found at $LOCAL_PROPERTIES"
  exit 1
fi

get_property() {
  grep "^$1=" "$LOCAL_PROPERTIES" | head -n 1 | cut -d'=' -f2-
}

FACEBOOK_APP_ID="$(get_property FACEBOOK_APP_ID)"
FACEBOOK_CLIENT_TOKEN="$(get_property FACEBOOK_CLIENT_TOKEN)"
GOOGLE_IOS_CLIENT_ID="$(get_property GOOGLE_IOS_CLIENT_ID)"
GOOGLE_SERVER_CLIENT_ID="$(get_property GOOGLE_SERVER_CLIENT_ID)"
GOOGLE_REVERSED_CLIENT_ID="$(get_property GOOGLE_REVERSED_CLIENT_ID)"

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
EOF

echo "Generated Secrets.generated.xcconfig"
