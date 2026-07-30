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

No estado documentado, a solução compila sem avisos e possui 229 testes
aprovados. O GitHub Actions repete build em Release e testes a cada push e
pull request (`.github/workflows/build.yml`).

## O que o programa faz

O BuildPC é um aplicativo desktop para manter um catálogo de componentes e
periféricos, consultar preços, montar orçamentos de venda e gerar PDFs para
clientes. Ele foi pensado para uma loja ou profissional que compra produtos por
um custo, aplica margens de lucro e vende uma composição completa.

O programa permite:

- importar produtos e imagens da KaBuM! por categoria, com histórico de preços
  e aviso do que subiu, baixou ou saiu do catálogo da loja;
- exportar e importar o catálogo em CSV, para edição em planilha;
- marcar produtos como favoritos, que passam à frente nas listas de seleção;
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

- o cartão “Catálogo local” da barra lateral mostra o total geral e todas as
  categorias no formato `Categoria (quantidade)`;
- a navegação da barra lateral possui rolagem própria para não sobrepor esse
  cartão quando Ferramentas está expandido em uma janela baixa.

### Montagem

Arquivos principais:

- `Views/FlexibleListView.axaml`
- `ViewModels/FlexibleListViewModel.cs`
- `ViewModels/FlexibleListItemViewModel.cs`

Comportamento:

- seleciona uma categoria e depois um produto;
- permite filtrar e ordenar as opções;
- permite adicionar quantas linhas de produto forem necessárias;
- aceita quantidades de 1 a 9999, digitadas em `NumericUpDown`. O teto alto
  existe só para conter erro de digitação (`Models/QuantityRange.cs`);
- as linhas podem ser reordenadas: a ordem da montagem é a ordem dos itens no
  PDF do cliente;
- desconto em reais, validade em dias, condições de pagamento e prazo de
  entrega são informados aqui e vão para o orçamento e para o PDF;
- o cabeçalho mostra o valor que o cliente paga; com desconto, o total dos
  itens aparece riscado acima;
- "Modelos de montagem" salva a combinação atual e permite aplicá-la de volta.
  Um modelo guarda só produto e quantidade: ao aplicar, custo e margem vêm do
  catálogo atual, diferente de reabrir um orçamento;
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
- o preço total de venda fica no cabeçalho, imediatamente antes do botão de
  olho;
- a Montagem não exibe cartões separados de quantidade de produtos ou de
  preço total no corpo da tela;
- os botões de olho, gravação e exportação ficam no cabeçalho da Montagem,
  junto de “Limpar montagem”;
- “Limpar montagem” exige confirmação, como as demais ações destrutivas. A
  mensagem avisa quando o orçamento ainda não foi gravado e, quando já foi,
  lembra que ele continua na lista de Orçamentos;
- atalhos: `Ctrl+S` grava, `Ctrl+P` exporta, `Ctrl+L` limpa e `Esc` fecha a
  confirmação. Funcionam mesmo com o foco em um campo de texto, por
  tunelamento do evento;
- o rodapé interno da Montagem fica reservado aos avisos de gravação e aos
  totais sensíveis revelados pelo olho;
- a exportação gera uma prévia PDF e abre o visualizador do sistema; é no
  visualizador que o usuário salva ou imprime.

Importante: a montagem antiga baseada em `Slots` foi removida de
`MainWindow.axaml` e de `MainWindowViewModel`, junto com o painel lateral de
resumo, os presets e as propriedades `IsAssemblyView`, `Slots`,
`SelectedItems`, `Issues`, `ProgressText`, `ProgressValue`, `EstimatedPower`,
`TotalCost` e `CompatibilityTitle`. A única Montagem é a `FlexibleListView`.

`CompatibilityService`, `PcBuild` e `CompatibilityIssue` (em `BuildPc.Core`)
foram conectados à `FlexibleListView` (auditoria de 30/07, lote 3b):
`FlexibleListViewModel.RefreshCompatibility()` roda a cada mudança de item e
expõe `CompatibilityIssues`/`HasCompatibilityIssues`, exibidos num painel
próprio quando há erro/aviso de soquete, memória ou fonte. É recurso
disponível ao usuário — pode ser descrito como tal.

### Consultar preço

Arquivos principais:

- `Views/PriceLookupView.axaml`
- `ViewModels/PriceLookupViewModel.cs`

Comportamento:

- categoria, filtro textual e seleção Custo/Venda;
- o seletor mostra `Categoria (total)` e, com filtro textual ativo,
  `Categoria (total) (filtrados)`;
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
- o resumo financeiro e o botão de exportação ficam na segunda faixa do
  cabeçalho, abaixo do título e do botão “Atualizar lista”;
- a lista reserva espaço à direita para não deixar a barra de rolagem cortar
  preços;
- produtos possuem o mesmo preview animado das outras listas;
- orçamentos novos guardam também a URL da imagem;
- orçamentos antigos sem URL continuam válidos e usam o placeholder;
- o PDF do orçamento mostra somente dados apropriados ao cliente: itens,
  quantidade, preço unitário de venda e total de venda;
- custo, lucro e percentual de lucro nunca devem aparecer no PDF do cliente;
- ao exportar, a prévia PDF é aberta antes de salvar ou imprimir;
- “Abrir na Montagem” recarrega o orçamento selecionado na Montagem para
  consulta ou ajuste. Título, descrição, quantidade, custo, margem e preço de
  venda voltam como foram acordados, e não recalculados pelo catálogo atual;
- gravar depois de reabrir atualiza o mesmo número, sem criar outro orçamento;
- itens cujo produto foi excluído do catálogo continuam abrindo, reconstruídos
  a partir do próprio orçamento;
- o orçamento selecionado pode ser excluído pela lixeira ao lado de
  “Exportar PDF”, sempre com confirmação destrutiva;
- excluir não reaproveita o número: a numeração continua de `MAX(number) + 1`.

### Ferramentas > Gerenciar Produtos

Partes principais:

- catálogo em `MainWindow.axaml`, região `IsProductsView`;
- lógica em `MainWindowViewModel.cs`;
- formulário auxiliar em `ProductManagementView.axaml`;
- janela de edição em `ProductEditWindow.axaml`.

Comportamento:

- filtra por categoria ou por várias palavras;
- o seletor mostra `Categoria (total)` e, com filtro textual ativo,
  `Categoria (total) (filtrados)`;
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
- toda exportação abre primeiro a prévia no visualizador do sistema;
- as duas listas de produtos (catálogo em `IsProductsView` e o painel de
  `ProductManagementView`) usam `ListBox` com `VirtualizingStackPanel` em vez
  de `ItemsControl` dentro de `ScrollViewer`, para catálogos com milhares de
  produtos não materializarem todas as linhas de uma vez.

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
- há uma pausa de 350 ms entre páginas para não disparar o limite da loja;
- falhas temporárias (429, 408, 500, 502, 503, 504 e erros de rede) são
  repetidas até 4 tentativas por página, com espera crescente e respeitando o
  cabeçalho `Retry-After`;
- se uma página continuar falhando, os produtos já lidos são gravados em vez de
  perder a categoria inteira. Só a falha da primeira página aborta a categoria;
- o Nginx aceita corpo de até 64 MB, porque uma categoria grande é enviada em um
  único JSON para `POST /imports/replace`;
- produtos repetidos são consolidados pelo ID;
- mouse e teclado compartilham a página da loja, mas o importador separa os
  produtos por nome e rejeita combos/acessórios incompatíveis;
- HD e SSD/NVMe também usam filtros independentes;
- título, marca e descrição removem menções `Kabum`, `Kabum!` e `no Kabum!`;
- a URL da miniatura é salva em `PcComponent.ImageUrl`;
- a importação não grava o arquivo remoto da imagem em disco. `RemoteImage`
  baixa a imagem quando ela precisa ser exibida e mantém os bytes num cache
  em memória compartilhado, **limitado a 96 MB** e com descarte do item usado
  menos recentemente (`Services/BoundedImageCache.cs`);
- fotos adicionadas manualmente são copiadas de forma persistente para
  `%LocalAppData%\BuildPC\imagens-produtos` por `Services/ProductImageStore.cs`,
  que também apaga a foto quando ela deixa de ser referenciada. Arquivos fora
  dessa pasta nunca são excluídos: pertencem ao usuário;
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
- os seletores de categoria de Consulta de Preços e Gerenciar Produtos mostram
  a contagem total por categoria e, quando há filtro textual, uma segunda
  contagem com os resultados correspondentes.

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
- as prévias ficam em `%TEMP%\BuildPC\visualizacoes-pdf` e expiram em 1 hora,
  porque incluem tabelas de custo e dados de clientes;
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

A janela principal usa duas colunas: `230` para a navegação e `*` para o
conteúdo. A terceira coluna de `320 px` existia apenas para o resumo da
montagem antiga e foi removida. Com isso a largura mínima caiu para `1100 px`.

A folga inferior das telas roláveis vem do estilo `Border.scroll-safe-area`
em `Styles/Controls.axaml`. Não repita `Height="180"` nas views: um teste
estrutural (`ScrollableLayoutTests`) falha se o valor voltar a ser fixo.

### Compiled bindings

`AvaloniaUseCompiledBindingsByDefault` está **`true`**. A migração foi feita
view por view (eram 622 erros ao ligar de uma vez) e todas as oito telas
declaram `x:CompileBindings="True"` e `x:DataType` na raiz e em cada
`DataTemplate`.

Consequência prática: **um nome de propriedade errado agora é erro de build**,
não uma ligação que falha em silêncio. Ao criar uma tela nova, declare o
`x:DataType` da raiz e de cada `DataTemplate`; sem isso o build falha.

A migração já apanhou um erro real: o painel de modelos da Montagem estava
ligado a `$parent[UserControl].DataContext.Templates`, mas o `DataContext`
daquele controle é `FlexibleListViewModel`, não `MainWindowViewModel` — em
tempo de execução o painel ficaria vazio sem qualquer aviso.

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
- há limite de 1200 requisições por minuto e por endereço, para encurtar
  tentativas de força bruta contra a chave;
- a rotação da chave é em duas etapas: `BuildPc__ApiKey` é a chave em uso e
  `BuildPc__PreviousApiKey` continua aceita enquanto os clientes são
  atualizados. Remova a anterior depois de todos migrarem;
- operações que alteram ou removem dados, e tentativas recusadas, são
  registradas em log de auditoria com método, rota, endereço e código;
- o nível de log de `Microsoft.AspNetCore` é `Warning`: o padrão gravava duas
  linhas por requisição e enchia o journal do servidor;
- `--import-sqlite` apaga produtos, orçamentos e configurações antes de gravar
  o snapshot, portanto exige `--force` explícito;
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
- `DELETE /quotes/{id}`
- `GET /imports/last-all`
- `PUT /products/{id}/favorite`
- `GET /products/{id}/price-history`
- `GET`/`POST /templates`
- `DELETE /templates/{id}`

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

### Estado verificado do servidor (29/07/2026)

Auditoria somente de leitura via `ssh contaslite`:

| Item | Estado |
|---|---|
| `buildpc-api`, `postgresql`, `nginx` | ativos; API em `127.0.0.1:8125` |
| PostgreSQL | 14.23, escutando só em `127.0.0.1:5432` |
| Firewall (ufw) | ativo; 5432 e 8125 não expostos |
| Porta 80 | redireciona 301 para HTTPS |
| `/buildpc-api/health` sem chave | 200 |
| `/buildpc-api/products` sem chave | 401 |
| `/etc/buildpc-api.env` | `600 root:root`; `ASPNETCORE_ENVIRONMENT=Production` |
| TLS | válido até 07/09/2026, `certbot.timer` ativo |
| `unattended-upgrades` | habilitado |
| Base `buildpc` | 11 MB, 1420 produtos, 2 orçamentos |
| Disco | 73% usado (11 GB livres) |
| Memória | 2,4 GB totais, folga pequena |

**Ações aplicadas no servidor em 29/07/2026:**

1. `client_max_body_size` do bloco `/buildpc-api/` passou de `10m` para `64m`
   em `/etc/nginx/sites-enabled/contaslite.conf`, com `nginx -t` e reload.
   Backups de configuração do Nginx ficam em `/etc/nginx/backups/` — **nunca**
   dentro de `sites-enabled/`, porque `include sites-enabled/*` carregaria o
   backup como um segundo servidor e `nginx -t` falha.
2. Deploy da API para `/opt/buildpc-api/releases/audit-20260729-175553`, com o
   destino anterior registado em `/opt/buildpc-api/ROLLBACK.txt`. A poda removeu
   577 marcas obsoletas: `app_metadata` caiu de 590 para 37 linhas, mantendo as
   24 marcas legítimas do catálogo inicial. Produtos (1420) e orçamentos (2)
   intactos. A memória do serviço caiu de 102 MB para 37 MB.
4. Segundo deploy para `releases/fase4-20260729-195631`, com as rotas de
   favoritos, histórico de preços e modelos, a rotação de chave e a auditoria.
   Antes da troca do symlink foi feito `pg_dump` e o binário novo foi validado
   na porta 8129. As migrações de schema criaram `price_history` e
   `assembly_templates` e acrescentaram `is_favorite` a `products` e desconto,
   validade e condições a `quotes`, preservando 1420 produtos e 2 orçamentos.
   Verificado por HTTPS: todas as rotas respondem, chave inválida devolve 401 e
   é auditada, e o log continua sem ruído por requisição.
3. O backup foi executado (`systemctl start buildpc-backup.service`) e o
   restauro foi validado num banco descartável: 1420 produtos, 2 orçamentos,
   37 metadata e 1 settings, idêntico à produção. Retenção de 14 dias
   confirmada no script e o timer diário está `enabled`.

**Publicação da API — o build tem de ser self-contained**

A VPS **não tem runtime .NET instalado** (`dotnet` não existe no PATH). Publicar
com `--self-contained false` gera um pacote que não inicia. Use sempre:

```powershell
dotnet publish src/BuildPc.Api/BuildPc.Api.csproj -c Release -r linux-x64 --self-contained true -o PASTA
```

O pacote correto tem ~343 arquivos e inclui `libcoreclr.so` e
`System.Private.CoreLib.dll`. Antes de trocar o symlink `current`, teste o
binário novo numa porta livre (`ASPNETCORE_URLS=http://127.0.0.1:8129`) com o
mesmo `/etc/buildpc-api.env`; só troque depois de `/health` responder 200.

Ao encerrar essa instância de teste, localize o PID pela porta
(`ss -tlnpH "sport = :8129"`). **Não use `pkill -f` com um padrão que apareça na
própria linha de comando por SSH**: o padrão casa com o próprio comando e mata a
sessão.

**Cópia externa do backup — resolvida reaproveitando o pipeline da VPS**

A VPS já tinha uma infraestrutura de backup pronta que o BuildPC não usava:

- `system-backup.service` / `.timer` (03:10 diário) roda
  `/usr/local/bin/system_backup.sh`, que faz `restic backup` e depois
  `rclone copy` para `gdrive:Backups/VPS-hawk-server/restic`;
- `restic` cifra o repositório, faz deduplicação e já guarda ~29 GB no Drive;
- existem ainda `system-backup-check.timer`, `backup-restore-test.timer` e
  `hawk-backup-watchdog.timer` cobrindo verificação e alerta por e-mail.

O problema era só de cobertura: a lista de caminhos do `restic` incluía
`/var/backups/system` mas **não** `/var/backups/buildpc`, então os dumps do
BuildPC nunca saíam da máquina. A correção foi acrescentar esse diretório à
linha do `restic backup` em `/usr/local/bin/system_backup.sh` (backup do script
original em `/root/system_backup.sh.bak-*`).

Verificado em 29/07/2026: o snapshot `5b695bda` contém
`buildpc-20260729-131635.dump` e `buildpc-20260729-205937.dump`, e esse mesmo
snapshot foi confirmado em `gdrive:Backups/VPS-hawk-server/restic/snapshots`.

Não crie um pipeline separado para o BuildPC: basta que
`/var/backups/buildpc` continue na lista de caminhos do `restic`.

Observações sobre esse pipeline:

- o `rclone copy` do script **não** apaga no destino, então o Drive acumula mais
  snapshots que o repositório local (151 contra 25). É mais seguro, mas cresce;
- o sync para o Drive falha de vez em quando por cota da API do Google
  (`Error 403: Quota exceeded ... Queries per minute`). O script trata isso como
  aviso e mantém o backup local, e a execução seguinte recupera o atraso;
- `msmtp` não consegue escrever em `/var/log/systembackup/msmtp.log`
  (permissão), mas o e-mail de alerta é enviado normalmente.

**Verificado e sem ação necessária:**

- O journal do systemd ocupa 1 GB porque `SystemMaxUse=1G` já está definido em
  `/etc/systemd/journald.conf` e é respeitado. Nesse 1 GB cabem ~46 dias de
  histórico de todos os serviços da máquina. Não vacuum sem motivo: a redução do
  log da própria API já libera a maior parte desse espaço para histórico útil.

### Migração SQLite para PostgreSQL

A API possui modos de linha de comando:

```powershell
dotnet run --project src/BuildPc.Api -- --backup-sqlite CAMINHO_ORIGEM CAMINHO_BACKUP
dotnet run --project src/BuildPc.Api -- --import-sqlite CAMINHO_BACKUP --force
```

`--import-sqlite` **apaga** produtos, orçamentos e configurações do PostgreSQL
antes de gravar o snapshot. Sem `--force` o comando recusa e explica o risco.
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
- todas as telas mantêm rodapé reservado e uma folga rolável inferior que
  permite levar o último controle completamente acima desse rodapé;
- o conteúdo direto de um `ScrollViewer` deve ter altura natural, como
  `StackPanel` ou `ItemsControl`; não use `Grid` diretamente, pois o conteúdo
  excedente pode ficar fora do cálculo da área rolável do Avalonia;
- a janela principal aceita altura mínima de 640 px e as telas devem continuar
  utilizáveis nesse tamanho;
- rodapé global mostra `LOCAL` em cinza quando o programa usa o SQLite deste
  computador, `ONLINE` em verde quando o servidor responde e `OFFLINE` em
  vermelho só quando existe servidor configurado e ele falhou. Nunca alarme o
  usuário por estar no modo local, que é o modo de uso normal;
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
15. Nunca use `ConfigureAwait(false)` em `await`s dentro do caminho de
    inicialização da interface (`MainWindowViewModel.CreateAsync` e afins).
    Em modo local, o SQLite devolve tarefas já concluídas e o defeito fica
    invisível; em modo servidor, a chamada HTTP real retorna numa thread do
    pool e a construção de objetos do Avalonia (`DispatcherTimer`, tema) falha
    com "The calling thread cannot access this object because a different
    thread owns it". Isso só aparece testando o app de verdade contra a API,
    nunca em testes unitários com SQLite/mocks.

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

- Auditoria completa (30/07), lote 4 — deploy/infraestrutura: novos
  `deploy/deploy-api.sh` e `deploy/rollback-api.sh` formalizam em script o
  que era ~8 passos manuais por SSH (publicar, copiar para
  `releases/<nome>`, subir numa porta de teste, esperar `/health` responder,
  só então trocar o symlink `current`, reiniciar, validar por HTTPS, anotar
  `ROLLBACK.txt`). `deploy-api.sh <caminho-do-release>` aborta sem tocar em
  produção se o release novo não responder `/health` na porta de teste;
  `rollback-api.sh` lê o `ROLLBACK.txt` já gravado e reverte em um comando.
  `backup-buildpc.sh` agora valida que o `.dump` não ficou vazio antes de
  seguir (`test -s`), e `buildpc-backup.service` ganhou
  `OnFailure=buildpc-backup-alert.service` (novo, registra no journal com
  prioridade `err` — ajustável para o mesmo mecanismo de e-mail que o
  `hawk-backup-watchdog` do servidor já usa) para uma falha de backup deixar
  de passar silenciosamente até uma restauração real ser necessária. Nenhum
  destes arquivos foi executado na VPS nesta sessão — só versionados; a
  aplicação em produção é uma ação separada.
- Auditoria completa (30/07), lote 3c — consolidação da edição de produto:
  existiam dois formulários completos para o mesmo produto (`ProductEditWindow`,
  modal aberto ao clicar numa linha em Gerenciar Produtos, e
  `ProductManagementView`, tela cheia aberta por "Novo produto"), ambos ligados
  às mesmas propriedades/comandos de `MainWindowViewModel` mas com UI
  duplicada e divergente — só o modal tinha o "olho" de custo/lucro
  (mascarado + venda/lucro/%lucro) e o painel de histórico de preço.
  `ProductEditWindow` foi removido; `ProductManagementView.axaml` ganhou o
  bloco de custo/venda/lucro com o olho (pointer + Espaço/Enter, mesmo
  padrão da Montagem) e o painel de histórico de preço, portados do modal.
  `BeginEditProductAsync` agora chama `ShowToolView("product-management")`
  antes do primeiro `await`, então `EditProductCommand` navega sozinho para
  a tela — `CatalogProduct_Click` (clique numa linha de Gerenciar Produtos)
  não abre mais janela nenhuma, só seleciona e edita. `ShowView` já era
  idempotente (não faz nada se a view pedida já é a atual), então chamar a
  partir do próprio botão "Editar" dentro de `ProductManagementView` é
  seguro. **Sem cobertura de teste automatizado para nenhum dos dois fluxos
  antes ou depois** (confirmado por busca — nenhum teste referencia
  `MainWindowViewModel`, `ProductEditWindow` ou `ProductManagementView`);
  verificação feita por build com compiled bindings (0 erros/avisos, pega
  binding quebrado) e leitura de código; a verificação visual manual nesta
  sessão foi interrompida por instabilidade do ambiente de automação de
  clique (janela sendo minimizada sozinha), não por um erro do aplicativo —
  vale um teste manual de verdade (clicar numa linha, conferir olho e
  histórico, salvar) antes do próximo release.
- Auditoria completa (30/07), lote 3b — duas funcionalidades novas: (1)
  `CompatibilityService` (soquete CPU/placa-mãe, tipo de memória, cooler,
  formato do gabinete, wattagem da fonte) já existia testado mas nenhuma
  tela o usava; agora `FlexibleListViewModel` recalcula a cada mudança de
  item e mostra os avisos (só Erro/Aviso, não a mensagem informativa "tudo
  certo") num painel na Montagem. (2) `AssemblyTemplate` ganhou
  `KitDiscountPercent` (0–100%, nova coluna `kit_discount_percent` em
  `assembly_templates` no SQLite e no Postgres, migração automática nos dois
  via `EnsureColumn`/`ADD COLUMN IF NOT EXISTS`): ao salvar um modelo o
  usuário pode sugerir um desconto, e ao aplicar o modelo o campo de
  desconto do orçamento é pré-preenchido a partir do preço recalculado —
  mesmo mecanismo de `DiscountText` que já existia, só automatizado.
- Auditoria completa (30/07), lote 3a — UI/UX: estado vazio em Orçamentos
  (usa `QuoteManagerViewModel.IsEmpty`, já existia mas não estava ligado no
  XAML), Consultar Preço e Gerenciar Produtos (`PriceLookupViewModel.
  IsEmpty`/`MainWindowViewModel.IsCatalogEmpty`, novos). Estrela de favorito
  agora aparece no seletor de produto da Montagem e em Consultar Preço, não
  só em Gerenciar Produtos. Novo estilo `TextBox.invalid`/`NumericUpDown.
  invalid` (`Controls.axaml`) dá borda vermelha ao campo com erro, aplicado
  em nome/telefone do cliente (Montagem) e nome de categoria (Gerenciar
  Categorias) — antes só uma mensagem de texto separada avisava. O "olho" de
  custo/lucro (Montagem e edição de produto) agora funciona segurando
  Espaço/Enter, não só o clique do mouse. Configurações > Logomarca ganhou
  miniatura de pré-visualização ao lado do caminho do arquivo.
- Auditoria completa (30/07), lote 2 — lógica de negócio: CSV agora rejeita
  custo zero (só negativo era barrado) e categoria fora do enum (antes virava
  produto "fantasma": gravado, mas invisível em toda lista filtrada, sem erro
  visível). Importador Kabum agora repete também em timeout de rede
  (`TaskCanceledException` sem cancelamento do usuário), não só em status
  HTTP transitório. `CompanySnapshot` (novo, em `BuildPc.Core.Models`)
  substitui `BusinessSettings` como tipo de `SavedQuote.CompanySnapshot` e
  `QuoteDraft.CompanySnapshot` — só os campos de identificação da empresa
  (nome, documento, telefone, e-mail, site, endereço, logo, informações
  adicionais), não mais a margem global/por categoria nem o tema. Orçamentos
  antigos continuam legíveis: o JSON gravado antes tinha mais campos, e o
  `System.Text.Json` ignora os que não existem no tipo novo.
- Auditoria completa (30/07): `/connection` agora executa `SELECT 1` de
  verdade no PostgreSQL (`PostgresBuildPcRepository.CanConnect`) — antes só
  confirmava que o processo da API estava de pé, então o rodapé podia mostrar
  ONLINE com o banco fora do ar. O middleware de auditoria foi movido para
  antes do `UseExceptionHandler`, para gravar também tentativas rejeitadas
  por exceção (400/409/500), não só sucesso e 401. `QuoteValidation.
  EnsureMinimumMargin` e `ProductValidation.EnsureValid` (novos, em
  `BuildPc.Core.Services`) passaram a recusar, na camada de persistência
  (SQLite e Postgres), item de orçamento abaixo da margem mínima de 15% e
  produto com preço/potência negativos ou categoria fora do enum — antes só
  a tela impedia. Edição manual de produto agora grava `price_history`
  (`source = "manual"`), igual a uma importação. `ReplaceImported` no
  Postgres ganhou `pg_advisory_xact_lock` por categoria+origem, mesmo padrão
  já usado na numeração de orçamento, para duas chamadas concorrentes não
  perderem favorito/histórico. JSON malformado na API agora responde 400 em
  vez de 500, e o limite de corpo do Kestrel foi alinhado aos 64 MB já
  liberados no Nginx.

- Telas roláveis receberam uma área segura inferior consistente; Configurações,
  Importações, Gerenciar Produtos, Gerenciar Categorias, Consulta de Preços,
  Montagem, Orçamentos e edição de produto não deixam o último conteúdo preso
  atrás do rodapé. Um teste estrutural protege a medição correta dos
  `ScrollViewer`.
- `MainWindowViewModel.CreateAsync` tinha `ConfigureAwait(false)` nos awaits de
  configurações, catálogo e últimas importações. Em modo servidor (API real)
  isso derrubava o app no primeiro início com "The calling thread cannot
  access this object because a different thread owns it", porque a
  continuação voltava numa thread fora da UI e a construção do
  `MainWindowViewModel` cria objetos do Avalonia. Corrigido removendo os
  `ConfigureAwait(false)` e adicionando um guard defensivo com
  `Dispatcher.UIThread.CheckAccess()`/`InvokeAsync` em volta da construção
  final. Só foi encontrado rodando o executável publicado contra a API da VPS
  — build e os 239 testes (SQLite/mocks) sempre passaram mesmo com o defeito
  presente. A coluna de quantidade da montagem também estava estreita demais
  (80px) para o `NumericUpDown` mostrar o número; ajustada para 110px em
  `FlexibleListView.axaml`.
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
