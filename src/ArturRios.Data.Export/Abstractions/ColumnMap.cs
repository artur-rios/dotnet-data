using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using ArturRios.Data.Export.Attributes;

namespace ArturRios.Data.Export.Abstractions;

/// <summary>A single export column: a header and a compiled getter over the record.</summary>
public sealed class Column(string header, Func<object, object?> getter)
{
    /// <summary>The column header.</summary>
    public string Header { get; } = header;

    /// <summary>Reads the column value from a record instance.</summary>
    public Func<object, object?> Getter { get; } = getter;
}

/// <summary>Builds and caches the ordered column plan for a record type.</summary>
public static class ColumnMap
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<Column>> Cache = new();

    /// <summary>Returns the cached column plan for <typeparamref name="T" />.</summary>
    public static IReadOnlyList<Column> For<T>() => For(typeof(T));

    /// <summary>Returns the cached column plan for <paramref name="type" />.</summary>
    public static IReadOnlyList<Column> For(Type type) => Cache.GetOrAdd(type, Build);

    private static IReadOnlyList<Column> Build(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                        && p.GetCustomAttribute<ExportIgnoreAttribute>() is null);

        var planned = properties.Select(p =>
        {
            var attribute = p.GetCustomAttribute<ExportColumnAttribute>();
            return new
            {
                Header = attribute?.Name ?? p.Name,
                Order = attribute?.Order ?? int.MaxValue,
                Token = p.MetadataToken,
                Getter = BuildGetter(p)
            };
        });

        return planned
            .OrderBy(x => x.Order).ThenBy(x => x.Token).ThenBy(x => x.Header, StringComparer.Ordinal)
            .Select(x => new Column(x.Header, x.Getter))
            .ToArray();
    }

    // Compiles a delegate getter once per property (cached with the column plan) to avoid per-row reflection.
    private static Func<object, object?> BuildGetter(PropertyInfo property)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var typed = Expression.Convert(instance, property.DeclaringType!);
        var access = Expression.Property(typed, property);
        var boxed = Expression.Convert(access, typeof(object));

        return Expression.Lambda<Func<object, object?>>(boxed, instance).Compile();
    }
}
