using FluentValidation;
using ItemRecognition.Api.Contracts.Recognition;

namespace ItemRecognition.Api.Validation;

public sealed class DetectMaterialsRequestDtoValidator : AbstractValidator<DetectMaterialsRequestDto>
{
    public DetectMaterialsRequestDtoValidator()
    {
        RuleFor(request => request.Items)
            .NotNull()
            .WithMessage("Items list is required.")
            .Must(items => items is { Count: > 0 })
            .WithMessage("Items list must contain at least one item.")
            .Must(items => items is null || items.Count <= 20)
            .WithMessage("Items list must contain at most 20 items.")
            .Must(HaveUniqueValuesIgnoringCase)
            .WithMessage("Items list must not contain duplicate values (case-insensitive).");

        RuleForEach(request => request.Items)
            .NotEmpty()
            .WithMessage("Item name is required.")
            .MaximumLength(200)
            .WithMessage("Item name length must be less than or equal to 200 characters.");
    }

    private static bool HaveUniqueValuesIgnoringCase(IReadOnlyList<string>? items)
    {
        if (items is null)
        {
            return true;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            if (!set.Add(item.Trim()))
            {
                return false;
            }
        }

        return true;
    }
}
