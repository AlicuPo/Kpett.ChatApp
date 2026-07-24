#!/bin/sh
set -e

mkdir -p /app/wwwroot/uploads/images /app/wwwroot/uploads/videos /app/wwwroot/uploads/posts
chown -R "$APP_UID":0 /app/wwwroot/uploads 2>/dev/null || true

exec setpriv --reuid="$APP_UID" --regid=0 --init-groups dotnet Kpett.ChatApp.dll
