#!/usr/bin/env bash
set -euo pipefail

# Uso: deploy-api.sh /opt/buildpc-api/releases/<nome-do-release>
#
# Antes, o deploy de rotina era ~8 passos manuais por SSH (publicar, copiar,
# subir numa porta livre, testar /health, trocar o symlink, reiniciar,
# validar por HTTPS, anotar ROLLBACK.txt à mão) — fácil de errar um passo,
# principalmente o de testar ANTES de trocar o symlink. Este script formaliza
# exatamente essa sequência: só troca "current" se o release novo já
# respondeu /health numa porta de teste; se não responder, aborta sem tocar
# em nada em produção.
#
# Pré-requisito: o release já publicado (self-contained linux-x64) e
# copiado para o diretório informado, por exemplo:
#   dotnet publish src/BuildPc.Api/BuildPc.Api.csproj -c Release -r linux-x64 \
#     --self-contained true -o /tmp/release-local
#   rsync -a /tmp/release-local/ vps:/opt/buildpc-api/releases/<nome>/

release_path="${1:?Uso: deploy-api.sh /opt/buildpc-api/releases/<nome>}"
release_name="$(basename "$release_path")"
base_dir=/opt/buildpc-api
current_link="$base_dir/current"
rollback_file="$base_dir/ROLLBACK.txt"
env_file=/etc/buildpc-api.env
test_port=8129
health_timeout_seconds=30
health_url="https://contaslite.hawk.com.br/buildpc-api/health"

if [ ! -d "$release_path" ] || [ ! -x "$release_path/BuildPc.Api" ]; then
    echo "deploy-api: $release_path não existe ou não tem o binário BuildPc.Api." >&2
    exit 1
fi

echo "deploy-api: testando $release_name na porta $test_port antes de trocar 'current'..."
set -a
# shellcheck disable=SC1090
source "$env_file"
set +a
export ASPNETCORE_URLS="http://127.0.0.1:$test_port"
"$release_path/BuildPc.Api" &
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
    echo "deploy-api: $release_name não respondeu /health em ${health_timeout_seconds}s." \
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
    echo "deploy-api: release anterior registado em $rollback_file para rollback."
fi

systemctl restart buildpc-api.service
sleep 2

if ! curl -fsS "$health_url" >/dev/null 2>&1; then
    echo "deploy-api: $health_url não respondeu após reiniciar o serviço." \
        "Verifique 'journalctl -u buildpc-api -n 50' e considere rollback-api.sh." >&2
    exit 1
fi

echo "deploy-api: $release_name publicado e respondendo em produção."
