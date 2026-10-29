using System.Reflection;
using System.Text.Json;
using System.Text;
using Qx.Game.Api;

namespace Qx.Mcp;

internal static class ApiExample
{
    private static readonly IReadOnlyDictionary<string, string> constants = typeof(ApiIds)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .ToDictionary(field => (string)field.GetRawConstantValue()!, field => field.Name, StringComparer.Ordinal);

    public static string Call(ApiDescriptor descriptor)
    {
        var namespaces = new SortedSet<string>(StringComparer.Ordinal) { typeof(ApiIds).Namespace! };
        string id = constants.TryGetValue(descriptor.Id, out string? constant)
            ? $"{nameof(ApiIds)}.{constant}"
            : JsonSerializer.Serialize(descriptor.Id);
        string result = TypeName(descriptor.ResultType, namespaces);
        string call;
        if (descriptor.RequestType is { } request_type)
        {
            string request = TypeName(request_type, namespaces);
            string arguments = string.Join(", ", request_type.GetConstructors()[0].GetParameters()
                .Where(parameter => !parameter.HasDefaultValue)
                .Select(parameter => $"{parameter.Name}: {Placeholder(parameter.ParameterType, namespaces)}"));
            call = $"var result = await Api.InvokeAsync<{request}, {result}>({id}, new {request}({arguments}));";
        }
        else
        {
            call = $"Api.Subscribe<{result}>({id}, value => Log(value));";
        }

        var script = new StringBuilder();
        foreach (string name in namespaces)
            script.Append("using ").Append(name).Append(";\n");
        if (script.Length > 0)
            script.Append('\n');
        return script.Append(call).ToString();
    }

    private static string Placeholder(Type type, ISet<string> namespaces)
    {
        if (type == typeof(string))
            return "\"\"";
        if (type == typeof(bool))
            return "false";
        if (type == typeof(Id) ||
            type == typeof(byte) ||
            type == typeof(short) ||
            type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(double))
        {
            return "0";
        }
        if (type.IsEnum && Enum.GetNames(type) is [string first, ..])
            return $"{TypeName(type, namespaces)}.{first}";
        return type.IsArray || IsSequence(type) ? "[]" : "default";
    }

    private static bool IsSequence(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() is var definition &&
        (definition == typeof(IEnumerable<>) ||
            definition == typeof(IReadOnlyCollection<>) ||
            definition == typeof(IReadOnlyList<>) ||
            definition == typeof(List<>));

    private static string TypeName(Type type, ISet<string> namespaces)
    {
        Collect(type, namespaces);
        return FormatType(type);
    }

    private static string FormatType(Type type)
    {
        if (type.IsArray)
            return FormatType(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsByRef)
            return FormatType(type.GetElementType()!);
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return FormatType(underlying) + "?";
        if (type == typeof(void)) return "void";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(double)) return "double";
        if (type.IsGenericParameter) return type.Name;
        var parts = new Stack<Type>();
        for (Type? part = type; part is not null; part = part.DeclaringType)
            parts.Push(part);
        Type[] arguments = type.GetGenericArguments();
        int offset = 0;
        var names = new List<string>();
        foreach (Type part in parts)
        {
            int marker = part.Name.IndexOf('`');
            string name = marker < 0 ? part.Name : part.Name[..marker];
            if (marker >= 0)
            {
                int count = int.Parse(part.Name[(marker + 1)..], System.Globalization.CultureInfo.InvariantCulture);
                name += "<" + string.Join(", ", arguments.Skip(offset).Take(count).Select(FormatType)) + ">";
                offset += count;
            }
            names.Add(name);
        }
        return string.Join(".", names);
    }

    private static void Collect(Type type, ISet<string> namespaces)
    {
        if (type.HasElementType)
        {
            Collect(type.GetElementType()!, namespaces);
            return;
        }
        if (type.IsGenericParameter)
            return;
        if (type.Namespace is { } name)
            namespaces.Add(name);
        foreach (Type argument in type.GetGenericArguments())
            Collect(argument, namespaces);
    }
}
