using System;
using System.IO;
using Rescue.Content;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            PrintUsage();
            return 2;
        }

        try
        {
            return args[0] switch
            {
                "validate" => ValidateSingle(args[1]),
                "validate-all" => ValidateAll(args[1]),
                "preview" => Preview(args[1]),
                _ => UnknownCommand(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int ValidateSingle(string path)
    {
        string json = File.ReadAllText(path);
        ValidationResult result = Validator.Validate(json);
        WriteResult(path, result);
        return result.HasErrors ? 1 : 0;
    }

    private static int ValidateAll(string levelsDir)
    {
        string[] files = Directory.GetFiles(levelsDir, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        bool hasErrors = false;
        for (int i = 0; i < files.Length; i++)
        {
            string json = File.ReadAllText(files[i]);
            ValidationResult result = Validator.Validate(json);
            WriteResult(files[i], result);
            hasErrors |= result.HasErrors;
        }

        return hasErrors ? 1 : 0;
    }

    private static int Preview(string path)
    {
        string json = File.ReadAllText(path);
        ValidationResult result = Validator.Validate(json);
        WriteResult(path, result);

        LevelJson level;
        try
        {
            level = ContentJson.DeserializeLevel(json);
        }
        catch (ContentJsonException)
        {
            Console.WriteLine("Preview unavailable: JSON did not deserialize.");
            return 1;
        }

        Console.Write(AsciiPreview.Render(level));
        return result.HasErrors ? 1 : 0;
    }

    private static void WriteResult(string path, ValidationResult result)
    {
        Console.WriteLine(path);
        if (result.Errors.Count == 0)
        {
            Console.WriteLine("  OK");
            return;
        }

        for (int i = 0; i < result.Errors.Count; i++)
        {
            ValidationError error = result.Errors[i];
            Console.WriteLine($"  {error.Severity}: {error.Code} at {error.Path}");
            Console.WriteLine($"    {error.Message}");
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  validate <path>");
        Console.WriteLine("  validate-all <levels-dir>");
        Console.WriteLine("  preview <path>");
    }
}
