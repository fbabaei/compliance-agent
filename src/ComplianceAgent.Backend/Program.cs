using System.Text.Json;
using ComplianceAgent.Services;

// --- Parse CLI arguments ---
string? fileInputArg = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] is "--file-input" && i + 1 < args.Length)
    {
        fileInputArg = args[++i];
    }
}

// --- Load settings ---
const string SettingsFileName = "appsettings.json";
var settingsPath = ResolveSettingsPath(SettingsFileName)
    ?? throw new FileNotFoundException(
        $"Could not find '{SettingsFileName}'. Place it in repo root or in 'src\\ComplianceAgent.Backend'.");

var json = await File.ReadAllTextAsync(settingsPath);
var settings = JsonSerializer.Deserialize<BackendSettings>(json, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
}) ?? throw new InvalidOperationException("Could not parse appsettings.json.");

if (string.IsNullOrWhiteSpace(settings.Foundry.Endpoint))
    throw new InvalidOperationException("Foundry:Endpoint is required in appsettings.json.");
if (string.IsNullOrWhiteSpace(settings.Foundry.Model))
    throw new InvalidOperationException("Foundry:Model is required in appsettings.json.");

// CLI --file-input overrides appsettings.json InputFile
var configuredInput = fileInputArg ?? settings.InputFile;
if (string.IsNullOrWhiteSpace(configuredInput))
    throw new InvalidOperationException("No input file specified. Use --file-input <path> or set InputFile in appsettings.json.");

var inputFile = ResolveInputFilePath(configuredInput, Path.GetDirectoryName(settingsPath)!)
    ?? throw new FileNotFoundException($"Input file not found: {configuredInput}");

Console.WriteLine($"Processing: {Path.GetFileName(inputFile)}");

// --- Load prompts ---
var promptPackage = PromptLibrary.GetPackage(settings.Foundry.PromptVersion);
Console.WriteLine($"Using prompt package version: {promptPackage.Version}");

var agentInstructions = await LoadPromptTextAsync(
    "ExtractionAgentInstructions.txt",
    promptPackage.AgentInstructions);

var extractionPromptTemplate = await LoadPromptTextAsync(
    "ExtractionPrompt.txt",
    promptPackage.ExtractionPromptTemplate);

var extractionPrompt = extractionPromptTemplate.Replace("{{input_file}}", Path.GetFileName(inputFile));
extractionPrompt = extractionPrompt.Replace("{{file_name}}", Path.GetFileName(inputFile));

// --- Run extraction ---
var foundrySettings = new FoundrySettings
{
    Endpoint = settings.Foundry.Endpoint,
    Model = settings.Foundry.Model,
    AgentName = settings.Foundry.AgentName
};

var extractionService = new ExtractionService(foundrySettings);
var result = await extractionService.ExtractAsync(inputFile, agentInstructions, extractionPrompt);

if (result.Success)
{
    var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "extractedjson");
    Directory.CreateDirectory(outputDir);
    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var outputFileName = $"{Path.GetFileNameWithoutExtension(inputFile)}_extracted_{timestamp}.json";
    var outputPath = Path.Combine(outputDir, outputFileName);
    await File.WriteAllTextAsync(outputPath, result.Json);

    Console.WriteLine($"Extraction complete. Output saved to: {outputPath}");
    Console.WriteLine();
    Console.WriteLine(result.Json);
}
else
{
    Console.WriteLine($"Warning: {result.Error}");
    Console.WriteLine("Raw output:");
    Console.WriteLine(result.Json);
}

// --- Helpers ---

static string? ResolveSettingsPath(string fileName)
{
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), fileName),
        Path.Combine(Directory.GetCurrentDirectory(), "src", "ComplianceAgent.Backend", fileName),
        Path.Combine(AppContext.BaseDirectory, fileName),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", fileName))
    };

    return candidates.FirstOrDefault(File.Exists);
}

static string? ResolveInputFilePath(string configuredPath, string settingsDirectory)
{
    if (Path.IsPathRooted(configuredPath))
    {
        return File.Exists(configuredPath) ? configuredPath : null;
    }

    var candidates = new[]
    {
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath)),
        Path.GetFullPath(Path.Combine(settingsDirectory, configuredPath))
    };

    return candidates.FirstOrDefault(File.Exists);
}

static string? ResolvePromptsDirectory()
{
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "src", "prompts"),
        Path.Combine(Directory.GetCurrentDirectory(), "src", "ComplianceAgent.Backend", "prompts"),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "prompts")),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "ComplianceAgent.Backend", "prompts")),
        Path.Combine(AppContext.BaseDirectory, "prompts")
    };

    return candidates.FirstOrDefault(Directory.Exists);
}

static async Task<string> LoadPromptTextAsync(string fileName, string fallback)
{
    var promptsDir = ResolvePromptsDirectory();
    if (!string.IsNullOrWhiteSpace(promptsDir))
    {
        var filePath = Path.Combine(promptsDir, fileName);
        if (File.Exists(filePath))
        {
            return await File.ReadAllTextAsync(filePath);
        }
    }

    Console.WriteLine($"Prompt file '{fileName}' not found. Using built-in default template.");
    return fallback;
}

public sealed record PromptPackage(
    string Version,
    string AgentInstructions,
    string ExtractionPromptTemplate);

public static class PromptLibrary
{
    public static PromptPackage GetPackage(string? requestedVersion)
    {
        if (string.Equals(requestedVersion, "v1", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(requestedVersion))
        {
            return V1;
        }

        Console.WriteLine($"Unknown prompt version '{requestedVersion}'. Falling back to v1.");
        return V1;
    }

    private static readonly PromptPackage V1 = new(
        Version: "v1",
        AgentInstructions: """
                        You are an AI system that extracts structured arrangement details from source text.

                        Rules:
                        - Use only explicit facts from the source.
                        - Never invent missing values.
                        - Return null for missing single-value fields and [] for missing list fields.
                        - Return JSON only.
            """,
        ExtractionPromptTemplate: """
                        Convert source file '{{input_file}}' into structured draft JSON.

            Rules:
            - Extract only explicit facts from the source.
            - Do not infer, normalize, or guess values.
                        - Keep unknown fields as null (or [] for arrays).
            - Do not add fields that are not in the schema.
            - Always set "status" to "draft".

                        Output schema:
            {
              "arrangementId": null,
              "country": null,
              "entities": [],
              "description": null,
              "transactionType": null,
              "status": "draft"
            }

                        Return valid JSON only.
            """);
}

public class BackendSettings
{
    public FoundrySettings Foundry { get; set; } = new();
    public string InputFile { get; set; } = "data\\Output.json";
}
