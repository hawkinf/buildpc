using BuildPc.Core.Models;
using BuildPc.Desktop.ViewModels;

namespace BuildPc.Core.Tests;

public sealed class CategoryManagementViewModelTests
{
    [Fact]
    public void CategoryCrud_AddsRenamesAndDeletesCustomCategory()
    {
        IReadOnlyList<ProductCategoryDefinition> saved =
            ProductCategoryDefinition.Defaults();
        var viewModel = new CategoryManagementViewModel(
            saved,
            _ => 0,
            categories =>
            {
                saved = categories.ToList();
                return null;
            });

        viewModel.CategoryName = "Acessórios";
        viewModel.SaveCategoryCommand.Execute(null);

        var added = Assert.Single(viewModel.Categories, category =>
            category.Name == "Acessórios");
        Assert.False(added.IsSystem);
        Assert.Equal(13, saved.Count);

        viewModel.SelectedCategory = added;
        viewModel.CategoryName = "Acessórios premium";
        viewModel.SaveCategoryCommand.Execute(null);

        var renamed = Assert.Single(viewModel.Categories, category =>
            category.Name == "Acessórios premium");
        Assert.Equal(added.Value, renamed.Value);

        viewModel.SelectedCategory = renamed;
        viewModel.RequestDeleteCategoryCommand.Execute(null);
        Assert.True(viewModel.IsDeleteConfirmationVisible);
        viewModel.ConfirmDeleteCategoryCommand.Execute(null);

        Assert.DoesNotContain(
            viewModel.Categories,
            category => category.Value == renamed.Value);
        Assert.Equal(12, saved.Count);
        Assert.True(viewModel.IsSuccess);
    }

    [Fact]
    public void CategoryDelete_BlocksSystemAndCategoriesWithProducts()
    {
        var custom = new ProductCategoryDefinition
        {
            Value = (ComponentCategory)1000,
            Name = "Acessórios",
            DisplayOrder = 12,
            IsSystem = false
        };
        var viewModel = new CategoryManagementViewModel(
            [.. ProductCategoryDefinition.Defaults(), custom],
            category => category == custom.Value ? 2 : 0,
            _ => null);

        viewModel.SelectedCategory = viewModel.Categories.First(category =>
            category.IsSystem);
        viewModel.RequestDeleteCategoryCommand.Execute(null);
        Assert.True(viewModel.IsError);
        Assert.False(viewModel.IsDeleteConfirmationVisible);

        viewModel.SelectedCategory = viewModel.Categories.First(category =>
            category.Value == custom.Value);
        viewModel.RequestDeleteCategoryCommand.Execute(null);
        Assert.True(viewModel.IsError);
        Assert.Contains("2 produto", viewModel.StatusMessage);
        Assert.False(viewModel.IsDeleteConfirmationVisible);
    }

    [Fact]
    public void DefaultSettings_ExposeSystemCategories()
    {
        var categories = new BusinessSettings().EffectiveProductCategories();

        Assert.Equal(12, categories.Count);
        Assert.All(categories, category => Assert.True(category.IsSystem));
        Assert.Equal(
            ComponentCategory.Processor,
            categories[0].Value);
    }
}
