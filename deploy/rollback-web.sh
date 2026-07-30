#!/usr/bin/env bash
set -euo pipefail

# Uso: rollback-web.sh
#
# Mesmo mecanismo de rollback-api.sh: reaponta "current" para o release
# anterior gravado por deploy-web.sh e reinicia o serviço.

base_dir=/opt/buildpc-web
current_link="$base_dir/current"
rollback_file="$base_dir/ROLLBACK.txt"
health_url="https://precos.hawk.com.br/health"

if [ ! -f "$rollback_file" ]; then
    echo "rollback-web: $rollback_file não existe — nada para reverter." >&2
    exit 1
fi

previous_target="$(sed -E 's/^[A-Z_]+=//' "$rollback_file" | head -n1)"
if [ ! -d "$previous_target" ]; then
    echo "rollback-web: $previous_target (registado em $rollback_file) não existe mais." >&2
    exit 1
fi

echo "rollback-web: revertendo 'current' para $previous_target..."
ln -sfn "$previous_target" "$current_link"
systemctl restart buildpc-web.service
sleep 2

if ! curl -fsS "$health_url" >/dev/null 2>&1; then
    echo "rollback-web: $health_url ainda não responde após o rollback." \
        "Intervenção manual necessária — verifique 'journalctl -u buildpc-web -n 50'." >&2
    exit 1
fi

echo "rollback-web: revertido para $previous_target e respondendo em produção."
