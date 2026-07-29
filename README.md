# BuildPC

Aplicação desktop em C# e Avalonia para montar configurações de computadores,
somar custos e verificar compatibilidade básica entre componentes.

## Continuidade do desenvolvimento

Antes de alterar o projeto, leia
[`CONTEXTO_DO_PROJETO.md`](CONTEXTO_DO_PROJETO.md). Ele documenta o
comportamento atual, regras de negócio, telas, arquitetura, banco, API,
implantação e o checklist obrigatório para manter o projeto consistente.

## Recursos

- catálogo inicial com 24 componentes e preços ilustrativos em reais;
- categorias próprias para processadores, coolers, placas-mãe, memória, GPU,
  discos rígidos (HD), SSD/NVMe, fontes, gabinetes, monitores, mouses e teclados;
- total e consumo estimado recalculados em tempo real;
- validações de soquete, tipo de memória, formato do gabinete, cooler e potência;
- presets de configuração equilibrada e de alta performance;
- cadastro de novos produtos com persistência no catálogo local;
- seleção no catálogo com painel de características e proteção “Manter”;
- importações separadas da KaBuM! para cada categoria do catálogo;
- importações independentes de SSDs, discos rígidos (HD) e coolers;
- importações independentes de monitores, mouses e teclados, com separação de combos e acessórios;
- links de categoria editáveis diretamente em cada cartão de importação;
- miniaturas importadas e persistidas no SQLite, com visualização ampliada dos produtos;
- importação de todas as categorias em sequência e data/hora da última carga;
- substituição dos importados anteriores a cada nova carga, com opção de manter itens escolhidos;
- novos produtos disponíveis imediatamente nas opções da montagem;
- quantidade configurável por componente e dois espaços independentes para armazenamento;
- lista livre por categoria, com adição ilimitada, título, descrição e valor editáveis;
- catálogo em SQLite e produtos em ordem alfabética dentro de cada categoria;
- PDF da tabela de custo separado por categorias, preservando filtros e a
  ordenação escolhida por descrição ou custo;
- rodapé global com estado ONLINE/OFFLINE da API e barras de rolagem verticais
  reforçadas em todas as telas;
- montagem em linhas completas e listas zebradas com produto, descrição e valor destacados;
- filtros instantâneos em cada lista, incluindo `*`, `?` e exclusões como `-note*`;
- destaque visual das correspondências e ordenação alfabética ou por preço;
- interface responsiva em tema escuro de alto contraste, construída com MVVM.

## Executar

Requer o SDK do .NET 10.

```powershell
dotnet restore
dotnet run --project src/BuildPc.Desktop
```

## Testar

```powershell
dotnet test
```

Os dados iniciais ficam em
`src/BuildPc.Core/Services/ComponentCatalog.cs` e podem ser substituídos pelos
preços e produtos dos seus fornecedores. Produtos cadastrados pela interface
são mantidos no banco SQLite local do usuário. Cores, geometrias vetoriais de
ícones e estilos ficam em `src/BuildPc.Desktop/Resources` e
`src/BuildPc.Desktop/Styles`.

## Servidor privado

O aplicativo continua usando SQLite quando não existe o arquivo
`%LocalAppData%\BuildPC\servidor.json`. Quando esse arquivo contém uma URL HTTPS
e uma chave válidas, produtos, configurações e orçamentos passam a ser lidos e
gravados pela API `BuildPc.Api`.

A seção **Configurações > Servidor de dados** permite testar e trocar a URL e a
chave da API, ou voltar ao SQLite local. A alteração entra em vigor depois que o
BuildPC é reiniciado; a senha do PostgreSQL permanece somente na VPS.

Na VPS, a API acessa o PostgreSQL por `127.0.0.1:5432` e o Nginx publica somente
o caminho HTTPS `/buildpc-api/`. A porta do banco não deve ser liberada no
firewall. Modelos de configuração ficam na pasta `deploy`. O timer de backup
gera diariamente um `pg_dump` em `/var/backups/buildpc` e mantém 14 dias.
