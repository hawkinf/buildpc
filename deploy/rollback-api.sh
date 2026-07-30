#!/usr/bin/env bash
set -euo pipefail

# Uso: rollback-api.sh
#
# Antes, um rollback exigia ler ROLLBACK.txt a olho, reapontar o symlink à
# mão e reiniciar o serviço — ~4 passos manuais por SSH. Este script reduz
# isso a um comando, usando o mesmo arquivo que deploy-api.sh já grava.

base_dir=/opt/buildpc-api
current_link="$base_dir/current"
rollback_file="$base_dir/ROLLBACK.txt"
health_url="https://contaslite.hawk.com.br/buildpc-api/health"

if [ ! -f "$rollback_file" ]; then
    echo "rollback-api: $rollback_file não existe — nada para reverter." >&2
    exit 1
fi

previous_target="$(cat "$rollback_file")"
if [ ! -d "$previous_target" ]; then
    echo "rollback-api: $previous_target (registado em $rollback_file) não existe mais." >&2
    exit 1
fi

echo "rollback-api: revertendo 'current' para $previous_target..."
ln -sfn "$previous_target" "$current_link"
systemctl restart buildpc-api.service
sleep 2

if ! curl -fsS "$health_url" >/dev/null 2>&1; then
    echo "rollback-api: $health_url ainda não responde após o rollback." \
        "Intervenção manual necessária — verifique 'journalctl -u buildpc-api -n 50'." >&2
    exit 1
fi

echo "rollback-api: revertido para $previous_target e respondendo em produção."
