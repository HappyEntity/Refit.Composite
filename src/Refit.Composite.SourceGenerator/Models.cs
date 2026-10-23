using System.Collections;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Refit.Composite.SourceGenerator;

/// <summary>Everything needed to emit one composite. Value-equatable so the incremental pipeline can cache it.</summary>
internal sealed record CompositeModel(
    string FullyQualifiedName,
    string GeneratedName,
    EquatableArray<PropertyModel> Properties,
    EquatableArray<ClientModel> Clients,
    EquatableArray<string> Handlers,
    EquatableArray<DiagnosticInfo> Diagnostics);

/// <param name="DeclaringInterface">The interface that declares the property; it is implemented explicitly.</param>
/// <param name="ClientIndex">Index into <see cref="CompositeModel.Clients"/>.</param>
internal sealed record PropertyModel(string DeclaringInterface, string Name, string ClientType, int ClientIndex);

/// <param name="Pipeline">Handler types in the order they are added, outermost first.</param>
internal sealed record ClientModel(string Type, EquatableArray<string> Pipeline);

/// <remarks>
/// <see cref="Location"/> compares by syntax tree and span, so a model stays equal (and cached) until the file declaring it changes.
/// </remarks>
internal sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, Location? Location, EquatableArray<string> Arguments)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, ISymbol? symbol, params string[] arguments)
    {
        return Create(descriptor, symbol?.Locations.FirstOrDefault(), arguments);
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string[] arguments)
    {
        return new DiagnosticInfo(descriptor, location, new EquatableArray<string>(arguments.ToImmutableArray()));
    }

    public Diagnostic ToDiagnostic()
    {
        // ReSharper disable once CoVariantArrayConversion
        return Diagnostic.Create(Descriptor, Location, Arguments.AsImmutableArray().ToArray());
    }
}

/// <summary>An immutable array with value equality.</summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> array) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array = array;

    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public int Count => AsImmutableArray().Length;

    public T this[int index] => AsImmutableArray()[index];

    public ImmutableArray<T> AsImmutableArray() => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    public bool Equals(EquatableArray<T> other)
    {
        var left = AsImmutableArray();
        var right = other.AsImmutableArray();
        if (left.Length != right.Length) return false;

        for (var i = 0; i < left.Length; i++) {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked {
            var hash = 17;
            foreach (var item in AsImmutableArray()) {
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class EquatableArrayExtensions
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source)
        where T : IEquatable<T>
    {
        return new EquatableArray<T>(source.ToImmutableArray());
    }
}
