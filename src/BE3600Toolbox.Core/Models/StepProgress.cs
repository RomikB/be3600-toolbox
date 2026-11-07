namespace BE3600Toolbox.Core.Models;

public record StepProgress(
    string StepName,
    double? Percentage = null,
    string? Details = null);

