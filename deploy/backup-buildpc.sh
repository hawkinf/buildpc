#!/usr/bin/env bash
set -euo pipefail

backup_directory=/var/backups/buildpc
case "$backup_directory" in
    /var/backups/buildpc) ;;
    *) exit 1 ;;
esac

umask 077
backup_file="${backup_directory}/buildpc-$(date -u +%Y%m%d-%H%M%S).dump"
/usr/bin/pg_dump --format=custom --file="$backup_file" buildpc
/usr/bin/find "$backup_directory" \
    -maxdepth 1 \
    -type f \
    -name 'buildpc-*.dump' \
    -mtime +14 \
    -delete
