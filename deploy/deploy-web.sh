#!/usr/bin/env bash
set -euo pipefail

# Uso: deploy-web.sh /opt/buildpc-web/releases/<nome-do-release>
#
# Mesmo mecanismo de deploy-api.sh: só troca "current" depois do release novo
# responder /health numa porta de teste. Se não responder, aborta sem tocar
# em produção.
#
# Pré-requisito: o release já publicado (self-contained linux-x64) e
# copiado para o diretório informado, por exemplo:
#   dotnet publish src/BuildPc.Web/BuildPc.Web.csproj -c Release -r linux-x64 \
#     --self-contained true -o /tmp/release-local
#   rsync -a /tmp/release-local/ vps:/opt/buildpc-web/releases/<nome>/

release_path="${1:?Uso: deploy-web.sh /opt/buildpc-web/releases/<nome>}"
release_name="$(basename "$release_path")"
base_dir=/opt/buildpc-web
current_link="$base_dir/current"
rollback_file="$base_dir/ROLLBACK.txt"
env_file=/etc/buildpc-web.env
test_port=8130
health_timeout_seconds=30
health_url="https://precos.hawk.com.br/health"

if [ ! -d "$release_path" ] || [ ! -x "$release_path/BuildPc.Web" ]; then
    echo "deploy-web: $release_path não existe ou não tem o binário BuildPc.Web." >&2
    exit 1
fi

echo "deploy-web: testando $release_name na porta $test_port antes de trocar 'current'..."
set -a
# shellcheck disable=SC1090
source "$env_file"
set +a
export ASPNETCORE_URLS="http://127.0.0.1:$test_port"
"$release_path/BuildPc.Web" &
test_pid=$!
trap 'kill "$test_pid" 2>/dev/null || true' EXIT

healthy=false
for _ in $(seq 1 "$health_timeout_seconds"); do
    if curl -fsS "http://127.0.0.1:$test_port/health" >/dev/null 2>&1; then
        healthy=true
        break
    fi
    sleep 1
done

kill "$test_pid" 2>/dev/null || true
trap - EXIT
wait "$test_pid" 2>/dev/null || true

if [ "$healthy" != true ]; then
    echo "deploy-web: $release_name não respondeu /health em ${health_timeout_seconds}s." \
        "Symlink NÃO foi trocado." >&2
    exit 1
fi

previous_target=""
if [ -L "$current_link" ]; then
    previous_target="$(readlink -f "$current_link")"
fi

ln -sfn "$release_path" "$current_link"
if [ -n "$previous_target" ]; then
    echo "$previous_target" > "$rollback_file"
    echo "deploy-web: release anterior registado em $rollback_file para rollback."
fi

systemctl restart buildpc-web.service
sleep 2

if ! curl -fsS "$health_url" >/dev/null 2>&1; then
    echo "deploy-web: $health_url não respondeu após reiniciar o serviço." \
        "Verifique 'journalctl -u buildpc-web -n 50' e considere rollback-web.sh." >&2
    exit 1
fi

echo "deploy-web: $release_name publicado e respondendo em produção."
