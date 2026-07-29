using BuildPc.Core.Models;
using BuildPc.Core.Services;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class FavoriteAndReorderTests
{
    [Fact]
    public void FavoriteSurvivesAnImportLikeTheKeepFlag()
    {
        using var workspace = new Workspace();
        var repository = workspace.Repository;
        repository.ReplaceImported(
            ComponentCategory.Processor,
            "kabum",
            [Product("kabum-1", 100m)]);

        Assert.True(repository.SetFavorite("kabum-1", favorite: true));

        // A importação seguinte não pode desfazer uma escolha do usuário.
        repository.ReplaceImported(
            ComponentCategory.Processor,
            "kabum",
            [Product("kabum-1", 120m)]);

        var stored = Assert.Single(
            repository.GetAll(),
            product => product.Id == "kabum-1");
        Assert.True(stored.IsFavorite);
        Assert.Equal(120m, stored.Price);
    }

    [Fact]
    public void SetFavoriteReturnsFalseForUnknownProduct()
    {
        using var workspace = new Workspace();

        Assert.False(workspace.Repository.SetFavorite("nao-existe", favorite: true));
    }

    [Fact]
    public void FavoritesComeFirstInTheProductPicker()
    {
        var common = Product("cpu-comum", 100m) with { Name = "AAA Comum" };
        var favorite = Product("cpu-favorito", 200m) with
        {
            Name = "ZZZ Favorito",
            IsFavorite = true
        };
        var viewModel = new FlexibleListViewModel(
            [common, favorite],
            [new(ComponentCategory.Processor, "Processador")]);

        // "ZZZ" viria por último em ordem alfabética; ser favorito o traz antes.
        Assert.Equal(
            ["ZZZ Favorito", "AAA Comum"],
            viewModel.ProductPicker.Options.Select(option => option.Name));
        Assert.True(viewModel.ProductPicker.Options[0].IsFavorite);
    }

    [Fact]
    public void FavoritesStayFirstEvenWhenSortingByPrice()
    {
        var cheap = Product("cpu-barato", 50m) with { Name = "Barato" };
        var expensiveFavorite = Product("cpu-caro", 900m) with
        {
            Name = "Caro favorito",
            IsFavorite = true
        };
        var viewModel = new FlexibleListViewModel(
            [cheap, expensiveFavorite],
            [new(ComponentCategory.Processor, "Processador")]);

        viewModel.ProductPicker.SortByPriceCommand.Execute(null);

        Assert.Equal("Caro favorito", viewModel.ProductPicker.Options[0].Name);
    }

    [Fact]
    public void MovingLinesChangesTheOrderAndMarksTheQuoteDirty()
    {
        var first = Product("cpu", 100m) with { Name = "Primeiro" };
        var second = Product("ram", 200m) with
        {
            Name = "Segundo",
            Category = ComponentCategory.Memory
        };
        var viewModel = new FlexibleListViewModel(
            [first, second],
            [
                new(ComponentCategory.Processor, "Processador"),
                new(ComponentCategory.Memory, "Memória")
            ]);

        viewModel.ProductPicker.Selected = first;
        viewModel.AddCommand.Execute(null);
        viewModel.SelectedCategory = viewModel.Categories[1];
        viewModel.ProductPicker.Selected = second;
        viewModel.AddCommand.Execute(null);

        Assert.Equal(["Primeiro", "Segundo"], viewModel.Items.Select(item => item.Name));

        // Nas pontas, o botão correspondente fica desabilitado.
        Assert.False(viewModel.Items[0].CanMoveUp);
        Assert.True(viewModel.Items[0].CanMoveDown);
        Assert.True(viewModel.Items[1].CanMoveUp);
        Assert.False(viewModel.Items[1].CanMoveDown);

        viewModel.Items[1].MoveUpCommand.Execute(null);

        Assert.Equal(["Segundo", "Primeiro"], viewModel.Items.Select(item => item.Name));
        Assert.True(viewModel.IsDirty);
        // A alternância de cor acompanha a nova ordem.
        Assert.Equal([false, true], viewModel.Items.Select(item => item.IsAlternate));
    }

    [Fact]
    public void MovingBeyondTheEdgesDoesNothing()
    {
        var product = Product("cpu", 100m);
        var viewModel = new FlexibleListViewModel(
            [product],
            [new(ComponentCategory.Processor, "Processador")]);
        viewModel.ProductPicker.Selected = product;
        viewModel.AddCommand.Execute(null);

        var item = Assert.Single(viewModel.Items);
        item.MoveUpCommand.Execute(null);
        item.MoveDownCommand.Execute(null);

        Assert.Same(item, Assert.Single(viewModel.Items));
    }

    private static PcComponent Product(string id, decimal price) =>
        new()
        {
            Id = id,
            Category = ComponentCategory.Processor,
            Name = $"Produto {id}",
            Brand = "Teste",
            Description = "Produto de teste",
            Price = price,
            ImportSource = "kabum"
        };

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"buildpc-favoritos-{Guid.NewGuid():N}");

        public Workspace()
        {
            Directory.CreateDirectory(_root);
            Repository = new ComponentCatalogRepository(
                Path.Combine(_root, "catalogo.db"));
        }

        public ComponentCatalogRepository Repository { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
