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

# Sem isto, um dump vazio ou truncado (disco cheio, permissão, pg_dump
# interrompido) passava despercebido: o arquivo existia, a rotina de
# retenção seguia normalmente, e só se notaria numa restauração real.
if [ ! -s "$backup_file" ]; then
    echo "buildpc-backup: $backup_file ficou vazio ou não foi criado." >&2
    exit 1
fi

/usr/bin/find "$backup_directory" \
    -maxdepth 1 \
    -type f \
    -name 'buildpc-*.dump' \
    -mtime +14 \
    -delete
