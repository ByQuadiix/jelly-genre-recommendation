using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeRecommendations.Services;

/// <summary>
/// Integration with the IAmParadox27 File Transformation plugin.
/// </summary>
public static class FileTransformationIntegration
{
    private static readonly Guid TransformationId = Guid.Parse("e8b15d23-74a9-4cf3-a612-98e364177b91");
    private const string ScriptTag = "<script src=\"/Recommendations/ClientScript.js\" defer></script>";

    /// <summary>
    /// Gets a value indicating whether the transformation has been registered with File Transformation.
    /// </summary>
    public static bool IsFileTransformationActive { get; private set; }

    /// <summary>
    /// Attempts to locate the File Transformation plugin and register this plugin's HTML injection.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public static void TryRegister(ILogger? logger = null)
    {
        if (IsFileTransformationActive)
        {
            return;
        }

        try
        {
            var assemblies = AssemblyLoadContext.All
                .SelectMany(ctx => ctx.Assemblies)
                .Concat(AppDomain.CurrentDomain.GetAssemblies())
                .Distinct()
                .Where(a => a.FullName?.Contains("FileTransformation", StringComparison.OrdinalIgnoreCase) ?? false)
                .ToList();

            Assembly? fileTransformationAssembly = null;
            Type? pluginInterfaceType = null;

            foreach (var asm in assemblies)
            {
                pluginInterfaceType = asm.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface")
                                   ?? asm.GetType("Jellyfin.Plugin.FileTransformation.FileTransformationPlugin")
                                   ?? asm.GetTypes().FirstOrDefault(t => t.Name == "PluginInterface" || t.Name == "FileTransformationPlugin");

                if (pluginInterfaceType != null)
                {
                    fileTransformationAssembly = asm;
                    break;
                }
            }

            if (fileTransformationAssembly == null || pluginInterfaceType == null)
            {
                logger?.LogDebug("[AnimeRecommendations] File Transformation plugin not yet available in current AppDomain/AssemblyLoadContext.");
                return;
            }

            var registerMethod = pluginInterfaceType.GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (registerMethod == null)
            {
                logger?.LogWarning("[AnimeRecommendations] RegisterTransformation method not found on {TypeName}.", pluginInterfaceType.FullName);
                return;
            }

            // Create payload with literal "index.html" as expected by File Transformation 3.0+
            var payloadNode = new JsonObject
            {
                ["id"] = TransformationId.ToString(),
                ["fileNamePattern"] = "index.html",
                ["callbackAssembly"] = typeof(FileTransformationIntegration).Assembly.FullName,
                ["callbackClass"] = typeof(FileTransformationIntegration).FullName,
                ["callbackMethod"] = nameof(Transform)
            };

            var paramType = registerMethod.GetParameters().FirstOrDefault()?.ParameterType;
            object payloadObj;

            if (paramType != null && paramType.FullName?.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase) == true)
            {
                var jsonStr = payloadNode.ToJsonString();
                var jObjectType = paramType.Assembly.GetType("Newtonsoft.Json.Linq.JObject")
                               ?? Type.GetType("Newtonsoft.Json.Linq.JObject, Newtonsoft.Json");
                var parseMethod = jObjectType?.GetMethod("Parse", new[] { typeof(string) });
                payloadObj = parseMethod?.Invoke(null, new object[] { jsonStr }) ?? payloadNode;
            }
            else
            {
                payloadObj = payloadNode;
            }

            registerMethod.Invoke(null, new[] { payloadObj });
            IsFileTransformationActive = true;
            logger?.LogInformation("[AnimeRecommendations] Successfully registered HTML transformation with File Transformation plugin for 'index.html'!");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[AnimeRecommendations] Failed to register with File Transformation plugin. Fallback to direct script serving.");
        }
    }

    /// <summary>
    /// Universal callback method invoked by File Transformation.
    /// Extracts the HTML contents from JObject, JsonObject, or string payloads,
    /// injects the script tag, and returns the modified HTML.
    /// </summary>
    /// <param name="payload">Payload containing HTML string or JObject.</param>
    /// <returns>Transformed HTML string.</returns>
    public static string Transform(object? payload)
    {
        if (payload == null)
        {
            return string.Empty;
        }

        string? html = null;

        if (payload is string s)
        {
            html = s;
        }
        else
        {
            // Case 1: Newtonsoft JObject indexer: payload["contents"]
            try
            {
                var indexer = payload.GetType().GetProperty("Item", new[] { typeof(string) });
                if (indexer != null)
                {
                    var token = indexer.GetValue(payload, new object[] { "contents" });
                    if (token != null)
                    {
                        html = token.ToString();
                    }
                }
            }
            catch { }

            // Case 2: System.Text.Json parsing
            if (string.IsNullOrEmpty(html))
            {
                try
                {
                    var jsonStr = payload.ToString();
                    if (!string.IsNullOrWhiteSpace(jsonStr) && jsonStr.TrimStart().StartsWith("{"))
                    {
                        using var doc = JsonDocument.Parse(jsonStr);
                        if (doc.RootElement.TryGetProperty("contents", out var elem))
                        {
                            html = elem.GetString();
                        }
                    }
                }
                catch { }
            }

            // Case 3: Property named "contents" or "Contents"
            if (string.IsNullOrEmpty(html))
            {
                var prop = payload.GetType().GetProperty("contents", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                        ?? payload.GetType().GetProperty("Contents", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    html = prop.GetValue(payload)?.ToString();
                }
            }

            // Case 4: If still null, check if payload.ToString() itself is HTML
            if (string.IsNullOrEmpty(html))
            {
                var str = payload.ToString();
                if (str != null && (str.Contains("<html", StringComparison.OrdinalIgnoreCase) || str.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)))
                {
                    html = str;
                }
            }
        }

        return TransformIndexHtml(html ?? string.Empty);
    }

    /// <summary>
    /// Injects the client-side script tag into index.html.
    /// </summary>
    /// <param name="html">The original or partially transformed index.html content.</param>
    /// <returns>The modified HTML string with recommendations script injected.</returns>
    public static string TransformIndexHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        if (html.Contains("/Recommendations/ClientScript.js", StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
        {
            return html.Replace("</body>", $"{ScriptTag}\n</body>", StringComparison.OrdinalIgnoreCase);
        }

        return html + ScriptTag;
    }
}
