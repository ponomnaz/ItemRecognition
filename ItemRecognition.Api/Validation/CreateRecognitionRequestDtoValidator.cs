using FluentValidation;
using ItemRecognition.Api.Contracts.Recognition;

namespace ItemRecognition.Api.Validation;

public sealed class CreateRecognitionRequestDtoValidator : AbstractValidator<CreateRecognitionRequestDto>
{
    public CreateRecognitionRequestDtoValidator()
    {
        RuleFor(request => request.ImageUrl)
            .NotEmpty()
            .WithMessage("Image URL is required.")
            .MaximumLength(2048)
            .WithMessage("Image URL length must be less than or equal to 2048 characters.")
            .Must(BeValidAbsoluteHttpUrl)
            .WithMessage("Image URL must be an absolute http/https URL.");
    }

    private static bool BeValidAbsoluteHttpUrl(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https";
    }
}
