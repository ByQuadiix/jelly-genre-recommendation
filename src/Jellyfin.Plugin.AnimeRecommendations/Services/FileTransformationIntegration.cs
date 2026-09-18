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
        try
        {
            var assemblies = AssemblyLoadContext.All
                .SelectMany(ctx => ctx.Assemblies)
                .Where(a => a.FullName?.Contains(".FileTransformation", StringComparison.OrdinalIgnoreCase) ?? false)
                .ToList();

            var fileTransformationAssembly = assemblies.FirstOrDefault();
            if (fileTransformationAssembly == null)
            {
                logger?.LogInformation("File Transformation plugin not detected. Client script can be included via Custom JS or File Transformation WebUI.");
                return;
            }

            var pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            if (pluginInterfaceType == null)
            {
                logger?.LogWarning("Found File Transformation assembly, but PluginInterface type was not found.");
                return;
            }

            var registerMethod = pluginInterfaceType.GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.Static);
            if (registerMethod == null)
            {
                logger?.LogWarning("RegisterTransformation method not found on PluginInterface.");
                return;
            }

            // Create payload object
            var payloadNode = new JsonObject
            {
                ["id"] = TransformationId.ToString(),
                ["fileNamePattern"] = "index\\.html",
                ["callbackAssembly"] = typeof(FileTransformationIntegration).Assembly.FullName,
                ["callbackClass"] = typeof(FileTransformationIntegration).FullName,
                ["callbackMethod"] = nameof(TransformIndexHtml)
            };

            // If the method expects Newtonsoft JObject, convert or pass accordingly
            var paramType = registerMethod.GetParameters().FirstOrDefault()?.ParameterType;
            object payloadObj;

            if (paramType != null && paramType.FullName?.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Deserialize using Newtonsoft via reflection or JSON string
                var jsonStr = payloadNode.ToJsonString();
                var jObjectType = paramType.Assembly.GetType("Newtonsoft.Json.Linq.JObject");
                var parseMethod = jObjectType?.GetMethod("Parse", new[] { typeof(string) });
                payloadObj = parseMethod?.Invoke(null, new object[] { jsonStr }) ?? payloadNode;
            }
            else
            {
                payloadObj = payloadNode;
            }

            registerMethod.Invoke(null, new[] { payloadObj });
            IsFileTransformationActive = true;
            logger?.LogInformation("Successfully registered HTML transformation with File Transformation plugin!");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to register with File Transformation plugin. Fallback to direct script serving.");
        }
    }

    /// <summary>
    /// Callback invoked by the File Transformation plugin when index.html is served.
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
