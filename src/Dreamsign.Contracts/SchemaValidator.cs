using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Dreamsign.Contracts;

/// <summary>Validator for the deliberately bounded JSON Schema subset used by the shared catalogue.</summary>
public static class SchemaValidator
{
    public static void Validate(JsonNode? value, JsonObject schema, string path = "$")
    {
        if (schema["anyOf"] is JsonArray any || schema["oneOf"] is JsonArray)
        {
            var variants = (JsonArray)(schema["anyOf"] ?? schema["oneOf"])!;
            var matches = 0;
            foreach (var variant in variants)
                try { Validate(value, variant!.AsObject(), path); matches++; } catch (ArgumentException) { }
            if (matches == 0 || (schema.ContainsKey("oneOf") && matches != 1)) Fail(path, "invalid variant");
            return;
        }
        if (schema.ContainsKey("const") && !JsonNode.DeepEquals(value, schema["const"])) Fail(path, "constant mismatch");
        if (schema["enum"] is JsonArray choices && !choices.Any(v => JsonNode.DeepEquals(value, v))) Fail(path, "invalid choice");
        var kind = value?.GetValueKind() ?? JsonValueKind.Null;
        switch (schema["type"]?.GetValue<string>())
        {
            case "object":
                if (value is not JsonObject) Fail(path, "object required");
                var obj = value!.AsObject();
                foreach (var key in schema["required"]?.AsArray() ?? [])
                    if (!obj.ContainsKey(key!.GetValue<string>())) Fail(path + "." + key, "required");
                if (obj.Count < (schema["minProperties"]?.GetValue<int>() ?? 0)) Fail(path, "empty object");
                var properties = schema["properties"]?.AsObject() ?? new JsonObject();
                foreach (var (key, child) in obj)
                {
                    if (properties[key] is not JsonObject childSchema) Fail(path + "." + key, "unknown field");
                    else Validate(child, childSchema, path + "." + key);
                }
                break;
            case "array":
                if (value is not JsonArray) Fail(path, "array required");
                var array = value!.AsArray();
                if (array.Count < (schema["minItems"]?.GetValue<int>() ?? 0) || array.Count > (schema["maxItems"]?.GetValue<int>() ?? int.MaxValue)) Fail(path, "invalid array length");
                for (var i = 0; i < array.Count; i++) Validate(array[i], schema["items"]!.AsObject(), $"{path}[{i}]");
                break;
            case "string":
                if (kind != JsonValueKind.String) Fail(path, "string required");
                var text = value!.GetValue<string>();
                if (text.Length < (schema["minLength"]?.GetValue<int>() ?? 0) || text.Length > (schema["maxLength"]?.GetValue<int>() ?? int.MaxValue)) Fail(path, "invalid string length");
                if (schema["pattern"] is JsonNode pattern && !Regex.IsMatch(text, pattern.GetValue<string>(), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) Fail(path, "invalid pattern");
                break;
            case "number":
            case "integer":
                if (kind != JsonValueKind.Number || !double.TryParse(value!.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) Fail(path, "finite number required");
                else
                {
                    if (schema["type"]!.GetValue<string>() == "integer" && (Math.Truncate(number) != number || Math.Abs(number) > 9007199254740991d)) Fail(path, "safe integer required");
                    if (number < (schema["minimum"]?.GetValue<double>() ?? double.NegativeInfinity) || number > (schema["maximum"]?.GetValue<double>() ?? double.PositiveInfinity)) Fail(path, "number outside bounds");
                }
                break;
            case "boolean":
                if (kind is not (JsonValueKind.True or JsonValueKind.False)) Fail(path, "boolean required");
                break;
        }
    }
    private static void Fail(string path, string message) => throw new ArgumentException(path + ": " + message);
}
