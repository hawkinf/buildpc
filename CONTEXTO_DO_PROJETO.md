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

No estado documentado, a solução compila sem avisos e possui 258 testes
aprovados. O GitHub Actions roda build e testes em Windows e em Linux a cada
push e pull request (`.github/workflows/build.yml`) — o job Linux tem Docker
e executa de verdade os testes de integração do PostgreSQL
(`PostgresBuildPcRepositoryIntegrationTests`, via Testcontainers), que no
Windows local só se registram como inconclusivos por falta de Docker.

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

### Implantação do BuildPc.Web (cliente web, `precos.hawk.com.br`) — EM PRODUÇÃO

Configuração real em `/etc/buildpc-web.env` (confirmada em produção,
30/07/2026):

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://127.0.0.1:8126` — **obrigatório**; sem isso o
  Kestrel sobe na porta padrão 5000 e o Nginx (que espera 8126) devolve 502
- `BuildPc__BaseUrl=http://127.0.0.1:8125/` — direto pro Kestrel interno da
  API (não pelo `https://contaslite.hawk.com.br/buildpc-api/` público),
  já que os dois serviços vivem na mesma VPS
- `BuildPc__ApiKey` — mesma chave usada pela API
- `BuildPc__WebPassword` — senha única compartilhada da equipe
- `BuildPc__DataProtectionKeyPath=/var/lib/buildpc-web/keys` — sem isso o
  cookie de login não sobrevive a um restart do serviço (ver histórico da
  fase 9 abaixo)
- `DOTNET_EnableDiagnostics=0`

Arquivos de implantação:

- `deploy/buildpc-web.service`
- `deploy/nginx-precos.conf`
- `deploy/nginx-websocket-map.conf` (instalar em
  `/etc/nginx/conf.d/02-websocket-map.conf`, uma vez só no servidor)
- `deploy/deploy-web.sh`
- `deploy/rollback-web.sh`

O serviço roda como usuário dedicado `buildpc-web` (não o `buildpc` da API —
isolamento entre os dois processos) em `/opt/buildpc-web/current`.
`ReadWritePaths=/var/lib/buildpc-web` no `.service` é necessário porque
`ProtectSystem=strict` deixa o resto do sistema de arquivos só leitura, e o
Data Protection precisa gravar lá (usuário sem home). Site Nginx dedicado
(domínio próprio, TLS via `certbot --nginx -d precos.hawk.com.br`), não um
`location` dentro de `contaslite.hawk.com.br` como a API. Portas
confirmadas sem colisão: produção `127.0.0.1:8126`, teste de deploy `8130`
(a API usa `8125`/`8129`).

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

**Ações aplicadas no servidor em 30/07/2026:**

1. `deploy/deploy-api.sh` e `deploy/rollback-api.sh` copiados para
   `/opt/buildpc-api/` (root:root, 755). `rollback-api.sh` foi ajustado antes
   do envio para aceitar tanto `ANTERIOR=<caminho>` (formato do
   `ROLLBACK.txt` já em produção, de um deploy manual anterior) quanto o
   formato de caminho puro que `deploy-api.sh` grava — sem isso, o primeiro
   rollback pelo script novo falharia contra o arquivo antigo.
2. `deploy/backup-buildpc.sh` (com a validação `test -s` do dump) substituiu
   `/usr/local/sbin/backup-buildpc`. `deploy/buildpc-backup.service` (com
   `OnFailure=buildpc-backup-alert.service`) substituiu o `.service` ativo, e
   `deploy/buildpc-backup-alert.service` (novo) foi instalado em
   `/etc/systemd/system/`. `systemctl daemon-reload` +
   `systemd-analyze verify` sem erros nas duas unidades (os avisos que
   apareceram são de unidades alheias ao BuildPC — `superat-api.service`,
   `snapd.service`, `rc-local.service` — pré-existentes no servidor).
   Testado de verdade: `systemctl start buildpc-backup.service` gerou um
   dump novo e válido (142 KB, mesma ordem de tamanho dos anteriores) sem
   disparar o alerta; `systemctl start buildpc-backup-alert.service` isolado
   confirmou que o alerta grava no journal (`journalctl -t buildpc-backup`)
   com prioridade `err`.
   `deploy-api.sh`/`rollback-api.sh` **não foram executados** — só
   instalados; não havia release novo da API para publicar nesta ação.
3. `/opt/buildpc-api/current`, `ROLLBACK.txt` e o binário em produção
   (`fase4-20260729-195631`) não foram tocados. `/health` confirmado 200
   depois de tudo.

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

- Cliente web (30/07) — dois ajustes visuais na Montagem/Consulta de
  Preços. (1) A linha Validade/Desconto/Pagamento/Prazo de entrega (fig2)
  ganhou o mesmo padrão de cartão do resumo Custo/Venda/Lucro (fig1):
  fundo `#f8fafc`, borda `#e2e8f0`, cantos arredondados, sombra suave,
  rótulo pequeno em caixa alta cinza-azulado e divisores verticais entre
  campos — antes era uma linha de formulário simples sem estilo. Gravar/
  Limpar ficaram à esquerda e o cartão de condições à direita, na mesma
  linha (`.build-footer-row`, `justify-content: space-between`), pedido
  explícito do usuário ("fig2 do lado direito"). Novas classes
  `.build-terms-card`/`.build-term-stat`/`.build-term-label` (antes
  `.build-terms`/`.build-field-label` reaproveitados sem o visual de
  cartão). (2) Coluna "Categoria" removida da tabela de Consulta de Preços
  — redundante com o filtro de categoria já existente no topo da tela,
  reportado pelo usuário com print da tabela.
- **Bug corrigido (30/07): lista de produtos da Montagem mostrava preço de
  custo, não de venda.** O seletor de produtos (`PickerOptions` em
  `Montagem.razor`) ordenava e exibia `component.Price` — o preço de custo
  importado do catálogo — direto na tabela e no popup de hover, sem passar
  por `PricingCalculator.CalculateSalePrice`. O resto da tela (item já
  adicionado, total do orçamento) sempre usou o preço de venda calculado
  corretamente; só a lista de seleção antes de adicionar é que vazava
  custo. Reportado pelo usuário com print mostrando preços terminados em
  ",99" (padrão de importação, não a regra ",90"/degrau de 5 reais).
  Corrigido com um `SalePriceFor(component)` que aplica
  `_settings.MarginFor(component.Category)` — usado tanto na ordenação por
  preço quanto na célula da tabela e no `ProductHoverPreview`. 294/294
  testes (mudança é só de exibição/ordenação, sem lógica nova testável).
- Cliente web (30/07) — reorganização dos campos Validade/Desconto/
  Pagamento/Prazo de entrega na Montagem: reordenados (Validade, Desconto,
  Pagamento, Prazo de entrega), rótulos encurtados ("Validade",
  "Pagamento"), Validade e Prazo de entrega estreitados pra largura de 2
  dígitos, Pagamento com campo menor, e Desconto passou a ficar mascarado
  (••••) por padrão — toque revela o campo `R$` editável, perde o foco
  volta a mascarar. Altura/borda de todos os controles da linha
  normalizadas pra alinhar (`.build-field-narrow` nova classe); no mobile os
  campos voltam a 100% da largura (empilhados).
- **Mudança de regra de negócio (30/07), afeta Desktop e Web igualmente**:
  arredondamento do preço de venda. `PricingCalculator.RoundUpToNinetyCents`
  (Core — único lugar que implementa a regra, compartilhado por Desktop e
  Web desde a fase 1 do cliente web) mudava pra cima até o próximo real
  terminado em ",90" (a cada 1 real: 80,90; 81,90; 82,90...). Nova regra,
  pedida pelo usuário com exemplos exatos: os alvos válidos passam a ficar
  5 reais um do outro, alternando ",90" (4,90; 9,90; 14,90...
  84,90; 89,90...) — fórmula
  `Math.Ceiling((valor + 0,10) / 5) * 5 - 0,10`. Exemplo dado: 81,20 → antes
  seria 81,90, agora 84,90. Continua sendo **sempre arredondamento pra
  cima** (nunca reduz o preço abaixo do calculado, mesma garantia de
  antes) — só o tamanho do degrau mudou de 1 para 5 reais. Afeta
  automaticamente todo lugar que já usava essa função só por estar
  centralizada no Core: importação (Desktop e Web), adicionar item na
  Montagem (Desktop e Web), digitar preço manual, aplicar modelo com
  desconto, `PriceTableRowBuilder` (Consulta de Preços) — nenhum código
  novo nesses lugares, só a implementação compartilhada mudou. 8 testes do
  Desktop (`FlexibleListViewModelTests`, `PriceLookupViewModelTests`,
  `AssemblyTemplateTests`) tinham valores esperados fixos calculados sob a
  regra antiga — recalculados um a um pra regra nova, não eram bugs, só
  ficaram desatualizados. Novo `PricingCalculatorTests.cs` (11 casos,
  incluindo os exemplos exatos do usuário) — a regra de arredondamento
  nunca tinha teste dedicado antes de agora. 294/294 testes.
  `dist/x64/BuildPc.Desktop.exe` republicado (o Desktop pega a mudança só
  recompilando, mesma DLL do Core).
- Cliente web (30/07) — três ajustes pequenos no `RevealCost`/Montagem.
  (1) Tocar de novo enquanto o valor está revelado agora esconde na hora
  (`Reveal()` virou toggle de verdade), em vez de só esperar o tempo normal
  (5s) esgotar — antes um segundo toque não fazia nada
  (`if (_revealed) return;`). (2) Removida a legenda "toque para ver"
  (estava desalinhando linhas na tabela de itens/resumo, cada célula com
  altura diferente dependendo do estado) — **confirmado com o usuário antes
  de mexer** (`AskUserQuestion`, pela ambiguidade real entre "remover só o
  texto" e "remover a proteção inteira"): mantido o mascaramento com
  pontinhos e o clique pra revelar, só a legenda saiu, virou `title`
  (tooltip nativo do navegador ao passar o mouse) em vez de texto sempre
  visível. (3) Campo "Preço unit." da tabela de itens (o preço de
  **venda**, editável — não o custo, que continua só na coluna "Custo"
  mascarada) ganhou o mesmo prefixo "R$" visual (`build-currency-input`)
  já usado no Desconto. CSS órfão removido (`.reveal-cost-hint`, nos dois
  arquivos que a referenciavam). 282/282 testes (sem novos — ajuste de
  comportamento simples e estilo).
- Cliente web (30/07) — terceira rodada de ajuste na Montagem: bloco fixo
  do topo compactado (paddings/gaps/fontes reduzidos ~20-30% —
  `build-fixed-top`, `build-client-box`, `build-summary-header`), corrigida
  uma sobreposição visual no cabeçalho fixo da lista de produtos do
  seletor (`.build-picker-table thead th` não tinha `z-index` — sem isso,
  em alguns navegadores a linha rolando por baixo do cabeçalho `position:
  sticky` conseguia aparecer por cima dele; `z-index: 5` +
  `box-shadow: 0 1px 0` pra separação visual clara resolvem os dois
  problemas), e a ordem da tela invertida: agora a tabela de itens já
  adicionados vem **antes** do seletor de produto (categoria/busca/
  quantidade/Adicionar + lista de resultados) — antes era o contrário
  (seletor primeiro, itens depois). Os avisos de compatibilidade
  acompanharam a tabela de itens (fazem sentido junto do que já foi
  montado, não do seletor). 282/282 testes (sem novos — reorganização e
  estilo).
- Cliente web (30/07) — segunda rodada de ajuste no topo da Montagem,
  reposicionando o que a rodada anterior tinha feito: agora só
  Cliente/Telefone/Observações (`build-client-box`) + resumo de preço
  (`build-summary-header`) ficam **fixos no topo durante a rolagem**
  (`.build-fixed-top`, `position: sticky` no contêiner que envolve os
  dois — antes era só o resumo isolado); Desconto/Validade/Pagamento/
  Entrega **saíram do topo e foram para o final**, depois da tabela de
  itens, mantendo `HasItems` como antes (fazem mais sentido como "detalhes
  de fechamento" depois de montar tudo, não antes). Observações migrou
  pra dentro do quadro do cliente, como terceiro campo ao lado de
  Telefone: ficou mais larga (`flex: 2`) e bem mais baixa
  (`height: 2.4rem`, era `5.5rem`) — cabe numa linha ao lado dos outros
  dois campos em vez de ocupar uma seção inteira embaixo.

  Alinhamento e tipografia: todo rótulo de campo (Cliente/Telefone/
  Observações e Desconto/Validade/Pagamento/Entrega) passou a usar
  `<span class="build-field-label">` — antes era texto solto direto no
  `<label>`, sem como estilizar separado do input; agora usa a mesma
  linguagem visual do resumo (maiúsculas pequenas, cinza, letter-spacing)
  para as duas áreas ficarem visualmente coerentes. `build-client-box` e
  `build-summary-header` ganharam `max-width: 56rem` centralizado
  (`margin: 0 auto` via `align-items: center` do contêiner pai) em vez de
  esticar a largura toda — pedido explícito de "centralizado". No
  celular, `.build-fixed-top` volta a `position: static` (sticky
  permanente ocuparia espaço demais numa tela pequena, mesma decisão já
  tomada pro resumo isolado na rodada anterior). 282/282 testes (sem
  novos — reorganização e estilo).
- Cliente web (30/07) — reorganização do formulário da Montagem (pedido
  detalhado do usuário) e zebrado nas tabelas de produtos em todo o
  cliente web. Cliente/Telefone viraram a primeira coisa da página
  (`build-client-box`, retângulo com fundo levemente destacado), antes do
  resumo de preço — sempre visíveis, não dependem mais de `HasItems`.
  Desconto ganhou máscara "R$ x.xxx,xx" de verdade (preenche da direita
  pra esquerda, como caixa registradora): `type="text"` porque
  `type="number"` do navegador rejeitaria os separadores, dígitos
  extraídos a cada `@oninput` e reformatados em centavos
  (`OnDiscountInput`) — sem JS interop, só C#/Blazor (o cursor pula pro
  fim a cada tecla, efeito colateral conhecido desse tipo de máscara sem
  JS; funcional, não perfeito). Validade: rótulo do zero mudou de "sem
  prazo" pra "pronta entrega". Condições de pagamento virou `<select>`
  (À vista / Dinheiro/Pix / Parcelado no cartão) no lugar de texto livre.
  Condições de entrega virou dias (number) + Úteis/Corridos (`<select>`),
  combinados em `_deliveryTerms` (`"N dias úteis"`) só na hora de gravar —
  `ParseDeliveryTerms` tenta decompor de volta ao carregar um orçamento
  salvo antes dessa mudança; se o texto livre antigo não bater com o
  formato novo, os campos voltam ao padrão mas o texto original
  continua preservado no orçamento/PDF. Observações ficou mais estreita e
  mais alta (`height: 5.5rem`, largura menor) — já estava na posição certa
  (logo após Entrega), só precisava do reestilo.

  Zebrado (`nth-child(even)`, sem JS) nas linhas de produto: tabela de
  itens e seletor de produto da Montagem, tabela da Consulta de Preços, e
  tanto a lista de orçamentos quanto os itens dentro de um orçamento
  selecionado — pedido do usuário cobria "todo o orçamento", então
  estendido de forma consistente pras telas irmãs, não só onde foi citado
  literalmente. 282/282 testes (sem novos — reorganização de formulário e
  estilo puro).
- Cliente web (30/07) — resumo da Montagem redesenhado (feedback direto do
  usuário: "extremamente amador"). Antes era texto simples com labels
  inline; agora é um cartão de estatísticas centralizado
  (`build-summary-header`): 3 blocos (Custo total/Total de venda/Lucro)
  separados por divisórias verticais, rótulo pequeno em maiúsculas acima
  do valor grande (1.6-2rem), Total de venda em azul e maior que os
  outros (é o número voltado pro cliente), Custo em vermelho, Lucro em
  verde — tudo dentro de `RevealCost`/mascarado como antes. **Passou a
  aparecer sempre**, mesmo com o carrinho vazio (mostra zerado) — antes só
  renderizava depois do primeiro item adicionado (`@if (HasItems)` também
  envolvia o resumo; separei o resumo dessa condição, mantida só pro
  restante do formulário — desconto/cliente/etc. continuam fazendo sentido
  só depois de ter item).

  Lucro agora mostra o percentual junto (`TotalProfitPercent =
  round(lucro/custo × 100, 1)`, mesma fórmula do Desktop) — mas **dentro do
  mesmo toque-para-revelar**, não como texto separado sempre visível: o
  percentual sozinho já permitiria calcular o custo de volta a partir do
  Total de venda (esse sim sempre visível), então mascarar só o valor em
  R$ e deixar a % solta anularia a proteção. `RevealCost` (Core, fase 4)
  ganhou um parâmetro `Suffix` opcional pra isso — revelado junto com o
  valor principal, não antes. Estilo do `RevealCost` dentro do cartão
  sobrescrito via `::deep` (CSS isolation não alcança um componente-filho
  por padrão) só dentro de `.build-summary-header`, sem afetar as outras
  instâncias (tabela de itens, Consulta de Preços). No celular, o cartão
  vira coluna única e sai do modo `sticky` (empilhado ficaria alto demais
  fixo permanentemente na tela). 282/282 testes (sem novos — só
  reorganização visual e uma fórmula já coberta indiretamente pelos
  cálculos de `PricingCalculator`).
- Cliente web (30/07) — correção sobre o item anterior: só tinha movido as
  3 linhas de total (custo/venda/lucro) pro topo da Montagem, mas a
  captura de tela do usuário mostrava o bloco inteiro (desconto, validade,
  condições de pagamento/entrega, observações, cliente, telefone, gravar/
  limpar) — interpretação incompleta do pedido original, apontada
  diretamente pelo usuário. `build-terms`/`build-client`/`build-actions`/
  `build-confirm` movidos também, logo depois do resumo de 3 linhas e
  antes do seletor de produto — mesma condição `HasItems`, só que agora
  tudo faz parte do mesmo bloco no topo da página. Só o resumo de 3 linhas
  continua `sticky` (compacto o bastante pra ficar fixo sem tomar muito
  espaço da tela); o bloco maior de campos rola com a página normalmente.
  282/282 testes (sem novos — reorganização pura de markup).
- Cliente web (30/07) — dois ajustes pedidos pelo usuário via captura de
  tela da Montagem: (1) resumo (custo total/total de venda/lucro) movido
  pro topo da página, logo abaixo do título — antes só aparecia depois da
  tabela de itens, obrigando rolar a tela toda pra ver o total durante a
  montagem. Ganhou `position: sticky; top: 0` (`.build-summary-header`):
  fica visível o tempo todo enquanto rola a lista de itens, mais perto do
  comportamento do Desktop (barra de total sempre visível, sem rolagem
  possível numa janela única). (2) seletor de categoria da Montagem
  ganhou contagem entre parênteses (`PickerCountFor`) — antes não mostrava
  nenhuma; e a de Consulta de Preços (`CountFor`, já existia) passou a
  respeitar o texto de busca digitado, em vez de sempre mostrar o total da
  categoria inteira — mesmo comportamento do `PriceLookupViewModel` do
  Desktop, onde a contagem por categoria reflete a busca ativa. Mesma
  lacuna existia nas duas telas; corrigida nas duas de uma vez por
  consistência, embora só a Montagem tivesse sido citada. 282/282 testes
  (sem novos — reorganização de UI e correção de contagem sobre catálogo
  já carregado).
- Cliente web (30/07) — seletor de produto da Montagem trocou o `<select>`
  nativo (uma linha de texto, sem foto) por uma lista igual à tabela da
  Consulta de Preços, a pedido do usuário ("tem que ser igual a consulta de
  preços"). `<select><option>` nativo não tem como mostrar imagem nem popup
  por item — não dava pra chegar em paridade visual sem trocar o controle.
  Nova tabela (`build-picker-table`, `max-height: 20rem` com scroll interno
  — categoria filtrada ainda pode ter uma paisagem de produtos): miniatura
  por linha, nome envolto em `ProductHoverPreview` (mesmo popup da fase
  anterior), ordenar por nome/preço (`PriceTableSortMode`, reaproveitado do
  Core em vez de criar um enum novo — mesmo shape que a Consulta de Preços
  já usa), favoritos sempre no topo independente da ordenação (igual ao
  seletor do Desktop). Clicar numa linha seleciona (`_pickerComponentId`,
  linha destacada via classe `.selected`) — fluxo de quantidade+Adicionar
  continua igual, só a lista de escolha mudou. Removida a prévia de imagem
  avulsa ao lado do seletor (ficou redundante — a miniatura já aparece em
  cada linha da lista agora). 282/282 testes (sem novos — só reorganização
  de UI sobre catálogo já carregado, nenhuma lógica de negócio nova).
- Cliente web (30/07) — URLs de importação por categoria agora persistem no
  servidor, fechando a lacuna que ficou documentada (e explicada ao
  usuário) quando a tela de Importações foi adicionada: antes, cada edição
  só ia pro arquivo local do Desktop (`buildpc.config.json`), e a Web
  nem lia nem gravava nada — cada sessão da Web começava do zero. Nova
  entrada `import_source_urls` na mesma tabela `business_settings`
  (Postgres) / `business_settings` (SQLite local), com **chave própria**
  (não dentro do blob de `BusinessSettings`) de propósito: gravar uma nunca
  sobrescreve a outra, mesmo risco de "UPDATE substitui o registro inteiro"
  que já tinha descartado colocar isso dentro de `BusinessSettings` na fase
  de Importações. Novo par de métodos em `IQuoteRepository`
  (`GetImportSourceUrlsAsync`/`SaveImportSourceUrlsAsync`), implementado
  nos três lugares que já implementam a interface (`QuoteRepository`
  SQLite, `PostgresBuildPcRepository`, `BuildPcApiClient` via
  `GET`/`PUT /settings/import-sources`). Formato de chave
  (`"{sourceKey}:{category}"`, ex. `"kabum:Processor"`,
  `"kabum-hd:HardDrive"`) extraído pra `ImportKeys.SourceUrlKey` (Core) —
  antes duplicado entre o método privado `ImportSourceConfigurationKey` do
  Desktop e o que eu ia escrever de novo na Web; "mover, não duplicar" de
  novo (é diferente do formato de `ImportKeys.For`, usado só pro
  metadado de "última importação").

  Desktop: `MainWindowViewModel.SaveApplicationConfiguration` (chamado a
  cada edição de URL, já existia) agora também empurra as URLs pro
  servidor via `_ = PushImportSourceUrlsAsync(...)` — *fire-and-forget*,
  erros engolidos (`SqliteException`/`InvalidOperationException`), porque
  a gravação local já é o que importa pro app continuar funcionando
  offline; uma falha de rede ao sincronizar não pode incomodar quem só
  queria editar uma URL. Deliberadamente **não** fiz o Desktop *ler* do
  servidor na inicialização (ficaria bidirecional só entre sessões da
  própria Web) — mexer no startup do Desktop, já em uso diário de verdade,
  pareceu risco desnecessário pra um recurso secundário; se um dia isso
  incomodar, dá pra adicionar depois. Web: `Importacoes.razor` busca do
  servidor no `OnInitializedAsync` (sobrepõe o padrão embutido só onde o
  servidor tem valor) e grava de volta a cada edição de URL (`@onchange`),
  mesmo espírito best-effort do Desktop.

  Implantado e testado ao vivo, nessa ordem (API antes da Web, já que a Web
  depende dos endpoints novos): API republicada (endpoints
  `GET`/`PUT /settings/import-sources` testados direto contra o Postgres de
  produção via `curl` — gravei um valor, li de volta, confirmei que
  `GetSettings` continuou intacto), Web republicada (confirmei que uma URL
  gravada direto na API aparece na tela ao recarregar — sobrepõe o padrão
  embutido), Desktop recompilado localmente (`dist/x64/BuildPc.Desktop.exe`
  — o usuário roda essa build para pegar o novo comportamento de push).
  **Um problema à parte durante o deploy**: a primeira transferência do
  release da Web via `tar | ssh` foi interrompida no meio (conexão SSH
  caiu) e gerou um binário corrompido no servidor — `deploy-web.sh` pegou
  isso corretamente (o teste de `/health` na porta de teste falhou porque o
  runtime .NET não conseguiu carregar a DLL truncada) e **não trocou o
  symlink**, produção continuou no ar no release anterior o tempo todo;
  bastou apagar o diretório corrompido e retransmitir. 282/282 testes (5
  novos: round-trip SQLite e Postgres da nova entrada — confirma
  isolamento de `BusinessSettings` nos dois bancos —, checagem de
  autenticação do endpoint novo, e dois testes de `ImportKeys.SourceUrlKey`
  incluindo a diferença de formato contra `ImportKeys.For`).
- Cliente web (30/07) — corrigida a URL de importação de HD, que eu tinha
  montado errado na fase anterior (busquei na web em vez de checar o
  próprio código do Desktop). `MainWindowViewModel.cs:330-337` já tinha a
  categoria HardDrive com um `ImportSource` explícito — path
  `/hardware/disco-rigido-hd` (sem o `/pessoal/interno` que eu tinha
  inventado) e, importante, `sourceKey: "kabum-hd"` — diferente do
  `"kabum"` usado por todas as outras categorias. `Importacoes.razor`
  corrigido nos dois pontos: URL certa e um `SourceKeyFor(category)` que
  usa `"kabum-hd"` só para HardDrive (as outras continuam `"kabum"`) — sem
  isso, `GetLastImportsAsync` trataria a mesma categoria como duas origens
  diferentes dependendo de qual cliente importou por último. Lição: quando
  o comportamento de referência já existe no código do próprio projeto,
  checar o código primeiro é mais confiável que buscar na web. 277/277
  testes.
- Cliente web (30/07) — responsividade mobile em todo o `BuildPc.Web`.
  Reset global `box-sizing: border-box` (`app.css`) evita que padding/
  borda estourem a largura do container — causa comum de scroll
  horizontal indesejado no celular. Em cada tela, breakpoint
  `@media (max-width: 640px)`: barras de filtro/formulário empilham em
  coluna única (`flex-direction: column`, campos `width: 100%`); tabelas
  com muitas colunas (Consulta de Preços, itens da Montagem, lista de
  Orçamentos) ganharam um wrapper `overflow-x: auto` em vez de forçar
  colunas a espremer; `Orcamentos`' layout lista+detalhe lado a lado já
  quebrava sozinho (`flex-wrap` + `min-width` em cada lado), só reforçado
  pra ocupar 100% da largura quando empilhado. Cabeçalho (`MainLayout`)
  ganhou `flex-wrap` na nav pra não cortar links em telas estreitas.
  `ProductHoverPreview` (popup de hover) muda de "ancorado à direita do
  item" pra "centralizado na tela" (`position: fixed`) abaixo de 640px —
  ancorado à direita estouraria a tela em celular, e hover em touch é
  pouco confiável entre navegadores de qualquer forma. Sem mudança de
  código C#/Razor além do wrapper `<div>` em volta de três tabelas — só
  CSS. 277/277 testes (nenhum novo — puramente visual, sem lógica pra
  testar). Não verificado em dispositivo real nem por screenshot
  (ferramenta de automação de navegador não disponível nesta sessão) —
  vale conferir visualmente num celular de verdade.
- Cliente web (30/07) — popup de hover com foto e características, pedido
  do usuário pra replicar o `ProductHoverPreview` do Desktop (`Controls/
  ProductHoverPreview.axaml`). Componente novo,
  `Components/Shared/ProductHoverPreview.razor`: mesmos 4 campos do
  Desktop (imagem, título, descrição, preço — nada de socket/wattage/
  categoria, o popup do Desktop também não mostra isso), aplicado nas três
  telas (nome do produto na Consulta de Preços, item na tabela da
  Montagem, item na lista de Orçamentos). Implementado só com CSS (sem
  JS/IJSRuntime): `transition-delay` no `:hover` aproxima o atraso de
  abertura de 220ms do Desktop sem precisar de interop; não replica a
  lógica de "só um popup aberto por vez" nem o posicionamento seguindo o
  cursor (ancorado à direita do item via `position:absolute`, não
  `Placement="Pointer"``) — simplificação aceitável pra CSS puro. Preço é
  recebido como string já formatada, mesma convenção do Desktop.
  `ProductPriceTableRow` (Core) ganhou `Description` como parâmetro
  posicional opcional no fim (não quebra os call sites existentes,
  inclusive nos testes) — só usado pelo popup, não entra no PDF.
  `Montagem`/`Orçamentos` já tinham `Description` disponível em
  `PcComponent`/`SavedQuoteItem`, sem mudança de modelo.

  URLs de importação por categoria (fase anterior) agora vêm
  **pré-preenchidas** com os mesmos valores já configurados no
  `buildpc.config.json` do Desktop (conferido: 11 das 12 categorias têm
  URL configurada — falta `HardDrive`, que também não está configurada no
  Desktop, então não inventei uma). Continuam não persistidas entre
  sessões (mesmo motivo já documentado: risco de o Desktop apagar sem
  querer se isso fosse pra `BusinessSettings`) — editar e recarregar volta
  pro padrão do Desktop, não fica em branco. 277/277 testes.
- Cliente web (30/07) — Importações adicionada ao `BuildPc.Web`
  (`/importacoes`), fora do plano original de 9 fases: a exclusão inicial
  ("Importações fica só no Desktop, depende de scraping bloqueado por
  CORS") valia para uma abordagem client-side/WASM, mas o `BuildPc.Web` é
  Blazor **Server** — todo o código, inclusive as chamadas HTTP pro Kabum,
  roda no servidor, exatamente como já roda hoje no Desktop (só que na VPS
  em vez da máquina do usuário). CORS é uma política do navegador; não se
  aplica a uma chamada servidor-servidor. `KabumCatalogImporter` já vivia
  em `BuildPc.Core` (puro `HttpClient`, sem nada de Windows/Avalonia/
  headless-browser — o catálogo já vem embutido no HTML via SSR do Next.js
  da Kabum, `__NEXT_DATA__`), então a página só chama o que já existia.
  Registrado via `AddHttpClient<KabumCatalogImporter>` (cliente tipado, não
  `new HttpClient()` direto — evita esgotamento de socket num processo de
  longa duração, diferente do Desktop que abre um por sessão).

  Decisão consciente: as URLs configuradas por categoria **não são
  persistidas** (nem em `BusinessSettings`, nem em nenhum lugar
  compartilhado) — só vivem no estado do componente durante a sessão.
  Motivo: `BusinessSettings` já é sincronizado via API
  (`GetSettingsAsync`/`SaveSettingsAsync`), mas o Desktop grava a URLs de
  importação num arquivo **local** (`buildpc.config.json`,
  `BuildPcApplicationSettingsStore`) totalmente separado de
  `BusinessSettings` — e `PostgresBuildPcRepository.SaveSettings` faz um
  UPSERT que **substitui o registro inteiro** (não faz merge por campo).
  Se `ImportSourceUrls` fosse adicionado a `BusinessSettings`, qualquer
  save do Desktop em modo servidor (ex.: editar margem) usaria sua cópia
  local de `BusinessSettings` — que nunca teria essa chave populada — e
  apagaria silenciosamente as URLs que a Web tivesse configurado. Persistir
  direito exigiria também atualizar o Desktop pra ler/preservar esse campo
  no round-trip, fora do escopo desta adição pontual. Fica documentado como
  melhoria futura, não como bug.

  Mesmo fluxo do Desktop: card por categoria (`ProductCategoryDefinition`
  ativas), URL editável, confirmação em duas etapas antes de importar (a
  operação é destrutiva — substitui o catálogo da categoria), progresso via
  `IProgress<KabumImportProgress>`, "Importar tudo" roda as categorias com
  URL preenchida sequencialmente (mesmo motivo do Desktop: não fazer rajada
  concorrente contra a Kabum). Usa a mesma chave de origem `"kabum"` que o
  Desktop (`ImportKeys.For`), então `GetLastImportsAsync` fica consistente
  entre os dois clientes. Trava de concorrência (`pg_advisory_xact_lock`
  por categoria+origem) já existe em `PostgresBuildPcRepository`, então
  duas pessoas importando a mesma categoria ao mesmo tempo (Desktop e Web,
  ou duas abas da Web) já é seguro sem mudança nenhuma. 277/277 testes
  (sem novos — a página só orquestra `KabumCatalogImporter` e
  `IComponentCatalogRepository`, ambos já cobertos). Único risco genuíno
  (não é CORS): todas as importações agora saem do mesmo IP da VPS em vez
  de IPs residenciais distribuídos dos desktops da equipe — o
  autothrottling já embutido no importer (350ms entre páginas, retry com
  backoff em 429/5xx) não muda, mas vale observar se a Kabum passar a
  bloquear esse IP com uso mais frequente.
- Cliente web (30/07), fase 9/9 — **em produção**: `https://precos.hawk.com.br`
  está no ar, autenticado, servindo as três telas contra o catálogo/
  orçamentos reais (1420 produtos, 2 orçamentos — confere com a auditoria do
  lote 4). Usuário de sistema `buildpc-web` criado; `/etc/buildpc-web.env`
  (`BuildPc__BaseUrl=http://127.0.0.1:8125/` — direto pro Kestrel interno da
  API, sem passar pelo Nginx/TLS público, já que os dois processos vivem na
  mesma VPS; `BuildPc__ApiKey` igual ao da API; `BuildPc__WebPassword`
  definida pelo usuário). Certificado TLS emitido via
  `certbot --nginx -d precos.hawk.com.br -n --agree-tos --redirect`
  (reaproveitou a conta ACME já registrada nesta VPS — sem `--email`,
  mesmo padrão dos outros `*.hawk.com.br`); DNS já estava propagado
  (proxied pelo Cloudflare, confirmado que o desafio HTTP-01 passa
  normalmente, igual aos subdomínios irmãos). Site Nginx dedicado inclui
  `snippets/hawk-security.conf` (cabeçalhos de segurança padrão desta VPS).

  **Dois bugs reais só apareceram no primeiro deploy de verdade** (nenhum
  dos dois é coberto pelos testes automatizados — são puramente de
  configuração de produção):
  1. `/etc/buildpc-web.env` faltava `ASPNETCORE_URLS` — o serviço systemd
     caía no padrão do Kestrel (`localhost:5000`) em vez da porta 8126 que
     o Nginx espera. A fase de teste do `deploy-web.sh` não pegou isso
     porque ela mesma sobrescreve `ASPNETCORE_URLS` explicitamente antes de
     subir o binário de teste — só o serviço systemd real (via
     `EnvironmentFile=`) dependia do valor do arquivo.
  2. O usuário de sistema `buildpc-web` não tem diretório home
     (endurecimento intencional do systemd), então o Data Protection do
     ASP.NET Core caía para uma chave **efêmera** — cada reinício do
     serviço invalidaria o cookie de login de todo mundo. Corrigido de
     verdade no código: `Program.cs` agora lê
     `BuildPc:DataProtectionKeyPath` (só quando configurado — em dev local
     sem essa variável, usa o comportamento padrão) e chama
     `AddDataProtection().PersistKeysToFileSystem(...)`. Em produção aponta
     pra `/var/lib/buildpc-web/keys`, que precisou de
     `ReadWritePaths=/var/lib/buildpc-web` no `buildpc-web.service` (o
     `ProtectSystem=strict` deixa o resto do sistema de arquivos só
     leitura). Armadilha adicional descoberta na prática: a fase de teste
     do `deploy-web.sh` roda como root (não como `buildpc-web`) e, sem
     tratamento, escreveria uma chave *dona de root* nesse mesmo diretório
     persistente — o serviço real então não conseguia ler
     (`UnauthorizedAccessException` no primeiro login após qualquer
     deploy). `deploy-web.sh` corrigido pra exportar
     `BuildPc__DataProtectionKeyPath=""` só durante o teste descartável,
     deixando o processo de teste cair no repositório padrão (que pra
     root vira `/root/.aspnet/DataProtection-Keys`, sem tocar no
     diretório de produção). **Confirmado corrigido**: sessão sobrevive a
     `systemctl restart buildpc-web` (testado ao vivo).

  Verificação em produção (todas ao vivo, via `curl` com cookie real):
  login rejeita senha errada, aceita a certa, `/` redireciona pra
  `/montagem`, as três telas respondem 200 com dados reais (sem
  "Falha ao carregar"), logout revoga o cookie. PDF de orçamento (dado
  real): 200, ~37 KB, `%PDF` válido, rápido. PDF da tabela de preços
  **sem filtro de categoria** (1420 produtos, baixa imagem de cada um):
  200, mas ~1-2 min e 22 MB/110 páginas — lento por natureza (não é bug;
  filtrar por categoria antes de exportar é o caminho normal). Memória do
  serviço ficou em ~310-330 MB sob teste de concorrência leve (9
  requisições simultâneas simulando abas); memória "available" do sistema
  caiu de ~1,1 GB pra ~870 MB sob essa carga — **VPS com pouca folga**
  (2,4 GB totais, API+Web+Postgres+Nginx+outro serviço FastAPI na porta
  8100 já dividindo o que sobra), vale monitorar se o uso real crescer.
  Ensaio de rollback feito de verdade: `rollback-web.sh` reverteu pro
  release anterior (que ainda tinha os dois bugs acima, já que foi
  publicado antes das correções — confirmou que o rollback em si funciona,
  mecânica idêntica ao `rollback-api.sh`), depois `deploy-web.sh` reaplicou
  o release corrigido. Portas confirmadas sem colisão via `ss -tlnp` antes
  de instalar (8125/8129 da API livres, nada em 8126/8130). Serviço
  habilitado no boot (`systemctl enable`). Plano completo (9/9 fases)
  fechado.
- Cliente web (30/07), fase 8/9 — scripts de deploy do `BuildPc.Web`,
  espelhando quase literalmente `deploy-api.sh`/`rollback-api.sh` (lote 4):
  `deploy/deploy-web.sh` só troca o symlink `current` depois do release
  novo responder `/health` numa porta de teste (`8130`); `deploy/
  rollback-web.sh` usa o mesmo `ROLLBACK.txt` gravado pelo deploy.
  `deploy/buildpc-web.service` roda como usuário dedicado `buildpc-web`
  (não reaproveita o `buildpc` da API — mesmo princípio de isolamento),
  `After=...buildpc-api.service` sem `Requires=` (Web deve subir mesmo se a
  Api estiver momentaneamente fora — cada página já trata
  `InvalidOperationException` da API isoladamente, fases 5-7). `deploy/
  nginx-precos.conf` é um site **dedicado** (domínio próprio, diferente do
  `location` que a API usa dentro de `contaslite.hawk.com.br`) — bloco HTTP
  puro, pensado pra rodar `certbot --nginx -d precos.hawk.com.br` DEPOIS de
  instalado (o certbot edita o próprio bloco pra adicionar TLS + redirect,
  não criar um bloco HTTPS à mão antes). Inclui os cabeçalhos
  `Upgrade`/`Connection $connection_upgrade` que o circuito SignalR do
  Blazor Server exige (sem eles a UI trava no modal "Reconectando..." da
  fase 4) — o mapa `$connection_upgrade` só pode viver no `http{}` do
  `nginx.conf` principal, então o arquivo documenta conferir se já existe
  (o plano original menciona um serviço de chat na VPS que já usa
  WebSocket) em vez de arriscar duplicar a diretiva. Portas escolhidas
  (produção `8126`, teste de deploy `8130`) evitam as já documentadas da
  API (`8125`/`8129`) — **a confirmar contra `ss -tlnp` na VPS antes do
  primeiro deploy real da fase 9**, não verificado ao vivo nesta fase.
  `/etc/buildpc-web.env` esperado: `BuildPc__BaseUrl`, `BuildPc__ApiKey`
  (mesma chave da API), `BuildPc__WebPassword` (senha da equipe, fase 3),
  `ASPNETCORE_ENVIRONMENT=Production`. Nada de código mudou nesta fase —
  277/277 testes, só arquivos de infraestrutura.
- Cliente web (30/07), fase 7/9 — tela de Montagem (`Montagem.razor`,
  `/montagem`), a mais complexa, feita por último de propósito (reusa tudo
  das fases 4-6). Fluxo: seletor de categoria/produto (`ProductFilter.
  Matches`, favoritos primeiro), quantidade (`QuantityRange.Clamp`),
  adicionar/remover/reordenar itens, preço unitário editável (arredonda
  pra ",90" via `PricingCalculator.RoundUpToNinetyCents` e nunca desce da
  margem mínima de 15% — mesmo piso que a Fase 1 já tinha, agora aplicado
  na digitação), desconto/validade (0-365 dias)/condições de pagamento e
  entrega, avisos de compatibilidade (`CompatibilityService.Evaluate`
  sobre um `PcBuild` reconstruído a cada mudança, só Warning/Error
  aparecem — puramente informativo, nunca bloqueia salvar/exportar, igual
  ao Desktop), nome/telefone do cliente (`PhoneNumberFormatter.
  FormatBrazilian` a cada edição), gravar orçamento
  (`IQuoteRepository.SaveQuoteAsync`), exportar PDF (mesmo endpoint
  `/pdf/orcamento/{id}` da fase 6 — só habilitado depois de gravar sem
  alterações pendentes, mesmo gate `CanExport` do Desktop), revelar custo
  por item e nos totais (`RevealCost` da fase 4, um por valor sensível).
  Modelos de montagem: listar/aplicar (sempre contra o catálogo atual —
  preço/margem nunca ficam congelados; item cujo id sumiu do catálogo é
  ignorado silenciosamente com aviso de quantos foram pulados, igual ao
  Desktop)/salvar/excluir. "Abrir na Montagem" e "Duplicar" (links criados
  na fase 6) resolvidos via `[SupplyParameterFromQuery]` (`editar`/
  `duplicar`) — a página busca o orçamento de novo em `GetQuotesAsync()`
  (não há estado compartilhado entre circuitos Blazor Server distintos) e,
  no caso de duplicar, zera `Id`/cliente mas mantém itens/desconto/
  validade/condições (mesmo comportamento do `LoadQuoteAsCopy` do
  Desktop). Erros de margem mínima do Core (`QuoteValidation.
  EnsureMinimumMargin`, reforço server-side independente do clamp da
  interface) capturados como `ArgumentException` e mostrados como
  mensagem de status. `/` (antes o placeholder de conectividade da fase 2)
  agora redireciona pra `/montagem` — ponto em aberto do plano, resolvido
  agora que as três telas existem. 277/277 testes (sem novos — a página
  só orquestra serviços do Core já cobertos: `ProductFilter`,
  `PricingCalculator`, `CompatibilityService`, `QuoteValidation`,
  `PhoneNumberFormatter`). Testado manualmente via `curl` com cookie:
  `/montagem` carrega com erro tratado quando a API não responde, exige
  login, `/` redireciona.
- Cliente web (30/07), fase 6/9 — tela de Orçamentos (`Orcamentos.razor`,
  `/orcamentos`). Lista + detalhe (mesmo padrão mestre-detalhe do
  `QuoteManagerViewModel` do Desktop): filtro por período
  (`QuoteFilter.IsInPeriod`) e busca com debounce de 300ms
  (`QuoteFilter.Matches`), excluir com confirmação em duas etapas,
  "Abrir na Montagem" e "Duplicar" — como a tela de Montagem só chega na
  fase 7, os dois viram links pra `/montagem?editar={id}` e
  `/montagem?duplicar={id}` que ainda não resolvem (mesma situação dos
  links de nav criados na fase 4): o handoff de estado entre as duas
  páginas é só esse id na query string — a Montagem vai recarregar o
  orçamento do zero via `GetQuotesAsync()`, não hà nada compartilhado em
  memória entre circuitos Blazor Server diferentes. PDF é outro endpoint
  minimal-API (`GET /pdf/orcamento/{id:guid}`, mesmo padrão da fase 5):
  como `IQuoteRepository` não tem "buscar por id", filtra a lista completa
  de `GetQuotesAsync()` em memória (orçamento não tem volume que
  justifique um endpoint dedicado na API) e usa `QuotePdfService.Export`
  (não o `PdfPreviewService` do Desktop, que grava em pasta temporária
  local — sem sentido num servidor) escrevendo num arquivo temporário,
  lendo os bytes e apagando antes de responder. 277/277 testes (sem
  novos — a página só orquestra `QuoteFilter`, já coberto). Testado
  manualmente via `curl` com cookie: `/orcamentos` carrega, PDF exige
  login (302 sem cookie) e falha só pela API de teste inalcançável
  (mesma `InvalidOperationException` de sempre).
- Cliente web (30/07), fase 5/9 — primeira tela real: Consulta de Preços
  (`Precos.razor`, `/precos`). `PriceTableRowBuilder` (novo,
  `BuildPc.Core.Services` — mesmo motivo do `StaffPasswordValidator` na fase
  3: lógica pura, sem nada específico do Blazor, então fica testável sem
  puxar `BuildPc.Web` pro projeto de testes) filtra
  (`ProductFilter.Matches`), aplica custo/venda
  (`PricingCalculator.CalculateSalePrice` + `BusinessSettings.MarginFor`) e
  ordena o catálogo — usado tanto pela página quanto pelo endpoint de PDF,
  então tela e PDF nunca mostram tabelas diferentes. Busca por texto usa
  debounce de 300ms (mesmo padrão de `CancellationTokenSource` do
  `RevealCost` da fase 4, não o `DebounceTimer` do Desktop). Alternância
  custo/venda: modo "Custo" envolve cada preço em `RevealCost` (consumidor
  real do componente da fase 4); modo "Venda" mostra direto (preço de
  cliente, não é dado sensível). Exportação em PDF é um link
  (`<a href>`) pro endpoint minimal-API `GET /pdf/tabela-precos` — não tenta
  transmitir o PDF pelo circuito SignalR (mesma decisão já prevista no plano
  pra fase 6). O endpoint reconstrói a tabela a partir de query string
  (categoria/busca/ordenação/modo de preço) usando o mesmo
  `PriceTableRowBuilder`, grava num arquivo temporário
  (`ProductPriceTablePdfService.ExportAsync` só sabe escrever em disco),
  lê os bytes e apaga o arquivo antes de responder. 277/277 testes (9 novos,
  `PriceTableRowBuilderTests` — filtro por categoria/texto, os 4 modos de
  ordenação, custo vs. venda, nome de categoria configurado). Testado
  manualmente via `curl` com cookie: `/precos` carrega (erro tratado
  quando a API não responde), `/pdf/tabela-precos` exige login (302 sem
  cookie) e falha só por causa da API de teste inalcançável (confirmado no
  log — mesma `InvalidOperationException` de sempre, não bug de rota).
- Cliente web (30/07), fase 4/9 — layout compartilhado e componente de
  revelar custo. `MainLayout.razor` ganhou cabeçalho com nav
  (Montagem/Consulta de Preços/Orçamentos, ainda sem página — fases 5-7) e
  "Sair" (form POST pra `/account/logout`), condicionados por
  `<AuthorizeView>` — a página de login não mostra nav nem logout (renderiza
  a mesma `MainLayout` porque não define `@layout` próprio, mas o
  `<AuthorizeView>` esconde o conteúdo de `<Authorized>` para usuário
  anônimo). Links da nav usam `NavLink`; estilo `.active` exigiu
  `::deep` no CSS isolado do componente (`NavLink` é um componente-filho,
  CSS isolation por padrão não alcança a marcação dele sem esse
  combinador). Modal de reconexão do Blazor Server (`ReconnectModal.razor`,
  gerado pelo template) traduzido pra pt-BR — plano previa isso de
  propósito ("volta e meia vai cair Wi-Fi em tablet"). `RevealCost` (novo,
  `Components/Shared`) é o componente reutilizável de "toque pra revelar
  custo": ao clicar mostra o valor formatado (`CultureHelpers.
  BrazilianCulture`) por 5 segundos (parâmetro `RevealSeconds`) e volta a
  mascarar sozinho — decisão já tomada no plano de usar "toque = revela por
  N segundos" em vez de tentar replicar o "segurar" do Desktop (mais
  confiável em touch, onde toque longo costuma abrir menu de contexto).
  Ainda sem consumidor real (fases 5-7). 268/268 testes (sem novos —
  cobertura de componente Blazor ainda em aberto pra v1, conferido
  manualmente via `dotnet run` + `curl` com cookie: nav/logout ausentes em
  `/login`, presentes na home autenticada).
- Cliente web (30/07), fase 3/9 — autenticação por cookie no `BuildPc.Web`.
  `StaffPasswordValidator` (novo, `BuildPc.Core.Services` — não é específico
  do Blazor, então testável sem referenciar `BuildPc.Web` no projeto de
  testes; referenciar os dois causaria `CS0433` porque `BuildPc.Api` e
  `BuildPc.Web` são top-level statements sem namespace e cada um gera seu
  próprio tipo `Program` implícito no namespace global — ambíguo se ambos
  forem referenciados no mesmo projeto) usa `PasswordHasher<T>`
  (`Microsoft.Extensions.Identity.Core`, novo pacote do Core): o hash da
  senha configurada é calculado uma vez na inicialização,
  `VerifyHashedPassword` compara cada tentativa de login contra ele.
  `BuildPc:WebPassword` segue a mesma convenção de variável de ambiente
  (`BuildPc__WebPassword`) das demais chaves. Cookie de autenticação
  (`CookieAuthenticationDefaults`) com expiração de 10h e renovação
  deslizante; `ForwardedHeadersOptions` confia só no proxy loopback (Nginx
  na mesma VPS) para o cookie ganhar o atributo `Secure` em produção sem
  quebrar o `dotnet run` local por HTTP. `[Authorize]` global via
  `@attribute` em `_Imports.razor`, com `[AllowAnonymous]` explícito em
  `Login.razor`/`NotFound.razor`/`Error.razor`. `Routes.razor` trocou
  `<RouteView>` por `<AuthorizeRouteView>` com `<NotAuthorized>` → um
  componente `RedirectToLogin` (padrão oficial do template Blazor + Identity
  — necessário porque, dentro de um circuito Server já aberto, a navegação
  do próprio Blazor não passa de novo pelo middleware de autorização do
  ASP.NET Core). O formulário de login é uma página SSR estática (sem
  `@rendermode`) que faz POST comum para `/account/login` — teve que ser um
  endpoint minimal-API separado da própria página `/login` (não
  `POST /login` direto: colidia com o endpoint da página Razor,
  `AmbiguousMatchException` em tempo de execução) porque `SignInAsync`
  grava o cookie no cabeçalho da resposta HTTP, e uma resposta de circuito
  Blazor Server interativo já foi iniciada — não dá pra escrever cabeçalho
  depois. `/account/login` e `/account/logout` usam `.DisableAntiforgery()`
  (mesmo padrão do template oficial de Identity: não há sessão prévia no
  login, e o logout precisa funcionar mesmo com token expirado). Testado
  manualmente via `curl` com cookie jar: redireciona sem cookie, aceita/
  rejeita senha, autoriza com cookie válido, `/account/logout` revoga.
  268/268 testes (7 novos, `StaffPasswordValidatorTests`).
- Cliente web (30/07), fase 2/9 — esqueleto do projeto `BuildPc.Web`
  (`dotnet new blazor --interactivity Server --empty --all-interactive`,
  adicionado à solução). `Program.cs` registra `BuildPcApiClient` via DI
  (singleton) contra `IComponentCatalogRepository`/`IQuoteRepository`/
  `IAssemblyTemplateRepository`, lendo `BuildPc:BaseUrl`/`BuildPc:ApiKey` da
  configuração — mesma convenção de variável de ambiente
  (`BuildPc__BaseUrl`/`BuildPc__ApiKey`) já usada pela `BuildPc.Api`, sem
  persistência local de chave como no Desktop. Endpoint `/health` público
  (necessário pro gate de healthcheck do deploy, fase 8/9). Página inicial
  (`Home.razor`) é um placeholder que injeta `IComponentCatalogRepository` e
  mostra a contagem de produtos do catálogo — prova o caminho ponta a ponta
  contra a API real antes de qualquer tela de verdade (fases 5-7); captura
  `InvalidOperationException` (não `HttpRequestException` diretamente — é o
  tipo que `BuildPcApiClient.SendAsync` usa pra embrulhar toda falha de
  rede/servidor). Testado localmente com `dotnet run` (env vars dummy):
  `/health` responde 200, `/` não derruba o processo quando a API está
  inalcançável. Ainda sem autenticação (fase 3) nem layout definitivo (fase
  4).
- Cliente web (30/07) — corrigido o último teste que travava o CI Linux
  depois da fase 1: `PdfPreviewService.CreatePath` sanitizava nomes de
  arquivo com `Path.GetInvalidFileNameChars()`, que no Linux só bloqueia `/`
  e NUL — `..`, `\`, `:`, `?` sobreviviam no nome (risco de path traversal
  na prévia de PDF). Trocado por uma lista fixa de caracteres inválidos
  (união Windows-Linux) + remoção explícita de sequências `..`. `CI
  build-windows`/`build-linux` (260 aprovados, 1 falha) → ambos verdes
  (261/261).
- Cliente web (30/07), fase 1/9 — início do projeto `BuildPc.Web` (Blazor
  Server, hospedado em `precos.hawk.com.br`, cobrindo Montagem/Consulta de
  Preço/Orçamentos; plano completo em decisão de arquitetura registrada na
  sessão). Primeiro passo: `QuotePdfService`, `ProductPriceTablePdfService`,
  `ProductPriceTableSectionFactory`, `PdfFontConfiguration` e
  `PhoneNumberFormatter` moveram de `BuildPc.Desktop/Services` para
  `BuildPc.Core/Services` — são .NET puro (MigraDoc/PdfSharp/SkiaSharp, sem
  Avalonia), e o cliente web precisa deles sem depender do Avalonia.
  `PricingCalculator` (novo, `BuildPc.Core.Services`) extrai
  `CalculateSalePrice`/`RoundUpToNinetyCents` (a regra de arredondamento
  terminando em ",90") de `FlexibleListItemViewModel`, que agora delega —
  cálculo de preço de venda tem uma implementação só, compartilhada.
  `CultureHelpers.BrazilianCulture` (novo) é a fonte canônica da cultura
  pt-BR; `MainWindowViewModel.BrazilianCulture` passou a delegar para lá.
  `PdfFontConfiguration` ganhou um `IFontResolver` para Linux: resolve
  "Arial" para a DejaVu Sans do pacote `fonts-dejavu-core` (**já presente**
  na VPS de produção, confirmado via SSH — nenhuma instalação nova
  necessária), ativado só quando `!OperatingSystem.IsWindows()`; o caminho
  do Windows (fontes do sistema) continua igual. `ProductPriceTableRowFactory.cs`
  ficou no Desktop de propósito (depende de `ProductListItemViewModel`, uma
  ViewModel do Avalonia) — só ganhou `using BuildPc.Core.Services;` para
  enxergar os tipos que mudaram de lugar. `BuildPc.Core.csproj` ganhou
  `PDFsharp-MigraDoc`, `SkiaSharp` e `SkiaSharp.NativeAssets.Linux`;
  `BuildPc.Desktop.csproj` perdeu a referência direta ao MigraDoc (vem
  transitivamente via o Core). 261/261 testes; PDF de amostra gerado e
  conferido visualmente (moeda, tabela, layout intactos) no Windows antes do
  commit.
- Auditoria completa (30/07), lote 6 — itens de baixa prioridade:
  `Models/Fixed/PcBuild.cs` (duplicata byte-a-byte de `Models/PcBuild.cs`,
  mantida viva só por uma regra `<Compile Remove>` em `Directory.Build.targets`)
  removida; `Directory.Build.targets` também removido, sem mais motivo para
  existir. Endpoint morto `GET /imports/last` (categoria única) removido da
  API junto com `PostgresBuildPcRepository.GetLastImport` — nenhum cliente
  chamava, só `GET /imports/last-all` é usado pelo Desktop (a versão SQLite
  do método foi mantida, tem teste direto). `DeleteMany` e
  `UpdateDescriptions` no Postgres trocaram um `DELETE`/`UPDATE` por id em
  loop por um único comando com `WHERE lower(id) = ANY(@ids)` — `DeleteMany`
  usa `RETURNING id` para saber quais foram apagados de fato (preserva a
  marca de exclusão do catálogo inicial, que só se aplica aos ~24 ids
  padrão). Contagem de testes atualizada (229 → 258 no lote 5).
- Auditoria completa (30/07), lote 5 — testes e CI: `PostgresBuildPcRepositoryIntegrationTests`
  (novo, via `Testcontainers.PostgreSql`) cobre `PostgresBuildPcRepository`
  contra um Postgres real dentro de um contêiner — round-trip de produto com
  todos os campos, `price_history` gravado em edição manual, favorito
  preservado + variação de preço registrada em `ReplaceImported`, orçamento
  com desconto/validade/condições. Sem Docker, os testes viram no-op (não
  falham, só avisam) em vez de quebrar `dotnet test` — por isso o CI ganhou
  um job Linux (`ubuntu-latest` já vem com Docker), que roda esses testes de
  verdade e publica cobertura via `coverlet.collector`.
  `BuildPcApiIntegrationTests` (novo, `WebApplicationFactory<Program>`, exigiu
  expor `public partial class Program` no fim de `Program.cs`) exercita a API
  pelo pipeline HTTP real: `/health` público, `/products` sem
  chave/com chave errada devolve 401, JSON malformado devolve 400 (confirma
  o ajuste do lote 1) — sem precisar de Postgres alcançável, porque a
  validação de chave e o parser de JSON rodam antes do repositório ser
  resolvido pelo DI. `QuotePdfServiceTests.SourceNeverReferencesCostOrProfitFields`
  é uma proteção estrutural (PdfSharp não expõe extração de texto): falha se
  `QuotePdfService.cs` algum dia referenciar campo de custo/lucro, antes de
  um vazamento chegar a um PDF real.
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
