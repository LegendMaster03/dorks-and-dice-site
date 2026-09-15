using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace dorks_and_dice_site.Modes.DorksAndDice.Persistence;

public static class DorksAndDiceMigrationModelGuard
{
    public static bool HasDifferences(IModel snapshotModel, IModel currentModel)
    {
        ArgumentNullException.ThrowIfNull(snapshotModel);
        ArgumentNullException.ThrowIfNull(currentModel);

        return !CreateSignature(snapshotModel)
            .SequenceEqual(CreateSignature(currentModel), StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> CreateSignature(IModel model)
    {
        var records = new List<string>();

        foreach (var entity in model.GetEntityTypes().OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var tableName = entity.GetTableName();
            var schema = entity.GetSchema();
            Add(records, "entity", entity.Name, tableName, schema);

            StoreObjectIdentifier? table = tableName is null
                ? null
                : StoreObjectIdentifier.Table(tableName, schema);

            foreach (var property in entity.GetProperties().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                var columnName = table.HasValue
                    ? property.GetColumnName(table.Value)
                    : null;

                Add(
                    records,
                    "property",
                    entity.Name,
                    property.Name,
                    property.ClrType,
                    property.IsNullable,
                    property.GetMaxLength(),
                    property.GetPrecision(),
                    property.GetScale(),
                    property.IsUnicode(),
                    property.IsConcurrencyToken,
                    property.ValueGenerated,
                    columnName,
                    property.FindAnnotation(RelationalAnnotationNames.DefaultValue)?.Value,
                    property.FindAnnotation(RelationalAnnotationNames.DefaultValueSql)?.Value,
                    property.FindAnnotation(RelationalAnnotationNames.ComputedColumnSql)?.Value,
                    property.FindAnnotation(RelationalAnnotationNames.Collation)?.Value);
            }

            foreach (var key in entity.GetKeys())
            {
                Add(
                    records,
                    "key",
                    entity.Name,
                    ReferenceEquals(key, entity.FindPrimaryKey()),
                    string.Join(",", key.Properties.Select(property => property.Name)));
            }

            foreach (var index in entity.GetIndexes())
            {
                Add(
                    records,
                    "index",
                    entity.Name,
                    string.Join(",", index.Properties.Select(property => property.Name)),
                    index.IsUnique,
                    index.GetDatabaseName(),
                    index.GetFilter());
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                Add(
                    records,
                    "foreign-key",
                    entity.Name,
                    string.Join(",", foreignKey.Properties.Select(property => property.Name)),
                    foreignKey.PrincipalEntityType.Name,
                    string.Join(",", foreignKey.PrincipalKey.Properties.Select(property => property.Name)),
                    foreignKey.IsRequired,
                    foreignKey.IsUnique,
                    foreignKey.DeleteBehavior);
            }
        }

        records.Sort(StringComparer.Ordinal);
        return records;
    }

    private static void Add(List<string> records, params object?[] values)
    {
        records.Add(string.Join("\u001f", values.Select(Format)));
    }

    private static string Format(object? value)
    {
        var text = value switch
        {
            null => "<null>",
            Type type => type.FullName ?? type.Name,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        } ?? string.Empty;

        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\u001f", "\\u001f", StringComparison.Ordinal);
    }
}
