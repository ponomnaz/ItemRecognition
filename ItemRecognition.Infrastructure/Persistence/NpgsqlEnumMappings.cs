using ItemRecognition.Domain.Enums;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace ItemRecognition.Infrastructure.Persistence;

public static class NpgsqlEnumMappings
{
    private static readonly INpgsqlNameTranslator Translator = new UpperSnakeCaseNameTranslator();

    public static NpgsqlDbContextOptionsBuilder MapItemRecognitionEnums(
        this NpgsqlDbContextOptionsBuilder builder)
    {
        builder.MapEnum<AiStage>("ai_stage", null, Translator);
        builder.MapEnum<RequestStatus>("request_status", null, Translator);
        return builder;
    }
}

internal sealed class UpperSnakeCaseNameTranslator : INpgsqlNameTranslator
{
    public string TranslateTypeName(string clrName) => ToUpperSnakeCase(clrName);

    public string TranslateMemberName(string clrName) => ToUpperSnakeCase(clrName);

    private static string ToUpperSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            var previous = i > 0 ? value[i - 1] : '\0';
            var next = i < value.Length - 1 ? value[i + 1] : '\0';

            var isBoundary =
                i > 0 &&
                char.IsUpper(current) &&
                (char.IsLower(previous) || char.IsDigit(previous) || char.IsUpper(previous) && char.IsLower(next));

            if (isBoundary && previous != '_')
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.ToString();
    }
}
