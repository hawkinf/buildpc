# Contexto do projeto BuildPC

> Documento de continuidade para pessoas e ferramentas de IA.
>
> Este arquivo é a fonte de referência funcional e técnica do projeto. Toda
> alteração de comportamento, interface, regra de negócio, arquitetura, banco,
> API, dependência, comando de execução ou implantação deve atualizar este
> documento no mesmo commit.

Última atualização: 29/07/2026.

## Resumo rápido para continuar o trabalho

- Repositório: `https://github.com/hawkinf/buildpc.git`.
- Branch principal: `main`.
- Aplicativo usado pelo usuário: `src/BuildPc.Desktop/BuildPc.Desktop.csproj`.
- Servidor privado opcional: `src/BuildPc.Api/BuildPc.Api.csproj`.
- Biblioteca compartilhada: `src/BuildPc.Core/BuildPc.Core.csproj`.
- Testes: `tests/BuildPc.Core.Tests/BuildPc.Core.Tests.csproj`.
- Plataforma principal: Windows.
- Stack: C#, .NET 10, Avalonia 12, SQLite, PostgreSQL e MigraDoc/PDFsharp.
- Idioma da interface: português do Brasil.
- Formato monetário: `pt-BR`, como `R$ 1.299,90`.
- O usuário exige commit e push para `origin/main` após cada solicitação
  concluída.
- Antes de alterar código, leia este arquivo, verifique `git status` e preserve
  mudanças que não pertençam à tarefa atual.
- Depois de alterar código, execute:

```powershell
dotnet build BuildPc.sln --no-restore
dotnet test BuildPc.sln --no-build
```

No estado documentado, a solução compila sem avisos e possui 104 testes
aprovados.

## O que o programa faz

O BuildPC é um aplicativo desktop para manter um catálogo de componentes e
periféricos, consultar preços, montar orçamentos de venda e gerar PDFs para
clientes. Ele foi pensado para uma loja ou profissional que compra produtos por
um custo, aplica margens de lucro e vende uma composição completa.

O programa permite:

- importar produtos e imagens da KaBuM! por categoria;
- cadastrar, editar e excluir produtos manualmente;
- manter categorias e seus nomes;
- consultar tabelas de custo e venda;
- montar uma lista com quantos produtos forem necessários;
- editar título, descrição, quantidade e preço de venda de cada item;
- calcular custo, venda, lucro e percentual de lucro;
- gravar e consultar orçamentos;
- gerar PDFs de orçamento e de tabelas de preços;
- usar SQLite local ou uma API privada conectada ao PostgreSQL;
- configurar tema, empresa, logomarca, margens e acesso ao servidor.

## Navegação e telas ativas

O menu principal está definido em
`src/BuildPc.Desktop/Views/MainWindow.axaml`.

### Montagem

Arquivos principais:

- `Views/FlexibleListView.axaml`
- `ViewModels/FlexibleListViewModel.cs`
- `ViewModels/FlexibleListItemViewModel.cs`

Comportamento:

- seleciona uma categoria e depois um produto;
- permite filtrar e ordenar as opções;
- permite adicionar quantas linhas de produto forem necessárias;
- aceita quantidades de 1 a 100;
- mostra foto, categoria, marca, título, descrição e valor de venda;
- título e descrição são editáveis;
- preço de venda é editável e formatado em reais;
- o preço digitado nunca pode produzir margem inferior a 15%;
- os centavos do preço de venda são ajustados para terminar em `,90`;
- o botão de olho funciona por pressionar e manter pressionado;
- enquanto o olho está pressionado, aparecem custo por item, custo total,
  lucro e percentual de lucro;
- nome do cliente e telefone são obrigatórios para gravar;
- telefone usa máscara brasileira;
- observações do orçamento são opcionais;
- o orçamento precisa ser gravado antes de poder ser exportado;
- depois de qualquer alteração, é necessário gravar novamente antes de
  exportar;
- a exportação gera uma prévia PDF e abre o visualizador do sistema; é no
  visualizador que o usuário salva ou imprime.

Importante: `MainWindow.axaml` ainda contém uma montagem antiga baseada em
`Slots`, mas ela está desativada por `MainWindowViewModel.IsAssemblyView =>
false`. A tela chamada **Montagem** no menu é a `FlexibleListView`. Não
implemente recursos novos apenas na montagem antiga.

### Consultar preço

Arquivos principais:

- `Views/PriceLookupView.axaml`
- `ViewModels/PriceLookupViewModel.cs`

Comportamento:

- categoria, filtro textual e seleção Custo/Venda;
- ordenação clicando nos títulos Título ou Custo/Venda;
- o filtro exige que todas as palavras positivas sejam encontradas;
- cada linha é zebrada;
- ao manter o mouse sobre um produto, abre o preview animado compartilhado com
  foto, título, descrição e o preço atualmente selecionado;
- ao retirar o mouse, o preview retrai e fecha.

### Orçamentos

Arquivos principais:

- `Views/QuoteManagerView.axaml`
- `ViewModels/QuoteManagerViewModel.cs`
- `ViewModels/SavedQuoteListItemViewModel.cs`
- `ViewModels/SavedQuoteItemListItemViewModel.cs`

Comportamento:

- lista os orçamentos gravados;
- mostra número, cliente, telefone, data e itens;
- dentro do programa, cada item mostra venda e custo;
- o resumo mostra Total, Custo em vermelho, Lucro e % de lucro;
- os quatro valores do resumo usam o mesmo tamanho de fonte;
- a lista reserva espaço à direita para não deixar a barra de rolagem cortar
  preços;
- produtos possuem o mesmo preview animado das outras listas;
- orçamentos novos guardam também a URL da imagem;
- orçamentos antigos sem URL continuam válidos e usam o placeholder;
- o PDF do orçamento mostra somente dados apropriados ao cliente: itens,
  quantidade, preço unitário de venda e total de venda;
- custo, lucro e percentual de lucro nunca devem aparecer no PDF do cliente;
- ao exportar, a prévia PDF é aberta antes de salvar ou imprimir.

### Ferramentas > Gerenciar Produtos

Partes principais:

- catálogo em `MainWindow.axaml`, região `IsProductsView`;
- lógica em `MainWindowViewModel.cs`;
- formulário auxiliar em `ProductManagementView.axaml`;
- janela de edição em `ProductEditWindow.axaml`.

Comportamento:

- filtra por categoria ou por várias palavras;
- ordena por descrição ou pelo preço atualmente exibido;
- o padrão de entrada é mostrar Venda;
- alterna entre Custo e Venda;
- um clique no produto abre a edição;
- edição mostra valor de venda e protege custo, lucro e percentual com olho;
- a foto aparece em destaque e pode ser adicionada, trocada ou removida;
- não mostrar o link bruto da imagem na interface;
- permite novo produto, alteração, exclusão e exclusão em massa;
- o checkbox superior marca ou desmarca todos os produtos visíveis;
- a lixeira em massa deve ocupar pouco espaço;
- todas as linhas são zebradas e usam o preview animado;
- a exportação PDF respeita categoria, filtro, ordem e Custo/Venda selecionado;
- a tabela de custo é agrupada por categorias;
- a tabela inclui miniatura, título e preço;
- toda exportação abre primeiro a prévia no visualizador do sistema.

### Ferramentas > Gerenciar Categorias

Arquivos principais:

- `Views/CategoryManagementView.axaml`
- `ViewModels/CategoryManagementViewModel.cs`
- `ViewModels/CategoryManagementItemViewModel.cs`

Permite adicionar categoria, alterar nome e excluir. As definições ficam em
`BusinessSettings.ProductCategories`. Os valores do enum continuam sendo as
chaves técnicas; os nomes são configuráveis.

### Ferramentas > Importações

Partes principais:

- interface e modais em `MainWindow.axaml`, região `IsImportsView`;
- coordenação em `MainWindowViewModel.cs`;
- cartões em `ImportSourceViewModel.cs`;
- leitura da loja em `BuildPc.Core/Services/KabumCatalogImporter.cs`;
- substituição no banco por
  `IComponentCatalogRepository.ReplaceImported`.

Categorias, ordem e caminhos padrão:

| Ordem | Categoria técnica | Nome | Caminho KaBuM! |
|---:|---|---|---|
| 1 | `Processor` | Processadores | `/hardware/processadores` |
| 2 | `Cooler` | Coolers | `/hardware/coolers` |
| 3 | `Motherboard` | Placas-mãe | `/hardware/placas-mae` |
| 4 | `Memory` | Memórias | `/hardware/memoria-ram` |
| 5 | `GraphicsCard` | Placas de vídeo | `/hardware/placa-de-video-vga` |
| 6 | `HardDrive` | Discos rígidos (HD) | `/hardware/disco-rigido-hd` |
| 7 | `Storage` | SSDs / NVMe | `/hardware/ssd-2-5` |
| 8 | `PowerSupply` | Fontes | `/hardware/fontes` |
| 9 | `Case` | Gabinetes | `/perifericos/gabinetes` |
| 10 | `Monitor` | Monitores | `/computadores/monitores` |
| 11 | `Mouse` | Mouses | `/perifericos/teclado-mouse` |
| 12 | `Keyboard` | Teclados | `/perifericos/teclado-mouse` |

Regras da importação:

- o link de cada cartão é editável;
- a leitura começa na página 1 e percorre todas as páginas;
- o limite de segurança é 200 páginas;
- a leitura para quando encontra uma página vazia, erro de paginação ou página
  repetida;
- produtos repetidos são consolidados pelo ID;
- mouse e teclado compartilham a página da loja, mas o importador separa os
  produtos por nome e rejeita combos/acessórios incompatíveis;
- HD e SSD/NVMe também usam filtros independentes;
- título, marca e descrição removem menções `Kabum`, `Kabum!` e `no Kabum!`;
- a URL da miniatura é salva em `PcComponent.ImageUrl`;
- importar uma categoria substitui os importados anteriores daquela categoria;
- produtos manuais nunca são removidos pela importação;
- produtos importados marcados como `KeepOnImport`/“Manter” são preservados;
- antes de importar uma categoria ou todas, aparece uma confirmação destrutiva;
- a tela mostra um aviso permanente explicando a substituição;
- durante a importação aparece um modal com categoria, página, quantidade e
  progresso percentual;
- o progresso é calculado por categoria e por etapas/páginas. A loja não
  fornece um total de páginas confiável, portanto o avanço interno de cada
  categoria é uma estimativa crescente; categorias concluídas são exatas;
- o cálculo percentual compartilhado fica em
  `Desktop/Services/ImportProgressCalculator.cs` e possui teste de regressão;
- o botão Cancelar usa `CancellationToken` até a requisição HTTP;
- antes da gravação, o token é verificado novamente;
- a substituição de uma categoria é uma operação curta e atômica. Se o
  cancelamento ocorrer durante a gravação, a categoria atual pode concluir, mas
  as próximas não começam.

### Ferramentas > Configurações

Arquivos principais:

- `Views/PricingSettingsView.axaml`
- `ViewModels/PricingSettingsViewModel.cs`
- `ViewModels/DataServerSettingsViewModel.cs`

Contém:

- tema Sistema, Claro ou Escuro;
- margem de lucro global;
- margens diferentes por categoria;
- nome, documento, telefone, e-mail, site e endereço da empresa;
- logomarca;
- informações adicionais do orçamento;
- URL e chave da API;
- botão para testar a conexão;
- opção de desativar o servidor e voltar ao SQLite local.

Mudanças de servidor entram em vigor depois de reiniciar o aplicativo.
Ao salvar, as configurações gerais e de servidor são gravadas em
`buildpc.config.json`, ao lado do executável. A chave da API nunca é gravada em
texto aberto.

## Regras de preço e margem

- `PcComponent.Price` representa custo.
- A margem efetiva vem de `BusinessSettings.MarginFor(category)`.
- Se a categoria não tiver margem própria, usa a margem global.
- A margem mínima absoluta é `BusinessSettings.MinimumMarginPercent = 15`.
- A venda base é `custo × (1 + margem / 100)`.
- O resultado é arredondado para cima até um preço terminado em `,90`.
- Exemplo: um cálculo que resulte em `100,30` vira `100,90`; se resultar em
  `100,95`, vira `101,90`.
- Se o usuário digitar uma venda abaixo do mínimo, o programa corrige para o
  menor valor permitido.
- Valores digitados e exibidos usam a cultura `pt-BR`.
- Na seleção de produtos da Montagem e em Consulta de Preços, o valor deve
  refletir o modo atual: venda calculada ou custo.

Implementação principal:

- `FlexibleListItemViewModel.CalculateSalePrice`
- `FlexibleListItemViewModel.RoundUpToNinetyCents`
- `BusinessSettings.MarginFor`

## Filtros, ordenação e preview

`BuildPc.Core/Services/ProductFilter.cs` centraliza a busca.

- Termos separados por espaço funcionam como `E`.
- `nvme 256 adata` exige as três palavras.
- `*` representa qualquer sequência.
- `?` representa um caractere.
- termo iniciado por `-` exclui resultados, como `-note*`.
- a busca considera nome, marca, descrição, especificações e categoria.
- a interface destaca correspondências por
  `Controls/HighlightedTextBlock.cs`.

Todas as listas de produtos devem:

- ser zebradas;
- evitar que a barra de rolagem cubra preços ou botões;
- usar `Controls/ProductHoverPreview.axaml`;
- abrir o preview após 220 ms;
- animar expansão e opacidade;
- retrair e fechar em 170 ms quando o mouse sair;
- mostrar foto/placeholder, título, descrição e o preço do contexto atual.

Não volte a duplicar a lógica do popup em cada tela. O comportamento está
centralizado em `ProductHoverPreview.axaml.cs`.

## PDFs

Bibliotecas: `PDFsharp-MigraDoc` 6.2.4.

Serviços:

- `QuotePdfService`: orçamento do cliente;
- `ProductPriceTablePdfService`: tabela de custo ou venda;
- `PdfPreviewService`: cria arquivo temporário de prévia;
- `SystemFileLauncher`: abre o PDF no aplicativo padrão;
- `PdfFontConfiguration`: configuração de fontes.

Regras:

- sempre gerar e abrir a prévia antes de o usuário salvar ou imprimir;
- orçamento precisa estar gravado e sem alterações pendentes;
- PDF do cliente nunca mostra custo ou lucro;
- PDF de catálogo respeita filtro, categoria e ordenação da tela;
- PDF de custo agrupa por categoria;
- PDF de catálogo mostra miniatura, título e preço;
- dados e logomarca da empresa vêm do snapshot salvo no orçamento.

## Arquitetura

```text
BuildPc.Desktop (Avalonia/MVVM)
        |
        +--> BuildPc.Core (modelos, regras, SQLite, cliente HTTP)
        |          |
        |          +--> SQLite local
        |
        +--> BuildPcApiClient --HTTPS + X-BuildPc-Key--> BuildPc.Api
                                                        |
                                                        +--> PostgreSQL local da VPS
```

### Projetos

| Projeto | Responsabilidade |
|---|---|
| `BuildPc.Core` | Modelos, filtros, compatibilidade, importador, repositórios SQLite, contratos e cliente da API |
| `BuildPc.Desktop` | Aplicação Avalonia, telas, ViewModels, estilos, imagens e PDFs |
| `BuildPc.Api` | API ASP.NET Core privada, autenticação por chave e repositório PostgreSQL |
| `BuildPc.Core.Tests` | Testes xUnit do Core e de partes testáveis do Desktop |

### MVVM no Desktop

- `MainWindowViewModel` cria os serviços, escolhe SQLite/API e coordena a
  navegação.
- Views usam bindings para ViewModels.
- `RelayCommand` cobre operações síncronas.
- `AsyncRelayCommand` impede execução concorrente da mesma operação assíncrona.
- estilos globais ficam em `Styles/Controls.axaml`;
- cores de tema ficam em `Resources/Colors.axaml`;
- ícones vetoriais ficam em `Resources/Icons.axaml`;
- imagens remotas usam `Controls/RemoteImage.cs`.

`MainWindowViewModel.cs` e `MainWindow.axaml` são grandes porque ainda hospedam
o catálogo e a área de importações. Ao alterar uma função, confirme qual
propriedade `Is...View` torna a região visível.

## Modelos principais

### `PcComponent`

- `Id`
- `Category`
- `Name`
- `Brand`
- `Description`
- `Price` (custo)
- `ImageUrl`
- campos de compatibilidade
- `ImportSource`
- `KeepOnImport`
- `IsUserDefined`

### `BusinessSettings`

- margem global e margens por categoria;
- categorias e nomes configuráveis;
- dados da empresa;
- logomarca;
- informações adicionais;
- tema.

### `SavedQuote` e `SavedQuoteItem`

Guardam cliente, telefone, data, observações, custo, venda, margem, itens e
snapshot dos dados da empresa. O snapshot impede que uma configuração futura
da empresa altere um orçamento já gravado.

## Persistência local e configuração distribuível

O Desktop gera `buildpc.config.json` em `AppContext.BaseDirectory`, que no
aplicativo publicado é a pasta do executável. Sem uma seção de servidor ativa e
válida nesse arquivo, o Desktop usa SQLite.

Diretório:

```text
%LocalAppData%\BuildPC\
```

Arquivos relevantes:

| Arquivo/pasta | Uso |
|---|---|
| `catalogo.db` | produtos, metadados de importação, configurações e orçamentos |
| `produtos.json` | formato legado, usado apenas para migração quando aplicável |
| `imagens-produtos\` | fotos adicionadas localmente |
| `servidor.json` | formato legado de servidor, removido após migração |

Ao lado do executável:

| Arquivo | Uso |
|---|---|
| `buildpc.config.json` | configurações do sistema, empresa, margens, categorias, links de importação e VPS/API |

Formato conceitual:

```json
{
  "schemaVersion": 1,
  "application": {
    "globalMarginPercent": 35,
    "themeMode": "System"
  },
  "server": {
    "enabled": true,
    "baseUrl": "https://exemplo.com/buildpc-api/",
    "encryptedApiKey": "dpapi-current-user:v1:..."
  },
  "importSourceUrls": {
    "kabum:Processor": "https://www.kabum.com.br/hardware/processadores?..."
  }
}
```

`application` também contém margens por categoria, nomes das categorias, dados
da empresa, logomarca e informações adicionais do orçamento. Os links dos
cartões de importação são persistidos ao serem editados.

A chave da API usa DPAPI com escopo do usuário atual do Windows e entropia
própria do BuildPC. Portanto:

- o valor aberto nunca aparece no JSON;
- somente o mesmo usuário do Windows consegue descriptografá-lo;
- ao distribuir o executável e o JSON para outro computador ou usuário, a
  chave deve ser informada e salva novamente nessa instalação;
- `servidor.json` antigo ainda é lido uma vez para migração e é removido apenas
  depois que o arquivo unificado foi salvo com sucesso.

Implementação:

- `BuildPcApplicationSettingsStore`: leitura e gravação atômicas do JSON;
- `BuildPcApiKeyProtector`: proteção DPAPI da chave;
- `BuildPcApiSettings`: compatibilidade de leitura com o formato legado.

Tabelas conceituais:

- `products`
- `app_metadata`
- `business_settings`
- `quotes`

SQLite usa WAL. Alterações de schema são criadas/migradas pelos próprios
repositórios; não há ferramenta externa de migrations.

Interfaces:

- `IComponentCatalogRepository`
- `IQuoteRepository`

Implementações locais:

- `ComponentCatalogRepository`
- `QuoteRepository`

## API e PostgreSQL na VPS

O Desktop usa `BuildPcApiClient` quando a seção `server` de
`buildpc.config.json` está ativa, contém uma URL válida e sua chave pode ser
descriptografada.

Regras de segurança:

- não commitar chaves, senhas ou arquivos reais de configuração;
- a API exige o cabeçalho `X-BuildPc-Key`;
- `/health` é público; as demais rotas exigem chave;
- a comparação da chave usa SHA-256 e tempo constante;
- o PostgreSQL deve escutar apenas em `127.0.0.1:5432` na VPS;
- nunca liberar a porta 5432 no firewall;
- somente o endpoint HTTPS da API deve ser publicado pelo proxy reverso;
- a senha PostgreSQL fica apenas na VPS.

Configuração esperada pela API:

- `ConnectionStrings__BuildPc`
- `BuildPc__ApiKey`

Rotas principais:

- `GET /health`
- `GET /connection`
- CRUD em `/products`
- `POST /products/delete`
- `POST /products/descriptions`
- `PUT /products/{id}/keep`
- `POST /imports/replace`
- `GET /imports/last`
- `GET`/`PUT /settings`
- `GET`/`POST /quotes`

Arquivos de implantação:

- `deploy/buildpc-api.service`
- `deploy/buildpc-backup.service`
- `deploy/backup-buildpc.sh`

O serviço roda como usuário `buildpc` em `/opt/buildpc-api/current`, lê
`/etc/buildpc-api.env` e reinicia em caso de falha. O backup usa `pg_dump`,
grava em `/var/backups/buildpc` e mantém 14 dias.

O alias SSH usado anteriormente pelo usuário foi `contaslite`; ele depende do
arquivo SSH local da máquina e deve ser confirmado antes de qualquer operação
remota.

### Migração SQLite para PostgreSQL

A API possui modos de linha de comando:

```powershell
dotnet run --project src/BuildPc.Api -- --backup-sqlite CAMINHO_ORIGEM CAMINHO_BACKUP
dotnet run --project src/BuildPc.Api -- --import-sqlite CAMINHO_BACKUP
```

Faça backup antes de migrar e nunca exponha diretamente o banco.

## Executar, testar e publicar

Requer SDK .NET 10.

### Restaurar e compilar

```powershell
dotnet restore BuildPc.sln
dotnet build BuildPc.sln --no-restore
```

### Executar o aplicativo

```powershell
dotnet run --project src/BuildPc.Desktop/BuildPc.Desktop.csproj
```

Se `run.ps1` perguntar qual executável usar:

1. `BuildPc.Api` é o servidor;
2. `BuildPc.Desktop` é o programa com interface usado no Windows.

Para publicar o Desktop diretamente:

```powershell
.\run.ps1 -Project src\BuildPc.Desktop\BuildPc.Desktop.csproj -Arch x64
```

### Executar a API localmente

Defina a conexão e a chave somente no ambiente:

```powershell
$env:ConnectionStrings__BuildPc='Host=127.0.0.1;Port=5432;Database=buildpc;Username=buildpc;Password=...'
$env:BuildPc__ApiKey='...'
dotnet run --project src/BuildPc.Api/BuildPc.Api.csproj
```

Não copie valores reais para documentação ou commits.

### Testar

```powershell
dotnet test BuildPc.sln
```

Os testes cobrem repositórios, filtro, margens, montagem, telefone, importação
com múltiplas páginas, progresso/cancelamento, API, PDFs e gerenciamento de
categorias. Também verificam a persistência completa da configuração e que a
chave da API não aparece em texto aberto no JSON.

## Convenções obrigatórias de interface

- textos para o usuário em português do Brasil;
- não exibir URL bruta da foto;
- custo é informação sensível;
- olho sensível funciona enquanto estiver pressionado, não como alternância;
- listas de produtos sempre zebradas;
- listas de produtos sempre usam o preview animado compartilhado;
- barras verticais devem ser grossas e não cobrir preços ou lixeiras;
- todas as telas mantêm rodapé reservado;
- rodapé global mostra ONLINE em verde ou OFFLINE em vermelho;
- usar recursos de cor dinâmica para Claro/Escuro/Sistema;
- evitar valores de cor fixos fora de overlays, sombras ou casos justificados;
- exportações sempre abrem a prévia;
- exclusões e substituições destrutivas exigem confirmação;
- não reintroduzir o menu antigo “Lista livre”: o nome atual é Montagem;
- Gerenciar Produtos fica dentro de Ferramentas.

## Cuidados ao alterar

1. Não confunda custo com venda.
2. Não permita margem menor que 15%.
3. Preserve o arredondamento final em `,90`.
4. Não coloque custo ou lucro no PDF do cliente.
5. Preserve filtro e ordenação na exportação da tabela.
6. Não remova produtos manuais durante importação.
7. Respeite `KeepOnImport`.
8. Passe `CancellationToken` em toda nova operação de rede da importação.
9. Atualize todas as coleções após alterar o catálogo: Montagem, Consulta,
   Gerenciar Produtos, contagens e categorias.
10. Em listas novas de produtos, use `ProductHoverPreview`.
11. Se mudar modelo persistido, mantenha compatibilidade com dados antigos.
12. Se mudar API, atualize cliente, contratos, servidor e testes juntos.
13. Nunca grave segredos no repositório.
14. Atualize este documento no mesmo commit da alteração.

## Checklist de conclusão para qualquer IA

- [ ] Entendeu qual tela e ViewModel estão ativos.
- [ ] Verificou `git status` antes de editar.
- [ ] Preservou alterações não relacionadas.
- [ ] Implementou a solicitação.
- [ ] Atualizou `CONTEXTO_DO_PROJETO.md`.
- [ ] Executou `git diff --check`.
- [ ] Executou build sem avisos.
- [ ] Executou todos os testes.
- [ ] Revisou alterações destrutivas e dados sensíveis.
- [ ] Fez commit com mensagem objetiva.
- [ ] Fez push para `origin/main`.
- [ ] Informou ao usuário o commit e o resultado dos testes.

## Histórico recente relevante

- Preview animado compartilhado foi aplicado a todas as listas de produtos.
- Gerenciador de Orçamentos recebeu folga para scrollbar e tabela financeira
  alinhada.
- Gerenciar Produtos foi movido para Ferramentas.
- Consulta de Preços recebeu custo/venda, filtro, ordenação e preview.
- Importações receberam confirmação destrutiva, acompanhamento por página,
  progresso percentual e cancelamento.
- Configurações passaram a usar um JSON unificado ao lado do executável, com
  chave da API protegida pelo Windows e migração do `servidor.json` legado.

Ao adicionar uma nova capacidade importante, acrescente-a aqui e na seção
correspondente, removendo informações que deixarem de ser verdadeiras.
