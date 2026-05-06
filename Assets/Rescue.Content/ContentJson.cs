using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace Rescue.Content
{
    public sealed class ContentJsonException : Exception
    {
        public ContentJsonException(string message, string? path, Exception? innerException)
            : base(message, innerException)
        {
            Path = path;
        }

        public string? Path { get; }
    }

    public static class ContentJson
    {
        private static readonly object Gate = new object();
        private static Assembly? _jsonAssembly;
        private static object? _serializerOptions;
        private static MethodInfo? _deserializeMethod;
        private static MethodInfo? _serializeMethod;
        private static JsonBackend _backend;

        private enum JsonBackend
        {
            None,
            SystemTextJson,
            NewtonsoftJson,
        }

        public static LevelJson DeserializeLevel(string json)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            object? value = DeserializeValue(json, typeof(LevelJson));
            if (value is not LevelJson level)
            {
                throw new ContentJsonException("Level JSON did not produce a schema object.", null, innerException: null);
            }

            return level;
        }

        public static LevelPacketManifest DeserializeLevelPacketManifest(string json)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            object? value = DeserializeValue(json, typeof(LevelPacketManifest));
            if (value is not LevelPacketManifest manifest)
            {
                throw new ContentJsonException("Level packet manifest JSON did not produce a schema object.", null, innerException: null);
            }

            return manifest;
        }

        public static LevelBrief DeserializeLevelBrief(string json)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            object? value = DeserializeValue(json, typeof(LevelBrief));
            if (value is not LevelBrief brief)
            {
                throw new ContentJsonException("Level brief JSON did not produce a schema object.", null, innerException: null);
            }

            return brief;
        }

        public static string SerializeLevel(LevelJson level)
        {
            if (level is null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            EnsureInitialized();

            try
            {
                object? value = _backend switch
                {
                    JsonBackend.SystemTextJson => _serializeMethod!.Invoke(null, new object?[] { level, typeof(LevelJson), _serializerOptions }),
                    JsonBackend.NewtonsoftJson => _serializeMethod!.Invoke(
                        null,
                        new object?[] { level, typeof(LevelJson), ((NewtonsoftInvocationSettings)_serializerOptions!).Formatting, ((NewtonsoftInvocationSettings)_serializerOptions).Settings }),
                    _ => throw new InvalidOperationException("JSON serializer was not initialized."),
                };
                if (value is not string json)
                {
                    throw new ContentJsonException("Level JSON serialization failed.", null, innerException: null);
                }

                return json;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw WrapJsonException(ex.InnerException);
            }
        }

        private static void EnsureInitialized()
        {
            if (_jsonAssembly is not null)
            {
                return;
            }

            lock (Gate)
            {
                if (_jsonAssembly is not null)
                {
                    return;
                }

                InitializeNewtonsoftJson();
            }
        }

        private static object? DeserializeValue(string json, Type type)
        {
            EnsureInitialized();

            MethodInfo? deserializeMethod = _deserializeMethod;
            object? serializerOptions = _serializerOptions;
            if (deserializeMethod is null || serializerOptions is null)
            {
                throw new InvalidOperationException("JSON serializer was not initialized.");
            }

            try
            {
                return _backend switch
                {
                    JsonBackend.SystemTextJson => deserializeMethod.Invoke(null, new object?[] { json, type, serializerOptions }),
                    JsonBackend.NewtonsoftJson when serializerOptions is NewtonsoftInvocationSettings settings => deserializeMethod.Invoke(
                        null,
                        new object?[] { json, type, settings.Settings }),
                    JsonBackend.NewtonsoftJson => throw new InvalidOperationException("Newtonsoft.Json serializer settings were not initialized."),
                    _ => throw new InvalidOperationException("JSON serializer was not initialized."),
                };
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw WrapJsonException(ex.InnerException);
            }
        }

        private static bool TryInitializeSystemTextJson()
        {
            Assembly jsonAssembly;
            try
            {
                jsonAssembly = LoadJsonAssembly();
            }
            catch
            {
                return false;
            }

            Type serializerType = RequireType(jsonAssembly, "System.Text.Json.JsonSerializer");
            Type optionsType = RequireType(jsonAssembly, "System.Text.Json.JsonSerializerOptions");
            MethodInfo? deserializeMethod = serializerType.GetMethod(
                "Deserialize",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(Type), optionsType },
                modifiers: null);
            MethodInfo? serializeMethod = serializerType.GetMethod(
                "Serialize",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(object), typeof(Type), optionsType },
                modifiers: null);

            if (deserializeMethod is null || serializeMethod is null)
            {
                return false;
            }

            Type namingPolicyType = RequireType(jsonAssembly, "System.Text.Json.JsonNamingPolicy");
            Type converterType = RequireType(jsonAssembly, "System.Text.Json.Serialization.JsonStringEnumConverter");

            object options = Activator.CreateInstance(optionsType)
                ?? throw new InvalidOperationException("Unable to create JsonSerializerOptions.");
            SetProperty(optionsType, options, "PropertyNameCaseInsensitive", true);
            SetProperty(optionsType, options, "WriteIndented", true);
            object? camelCase = namingPolicyType.GetProperty("CamelCase", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            SetProperty(optionsType, options, "PropertyNamingPolicy", camelCase);

            object converters = optionsType.GetProperty("Converters", BindingFlags.Public | BindingFlags.Instance)?.GetValue(options)
                ?? throw new InvalidOperationException("Unable to access JsonSerializerOptions.Converters.");
            object converter = Activator.CreateInstance(converterType)
                ?? throw new InvalidOperationException("Unable to create JsonStringEnumConverter.");
            converters.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance)?.Invoke(converters, new[] { converter });

            _jsonAssembly = jsonAssembly;
            _serializerOptions = options;
            _deserializeMethod = deserializeMethod;
            _serializeMethod = serializeMethod;
            _backend = JsonBackend.SystemTextJson;
            return true;
        }

        private static void InitializeNewtonsoftJson()
        {
            Assembly jsonAssembly = LoadNewtonsoftAssembly();
            Type jsonConvertType = RequireNewtonsoftType(jsonAssembly, "Newtonsoft.Json.JsonConvert");
            Type settingsType = RequireNewtonsoftType(jsonAssembly, "Newtonsoft.Json.JsonSerializerSettings");
            Type formattingType = RequireNewtonsoftType(jsonAssembly, "Newtonsoft.Json.Formatting");
            Type contractResolverType = RequireNewtonsoftType(jsonAssembly, "Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver");
            Type stringEnumConverterType = RequireNewtonsoftType(jsonAssembly, "Newtonsoft.Json.Converters.StringEnumConverter");

            object settings = Activator.CreateInstance(settingsType)
                ?? throw new InvalidOperationException("Unable to create JsonSerializerSettings.");
            object contractResolver = Activator.CreateInstance(contractResolverType)
                ?? throw new InvalidOperationException("Unable to create CamelCasePropertyNamesContractResolver.");
            SetProperty(settingsType, settings, "ContractResolver", contractResolver);

            object converters = settingsType.GetProperty("Converters", BindingFlags.Public | BindingFlags.Instance)?.GetValue(settings)
                ?? throw new InvalidOperationException("Unable to access JsonSerializerSettings.Converters.");
            object converter = Activator.CreateInstance(stringEnumConverterType)
                ?? throw new InvalidOperationException("Unable to create StringEnumConverter.");
            converters.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance)?.Invoke(converters, new[] { converter });

            MethodInfo deserializeMethod = jsonConvertType.GetMethod(
                "DeserializeObject",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(Type), settingsType },
                modifiers: null)
                ?? throw new MissingMethodException("JsonConvert.DeserializeObject(string, Type, JsonSerializerSettings) was not found.");
            MethodInfo serializeMethod = jsonConvertType.GetMethod(
                "SerializeObject",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(object), typeof(Type), formattingType, settingsType },
                modifiers: null)
                ?? throw new MissingMethodException("JsonConvert.SerializeObject(object, Type, Formatting, JsonSerializerSettings) was not found.");

            object indented = Enum.Parse(formattingType, "Indented");
            _jsonAssembly = jsonAssembly;
            _serializerOptions = new NewtonsoftInvocationSettings(settings, indented);
            _deserializeMethod = deserializeMethod;
            _serializeMethod = serializeMethod;
            _backend = JsonBackend.NewtonsoftJson;
        }

        private sealed record NewtonsoftInvocationSettings(object Settings, object Formatting);

        private static Assembly LoadNewtonsoftAssembly()
        {
            Assembly? loaded = FindLoadedAssembly("Newtonsoft.Json");
            if (loaded is not null)
            {
                return loaded;
            }

            try
            {
                return Assembly.Load(new AssemblyName("Newtonsoft.Json"));
            }
            catch
            {
                // Fall through to Unity package cache probing.
            }

            string baseDirectory = Directory.GetCurrentDirectory();
            string packageCacheDirectory = Path.Combine(baseDirectory, "Library", "PackageCache");
            if (Directory.Exists(packageCacheDirectory))
            {
                string[] candidates = Directory.GetFiles(packageCacheDirectory, "Newtonsoft.Json.dll", SearchOption.AllDirectories);
                for (int i = 0; i < candidates.Length; i++)
                {
                    Assembly assembly = Assembly.LoadFrom(candidates[i]);
                    if (string.Equals(assembly.GetName().Name, "Newtonsoft.Json", StringComparison.Ordinal))
                    {
                        return assembly;
                    }
                }
            }

            throw new FileNotFoundException("Newtonsoft.Json.dll could not be located for runtime loading.");
        }

        private static Type RequireNewtonsoftType(Assembly assembly, string fullName)
        {
            Type? type = assembly.GetType(fullName, throwOnError: false);
            if (type is null)
            {
                throw new TypeLoadException($"Type '{fullName}' was not found in Newtonsoft.Json.");
            }

            return type;
        }

        private static Assembly LoadJsonAssembly()
        {
            Assembly? loaded = FindLoadedAssembly("System.Text.Json");
            if (loaded is not null)
            {
                return loaded;
            }

            try
            {
                return Assembly.Load(new AssemblyName("System.Text.Json"));
            }
            catch
            {
                // Fall through to workspace-local plugin loading for Unity/Mono environments.
            }

            string baseDirectory = Directory.GetCurrentDirectory();
            string pluginDirectory = Path.Combine(baseDirectory, "Assets", "Plugins");
            string[] dependencyNames =
            {
                "Microsoft.Bcl.AsyncInterfaces.dll",
                "System.Memory.dll",
                "System.Buffers.dll",
                "System.IO.Pipelines.dll",
                "System.Runtime.CompilerServices.Unsafe.dll",
                "System.Text.Encodings.Web.dll",
                "System.Threading.Tasks.Extensions.dll",
                "System.Text.Json.dll",
            };

            Assembly? mainAssembly = null;
            for (int i = 0; i < dependencyNames.Length; i++)
            {
                string assemblyPath = Path.Combine(pluginDirectory, dependencyNames[i]);
                if (!File.Exists(assemblyPath))
                {
                    continue;
                }

                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                if (string.Equals(assembly.GetName().Name, "System.Text.Json", StringComparison.Ordinal))
                {
                    mainAssembly = assembly;
                }
            }

            if (mainAssembly is not null)
            {
                return mainAssembly;
            }

            throw new FileNotFoundException("System.Text.Json.dll could not be located for runtime loading.");
        }

        private static Assembly? FindLoadedAssembly(string simpleName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                AssemblyName name = assemblies[i].GetName();
                if (string.Equals(name.Name, simpleName, StringComparison.Ordinal))
                {
                    return assemblies[i];
                }
            }

            return null;
        }

        private static Type RequireType(Assembly assembly, string fullName)
        {
            Type? type = assembly.GetType(fullName, throwOnError: false);
            if (type is null)
            {
                throw new TypeLoadException($"Type '{fullName}' was not found in System.Text.Json.");
            }

            return type;
        }

        private static void SetProperty(Type declaringType, object instance, string propertyName, object? value)
        {
            PropertyInfo property = declaringType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMemberException(declaringType.FullName, propertyName);
            property.SetValue(instance, value);
        }

        private static ContentJsonException WrapJsonException(Exception exception)
        {
            Type exceptionType = exception.GetType();
            if (!string.Equals(exceptionType.FullName, "System.Text.Json.JsonException", StringComparison.Ordinal))
            {
                return new ContentJsonException(exception.Message, null, exception);
            }

            string? path = exceptionType.GetProperty("Path", BindingFlags.Public | BindingFlags.Instance)?.GetValue(exception) as string;
            return new ContentJsonException(exception.Message, path, exception);
        }
    }
}
