using BuildPc.Core.Models;

namespace BuildPc.Desktop.ViewModels;

public sealed record ProductDescriptionOperationViewModel(
    string Name,
    BulkDescriptionMode Mode);
