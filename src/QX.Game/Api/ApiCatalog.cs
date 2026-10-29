using System.Collections.ObjectModel;

namespace Qx.Game.Api;

internal sealed class ApiCatalog
{
    private readonly IReadOnlyDictionary<string, IApiBinding> by_id;

    public ApiCatalog(IEnumerable<IApiBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var values = new Dictionary<string, IApiBinding>(StringComparer.Ordinal);
        foreach (IApiBinding binding in bindings)
        {
            ArgumentNullException.ThrowIfNull(binding);
            if (!values.TryAdd(binding.Descriptor.Id, binding))
                throw new InvalidDataException($"Application member '{binding.Descriptor.Id}' is declared more than once.");
        }
        if (values.Count == 0)
            throw new ArgumentException("An application catalog requires at least one member.", nameof(bindings));

        Bindings = Array.AsReadOnly(values.Values.OrderBy(value => value.Descriptor.Id, StringComparer.Ordinal).ToArray());
        Descriptors = Array.AsReadOnly(Bindings.Select(value => value.Descriptor).ToArray());
        by_id = new ReadOnlyDictionary<string, IApiBinding>(values);
    }

    public IReadOnlyList<IApiBinding> Bindings { get; }
    public IReadOnlyList<ApiDescriptor> Descriptors { get; }

    public bool TryGet(string id, out IApiBinding binding)
    {
        ArgumentNullException.ThrowIfNull(id);
        return by_id.TryGetValue(id, out binding!);
    }

    public IApiBinding Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return by_id.TryGetValue(id, out IApiBinding? binding)
            ? binding
            : throw new KeyNotFoundException($"Unknown application member '{id}'.");
    }

    public ApiDescriptor Describe(string id) => Get(id).Descriptor;

    public ValueTask<object?> InvokeAsync(
        string id,
        object? request,
        CancellationToken cancellation_token = default) =>
        Get(id).InvokeAsync(request, cancellation_token);

    public IDisposable Subscribe(string id, Action<object?> receiver) =>
        Get(id).Subscribe(receiver);
}
