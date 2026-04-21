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

        public static LevelJson DeserializeLevel(string json)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            EnsureInitialized();

            try
            {
                object? value = _deserializeMethod!.Invoke(null, new object?[] { json, typeof(LevelJson), _serializerOptions });
                if (value is not LevelJson level)
                {
                    throw new ContentJsonException("Level JSON did not produce a schema object.", null, innerException: null);
                }

                return level;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw WrapJsonException(ex.InnerException);
            }
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
                object? value = _serializeMethod!.Invoke(null, new object?[] { level, typeof(LevelJson), _serializerOptions });
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

                Assembly jsonAssembly = LoadJsonAssembly();
                Type serializerType = RequireType(jsonAssembly, "System.Text.Json.JsonSerializer");
                Type optionsType = RequireType(jsonAssembly, "System.Text.Json.JsonSerializerOptions");
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

                MethodInfo deserializeMethod = serializerType.GetMethod(
                    "Deserialize",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(string), typeof(Type), optionsType },
                    modifiers: null)
                    ?? throw new MissingMethodException("JsonSerializer.Deserialize(string, Type, JsonSerializerOptions) was not found.");
                MethodInfo serializeMethod = serializerType.GetMethod(
                    "Serialize",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(object), typeof(Type), optionsType },
                    modifiers: null)
                    ?? throw new MissingMethodException("JsonSerializer.Serialize(object, Type, JsonSerializerOptions) was not found.");

                _jsonAssembly = jsonAssembly;
                _serializerOptions = options;
                _deserializeMethod = deserializeMethod;
                _serializeMethod = serializeMethod;
            }
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
