// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Collections;
using System.Reflection;
using System.Text;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Contracts.Admin;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Expansions;
using Lfm.Contracts.Guild;
using Lfm.Contracts.Health;
using Lfm.Contracts.Instances;
using Lfm.Contracts.Me;
using Lfm.Contracts.Privacy;
using Lfm.Contracts.Raiders;
using Lfm.Contracts.Runs;
using Lfm.Contracts.Specializations;

namespace Lfm.OpenApiGenerator;

public static class OpenApiSnapshotGenerator
{
    private static readonly Assembly ApiAssembly = typeof(HealthFunction).Assembly;
    private static readonly Assembly ContractsAssembly = typeof(HealthResponse).Assembly;

    private static readonly IReadOnlyDictionary<(string Method, string Path), SchemaRef> RequestSchemas =
        new Dictionary<(string, string), SchemaRef>(StringTupleComparer.OrdinalIgnoreCase)
        {
            [("patch", "/api/v1/me")] = SchemaRef.Object<UpdateMeRequest>(),
            [("patch", "/api/v1/guild")] = SchemaRef.Object<UpdateGuildRequest>(),
            [("patch", "/api/v1/guild/admin")] = SchemaRef.Object<UpdateGuildRequest>(),
            [("post", "/api/v1/runs")] = SchemaRef.Object<CreateRunRequest>(),
            [("put", "/api/v1/runs/{id}")] = SchemaRef.Object<UpdateRunRequest>(),
            [("post", "/api/v1/runs/{id}/signup")] = SchemaRef.Object<SignupRequest>(),
            [("post", "/api/v1/raider/character")] = SchemaRef.Object<AddCharacterRequest>(),
            [("post", "/api/v1/battlenet/character-portraits")] = SchemaRef.Array<CharacterPortraitRequest>(),
        };

    private static readonly IReadOnlyDictionary<(string Method, string Path), SchemaRef> ResponseSchemas =
        new Dictionary<(string, string), SchemaRef>(StringTupleComparer.OrdinalIgnoreCase)
        {
            [("get", "/api/v1/health")] = SchemaRef.Object<HealthResponse>(),
            [("get", "/api/v1/health/ready")] = SchemaRef.Object<HealthResponse>(),
            [("get", "/api/v1/me")] = SchemaRef.Object<MeResponse>(),
            [("patch", "/api/v1/me")] = SchemaRef.Object<UpdateMeResponse>(),
            [("get", "/api/v1/privacy-contact/email")] = SchemaRef.Object<PrivacyEmailResponse>(),
            [("get", "/api/v1/guild")] = SchemaRef.Object<GuildDto>(),
            [("patch", "/api/v1/guild")] = SchemaRef.Object<GuildDto>(),
            [("get", "/api/v1/guild/admin")] = SchemaRef.Object<GuildDto>(),
            [("patch", "/api/v1/guild/admin")] = SchemaRef.Object<GuildDto>(),
            [("get", "/api/v1/runs")] = SchemaRef.Object<RunsListResponse>(),
            [("post", "/api/v1/runs")] = SchemaRef.Object<RunDetailDto>(),
            [("get", "/api/v1/runs/{id}")] = SchemaRef.Object<RunDetailDto>(),
            [("put", "/api/v1/runs/{id}")] = SchemaRef.Object<RunDetailDto>(),
            [("post", "/api/v1/runs/{id}/signup")] = SchemaRef.Object<RunDetailDto>(),
            [("delete", "/api/v1/runs/{id}/signup")] = SchemaRef.Object<RunDetailDto>(),
            [("get", "/api/v1/runs/{id}/signup/options")] = SchemaRef.Object<RunSignupOptionsDto>(),
            [("post", "/api/v1/raider/character")] = SchemaRef.Object<AddCharacterResponse>(),
            [("put", "/api/v1/raider/characters/{id}")] = SchemaRef.Object<UpdateCharacterResponse>(),
            [("post", "/api/v1/raider/characters/{id}/enrich")] = SchemaRef.Object<CharacterDto>(),
            [("get", "/api/v1/battlenet/characters")] = SchemaRef.Array<CharacterDto>(),
            [("post", "/api/v1/battlenet/characters/refresh")] = SchemaRef.Array<CharacterDto>(),
            [("post", "/api/v1/battlenet/character-portraits")] = SchemaRef.Object<PortraitResponse>(),
            [("get", "/api/v1/wow/reference/expansions")] = SchemaRef.Array<ExpansionDto>(),
            [("get", "/api/v1/wow/reference/instances")] = SchemaRef.Array<InstanceDto>(),
            [("get", "/api/v1/wow/reference/specializations")] = SchemaRef.Array<SpecializationDto>(),
            [("post", "/api/v1/wow/reference/refresh")] = SchemaRef.Object<WowReferenceRefreshProgress>(),
        };

    private static readonly string[] Tags =
    [
        "Health",
        "Auth",
        "Me",
        "Guild",
        "Runs",
        "Raider Characters",
        "Battle.net Profile",
        "WoW Reference",
        "Admin",
        "Privacy",
    ];

    public static string Generate(OpenApiSnapshotOptions? options = null)
    {
        options ??= OpenApiSnapshotOptions.RepositorySnapshot;
        var endpoints = DiscoverEndpoints()
            .OrderBy(e => e.Path, StringComparer.Ordinal)
            .ThenBy(e => HttpMethodOrder(e.Method))
            .ThenBy(e => e.Method, StringComparer.Ordinal)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("# SPDX-License-Identifier: AGPL-3.0-or-later");
        builder.AppendLine("# SPDX-FileCopyrightText: 2026 LFM contributors");
        builder.AppendLine("# <auto-generated />");
        builder.AppendLine("# Generated by `dotnet run --project tools/Lfm.OpenApiGenerator`.");
        builder.AppendLine("# Do not edit this file by hand; change the API route/contract source instead.");
        builder.AppendLine();
        builder.AppendLine("openapi: 3.1.0");
        builder.AppendLine("info:");
        builder.AppendLine("  title: Lfm API");
        builder.AppendLine("  version: 0.1.0");
        builder.AppendLine("  summary: Raid logistics API for World of Warcraft guilds.");
        builder.AppendLine("  description: Public HTTP contract generated from Azure Functions HTTP triggers and shared DTO contracts.");
        builder.AppendLine("  x-generated-by: tools/Lfm.OpenApiGenerator");
        builder.AppendLine("  x-generated-source: Azure Functions HTTP triggers plus shared/Lfm.Contracts DTO reflection");
        builder.AppendLine("  license:");
        builder.AppendLine("    name: AGPL-3.0-or-later");
        builder.AppendLine("    identifier: AGPL-3.0-or-later");
        builder.AppendLine("  contact:");
        builder.AppendLine("    name: Lfm contributors");
        builder.AppendLine("    url: https://github.com/lfm-org/lfm");
        builder.AppendLine("servers:");
        if (string.IsNullOrWhiteSpace(options.ServerUrl))
        {
            builder.AppendLine("  - url: /");
            builder.AppendLine("    description: Relative deployment root.");
        }
        else
        {
            builder.AppendLine($"  - url: {Quote(options.ServerUrl)}");
            builder.AppendLine("    description: Deployment API origin.");
        }
        builder.AppendLine("tags:");
        foreach (var tag in Tags)
        {
            builder.AppendLine($"  - name: {Quote(tag)}");
        }

        builder.AppendLine("security:");
        builder.AppendLine("  - sessionCookie: []");
        builder.AppendLine("paths:");
        foreach (var pathGroup in endpoints.GroupBy(e => e.Path))
        {
            builder.AppendLine($"  {pathGroup.Key}:");
            foreach (var endpoint in pathGroup)
            {
                AppendOperation(builder, endpoint);
            }
        }

        AppendComponents(builder);
        return builder.ToString().ReplaceLineEndings("\n");
    }

    private static IReadOnlyList<Endpoint> DiscoverEndpoints()
    {
        var endpoints = new List<Endpoint>();
        foreach (var type in ApiAssembly.GetTypes().Where(t => string.Equals(t.Namespace, "Lfm.Api.Functions", StringComparison.Ordinal)))
        {
            var typeRequiresAuth = HasAttribute<RequireAuthAttribute>(type.GetCustomAttributes());
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var trigger = FindHttpTrigger(method);
                if (trigger is null || !trigger.Route.StartsWith("v1/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var requiresAuth = typeRequiresAuth || HasAttribute<RequireAuthAttribute>(method.GetCustomAttributes());
                foreach (var httpMethod in trigger.Methods)
                {
                    endpoints.Add(new Endpoint(
                        httpMethod.ToLowerInvariant(),
                        $"/api/{trigger.Route}",
                        method.Name,
                        requiresAuth));
                }
            }
        }

        return endpoints;
    }

    private static HttpTriggerMetadata? FindHttpTrigger(MethodInfo method)
    {
        foreach (var parameter in method.GetParameters())
        {
            var attribute = parameter.GetCustomAttributes()
                .FirstOrDefault(a => string.Equals(a.GetType().Name, "HttpTriggerAttribute", StringComparison.Ordinal));
            if (attribute is null)
            {
                continue;
            }

            var route = attribute.GetType().GetProperty("Route")?.GetValue(attribute) as string;
            var methods = attribute.GetType().GetProperty("Methods")?.GetValue(attribute) as IEnumerable<string>;
            if (string.IsNullOrWhiteSpace(route) || methods is null)
            {
                throw new InvalidOperationException($"Could not read HTTP trigger metadata from {method.DeclaringType?.FullName}.{method.Name}.");
            }

            return new HttpTriggerMetadata(route, methods.ToArray());
        }

        return null;
    }

    private static void AppendOperation(StringBuilder builder, Endpoint endpoint)
    {
        builder.AppendLine($"    {endpoint.Method}:");
        builder.AppendLine($"      tags:");
        builder.AppendLine($"        - {Quote(TagFor(endpoint.Path))}");
        builder.AppendLine($"      summary: {Quote($"{endpoint.Method.ToUpperInvariant()} {endpoint.Path}")}");
        builder.AppendLine($"      operationId: {OperationId(endpoint.Method, endpoint.Path)}");

        var pathParameters = ExtractPathParameters(endpoint.Path);
        if (pathParameters.Count > 0)
        {
            builder.AppendLine("      parameters:");
            foreach (var parameter in pathParameters)
            {
                builder.AppendLine($"        - name: {parameter}");
                builder.AppendLine("          in: path");
                builder.AppendLine("          required: true");
                builder.AppendLine("          schema:");
                builder.AppendLine("            type: string");
            }
        }

        if (!endpoint.RequiresAuth)
        {
            builder.AppendLine("      security: []");
        }

        if (RequestSchemas.TryGetValue((endpoint.Method, endpoint.Path), out var requestSchema))
        {
            builder.AppendLine("      requestBody:");
            builder.AppendLine("        required: true");
            builder.AppendLine("        content:");
            builder.AppendLine("          application/json:");
            builder.AppendLine("            schema:");
            AppendSchemaRef(builder, requestSchema, 14);
        }

        builder.AppendLine("      responses:");
        if (ResponseSchemas.TryGetValue((endpoint.Method, endpoint.Path), out var responseSchema))
        {
            builder.AppendLine("        '200':");
            builder.AppendLine("          description: Success.");
            builder.AppendLine("          content:");
            builder.AppendLine("            application/json:");
            builder.AppendLine("              schema:");
            AppendSchemaRef(builder, responseSchema, 16);
        }
        else
        {
            var status = string.Equals(endpoint.Method, "delete", StringComparison.Ordinal) ? "204" : "302";
            var description = status == "204" ? "No content." : "Redirect.";
            builder.AppendLine($"        '{status}':");
            builder.AppendLine($"          description: {description}");
        }

        builder.AppendLine("        default:");
        builder.AppendLine("          $ref: '#/components/responses/Problem'");
    }

    private static void AppendComponents(StringBuilder builder)
    {
        builder.AppendLine("components:");
        builder.AppendLine("  securitySchemes:");
        builder.AppendLine("    sessionCookie:");
        builder.AppendLine("      type: apiKey");
        builder.AppendLine("      in: cookie");
        builder.AppendLine("      name: session");
        builder.AppendLine("      description: Configured session cookie.");
        builder.AppendLine("  responses:");
        builder.AppendLine("    Problem:");
        builder.AppendLine("      description: RFC 9457 problem response.");
        builder.AppendLine("      content:");
        builder.AppendLine("        application/problem+json:");
        builder.AppendLine("          schema:");
        builder.AppendLine("            $ref: '#/components/schemas/ProblemDetails'");
        builder.AppendLine("  schemas:");
        AppendProblemDetailsSchema(builder);

        foreach (var type in ContractTypes())
        {
            AppendContractSchema(builder, type);
        }
    }

    private static void AppendProblemDetailsSchema(StringBuilder builder)
    {
        builder.AppendLine("    ProblemDetails:");
        builder.AppendLine("      type: object");
        builder.AppendLine("      properties:");
        foreach (var name in new[] { "type", "title", "status", "detail", "instance", "traceId" })
        {
            builder.AppendLine($"        {name}:");
            if (string.Equals(name, "status", StringComparison.Ordinal))
            {
                builder.AppendLine("          type: integer");
                builder.AppendLine("          format: int32");
            }
            else
            {
                builder.AppendLine("          type: string");
            }
        }
        builder.AppendLine("      additionalProperties: true");
    }

    private static void AppendContractSchema(StringBuilder builder, Type type)
    {
        builder.AppendLine($"    {type.Name}:");
        builder.AppendLine("      type: object");
        builder.AppendLine("      properties:");
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.GetIndexParameters().Length == 0))
        {
            builder.AppendLine($"        {ToCamelCase(property.Name)}:");
            AppendJsonSchema(builder, property.PropertyType, 10);
        }
        builder.AppendLine("      additionalProperties: false");
    }

    private static void AppendJsonSchema(StringBuilder builder, Type type, int indent)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        var prefix = new string(' ', indent);

        if (underlying == typeof(string))
        {
            builder.AppendLine($"{prefix}type: string");
        }
        else if (underlying == typeof(bool))
        {
            builder.AppendLine($"{prefix}type: boolean");
        }
        else if (underlying == typeof(int) || underlying == typeof(long))
        {
            builder.AppendLine($"{prefix}type: integer");
            builder.AppendLine($"{prefix}format: {(underlying == typeof(long) ? "int64" : "int32")}");
        }
        else if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal))
        {
            builder.AppendLine($"{prefix}type: number");
        }
        else if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
        {
            builder.AppendLine($"{prefix}type: string");
            builder.AppendLine($"{prefix}format: date-time");
        }
        else if (TryGetDictionaryValueType(underlying, out var valueType))
        {
            builder.AppendLine($"{prefix}type: object");
            builder.AppendLine($"{prefix}additionalProperties:");
            AppendJsonSchema(builder, valueType, indent + 2);
        }
        else if (TryGetEnumerableElementType(underlying, out var elementType))
        {
            builder.AppendLine($"{prefix}type: array");
            builder.AppendLine($"{prefix}items:");
            AppendJsonSchema(builder, elementType, indent + 2);
        }
        else if (IsContractType(underlying))
        {
            builder.AppendLine($"{prefix}$ref: '#/components/schemas/{underlying.Name}'");
        }
        else
        {
            builder.AppendLine($"{prefix}type: object");
        }
    }

    private static void AppendSchemaRef(StringBuilder builder, SchemaRef schema, int indent)
    {
        var prefix = new string(' ', indent);
        if (schema.IsArray)
        {
            builder.AppendLine($"{prefix}type: array");
            builder.AppendLine($"{prefix}items:");
            builder.AppendLine($"{prefix}  $ref: '#/components/schemas/{schema.Type.Name}'");
            return;
        }

        builder.AppendLine($"{prefix}$ref: '#/components/schemas/{schema.Type.Name}'");
    }

    private static IReadOnlyList<Type> ContractTypes() =>
        ContractsAssembly.GetExportedTypes()
            .Where(IsContractType)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToArray();

    private static bool IsContractType(Type type) =>
        type.Namespace?.StartsWith("Lfm.Contracts.", StringComparison.Ordinal) == true
        && type.IsClass
        && !type.IsAbstract
        && type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0;

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(string);
            return false;
        }

        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        elementType = enumerable?.GetGenericArguments()[0] ?? typeof(object);
        return enumerable is not null;
    }

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        var dictionary = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
        valueType = dictionary?.GetGenericArguments()[1] ?? typeof(object);
        return dictionary is not null;
    }

    private static IReadOnlyList<string> ExtractPathParameters(string path)
    {
        var parameters = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment is ['{', .., '}'])
            {
                parameters.Add(segment[1..^1]);
            }
        }

        return parameters;
    }

    private static string TagFor(string path)
    {
        if (path.StartsWith("/api/v1/health", StringComparison.Ordinal)) return "Health";
        if (path.Contains("/battlenet/login", StringComparison.Ordinal) ||
            path.Contains("/battlenet/callback", StringComparison.Ordinal) ||
            path.Contains("/battlenet/logout", StringComparison.Ordinal)) return "Auth";
        if (path.StartsWith("/api/v1/me", StringComparison.Ordinal)) return "Me";
        if (path.StartsWith("/api/v1/guild", StringComparison.Ordinal)) return "Guild";
        if (path.StartsWith("/api/v1/runs", StringComparison.Ordinal)) return "Runs";
        if (path.StartsWith("/api/v1/raider", StringComparison.Ordinal)) return "Raider Characters";
        if (path.StartsWith("/api/v1/battlenet", StringComparison.Ordinal)) return "Battle.net Profile";
        if (path.StartsWith("/api/v1/wow", StringComparison.Ordinal)) return "WoW Reference";
        if (path.StartsWith("/api/v1/admin", StringComparison.Ordinal)) return "Admin";
        if (path.StartsWith("/api/v1/privacy", StringComparison.Ordinal)) return "Privacy";
        return "API";
    }

    private static string OperationId(string method, string path)
    {
        var words = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.Equals(s, "api", StringComparison.Ordinal) && !string.Equals(s, "v1", StringComparison.Ordinal))
            .SelectMany(s => s.Trim('{', '}').Split('-', StringSplitOptions.RemoveEmptyEntries))
            .Select(ToPascalCase);

        return method.ToLowerInvariant() + string.Concat(words);
    }

    private static int HttpMethodOrder(string method) => method switch
    {
        "get" => 0,
        "post" => 1,
        "put" => 2,
        "patch" => 3,
        "delete" => 4,
        _ => 9,
    };

    private static bool HasAttribute<TAttribute>(IEnumerable attributes)
        where TAttribute : Attribute =>
        attributes.Cast<object>().Any(a => a.GetType() == typeof(TAttribute));

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) || char.IsLower(value[0])
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];

    private static string ToPascalCase(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];

    private static string Quote(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private sealed record Endpoint(string Method, string Path, string SourceMethod, bool RequiresAuth);

    private sealed record HttpTriggerMetadata(string Route, IReadOnlyList<string> Methods);

    private sealed record SchemaRef(Type Type, bool IsArray)
    {
        public static SchemaRef Object<T>() => new(typeof(T), IsArray: false);

        public static SchemaRef Array<T>() => new(typeof(T), IsArray: true);
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Method, string Path)>
    {
        public static readonly StringTupleComparer OrdinalIgnoreCase = new();

        public bool Equals((string Method, string Path) x, (string Method, string Path) y) =>
            string.Equals(x.Method, y.Method, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Method, string Path) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Method),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path));
    }
}

public sealed record OpenApiSnapshotOptions(string? ServerUrl)
{
    public static OpenApiSnapshotOptions RepositorySnapshot { get; } = new(ServerUrl: null);
}
