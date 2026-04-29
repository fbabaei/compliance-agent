using System.Text.Json;
using ComplianceAgent.Services;

// --- Parse CLI arguments ---
string? fileInputArg = null;
string? textInputArg = null;
string? textFileArg = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] is "--file-input" && i + 1 < args.Length)
    {
        fileInputArg = args[++i];
    }
    else if (args[i] is "--text-input" && i + 1 < args.Length)
    {
        textInputArg = args[++i];
    }
    else if (args[i] is "--text-file" && i + 1 < args.Length)
    {
        textFileArg = args[++i];
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

var inputSource = ResolveInputSource(
    fileInputArg,
    textInputArg,
    textFileArg,
    settings,
    Path.GetDirectoryName(settingsPath)!);

Console.WriteLine($"Processing source: {inputSource.DisplayName} ({inputSource.Mode})");

// --- Load prompts ---
var promptPackage = PromptLibrary.GetPackage(null);
Console.WriteLine($"Using prompt package version: {promptPackage.Version}");

var agentInstructions = await LoadPromptTextAsync(
    "ExtractionAgentInstructions.txt",
    promptPackage.AgentInstructions);

var extractionPromptTemplate = await LoadPromptTextAsync(
    "ExtractionPrompt.txt",
    promptPackage.ExtractionPromptTemplate);

var extractionPrompt = extractionPromptTemplate.Replace("{{input_file}}", Path.GetFileName(inputSource.FilePath));
extractionPrompt = extractionPrompt.Replace("{{file_name}}", Path.GetFileName(inputSource.FilePath));

// --- Run extraction ---
var foundrySettings = new FoundrySettings
{
    Endpoint = settings.Foundry.Endpoint,
    Model = settings.Foundry.Model,
    AgentName = settings.Foundry.AgentName
};

var extractionService = new ExtractionService(foundrySettings);
var result = await extractionService.ExtractAsync(inputSource.FilePath, agentInstructions, extractionPrompt);

var validationEnabled = settings.Validation.Enabled;
var maxAttempts = Math.Max(1, settings.Validation.MaxAttempts);

for (int attempt = 1; attempt < maxAttempts; attempt++)
{
    if (!validationEnabled)
    {
        break;
    }

    var validation = JsonContractValidator.Validate(result.Json);
    if (result.Success && validation.IsValid)
    {
        break;
    }

    var feedback = BuildValidationFeedback(result, validation.Errors);
    Console.WriteLine($"Validation retry {attempt}/{maxAttempts - 1}: {feedback}");

    var retryPrompt = $"""
        {extractionPrompt}

        Your previous output failed validation.
        Fix these issues and return corrected JSON only:
        {feedback}
        """;

    result = await extractionService.ExtractAsync(inputSource.FilePath, agentInstructions, retryPrompt);
}

if (result.Success)
{
    var finalValidation = JsonContractValidator.Validate(result.Json);
    if (!finalValidation.IsValid)
    {
        Console.WriteLine("Warning: output JSON does not match required schema after retries.");
        foreach (var error in finalValidation.Errors)
        {
            Console.WriteLine($"- {error}");
        }
    }

    var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "extractedjson");
    Directory.CreateDirectory(outputDir);
    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var outputFileName = $"{Path.GetFileNameWithoutExtension(inputSource.FilePath)}_extracted_{timestamp}.json";
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

if (inputSource.DeleteAfterRun && File.Exists(inputSource.FilePath))
{
    File.Delete(inputSource.FilePath);
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

static InputSource ResolveInputSource(
    string? fileInputArg,
    string? textInputArg,
    string? textFileArg,
    BackendSettings settings,
    string settingsDirectory)
{
    if (!string.IsNullOrWhiteSpace(fileInputArg))
    {
        var filePath = ResolveInputFilePath(fileInputArg, settingsDirectory)
            ?? throw new FileNotFoundException($"Input file not found: {fileInputArg}");
        return new InputSource(filePath, Path.GetFileName(filePath), "file", false);
    }

    if (!string.IsNullOrWhiteSpace(textInputArg))
    {
        var tempPath = WriteTemporaryTextInput(textInputArg);
        return new InputSource(tempPath, "inline-text", "text", true);
    }

    if (!string.IsNullOrWhiteSpace(textFileArg))
    {
        var textFilePath = ResolveInputFilePath(textFileArg, settingsDirectory)
            ?? throw new FileNotFoundException($"Text file not found: {textFileArg}");
        var textContent = File.ReadAllText(textFilePath);
        var tempPath = WriteTemporaryTextInput(textContent);
        return new InputSource(tempPath, Path.GetFileName(textFilePath), "text-file", true);
    }

    if (!string.IsNullOrWhiteSpace(settings.InputFile))
    {
        var filePath = ResolveInputFilePath(settings.InputFile, settingsDirectory)
            ?? throw new FileNotFoundException($"Input file not found: {settings.InputFile}");
        return new InputSource(filePath, Path.GetFileName(filePath), "file", false);
    }

    if (!string.IsNullOrWhiteSpace(settings.InputText))
    {
        var tempPath = WriteTemporaryTextInput(settings.InputText);
        return new InputSource(tempPath, "configured-text", "text", true);
    }

    throw new InvalidOperationException("No input source specified. Use --file-input, --text-input, --text-file, InputFile, or InputText.");
}

static string WriteTemporaryTextInput(string text)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "compliance-agent-input");
    Directory.CreateDirectory(tempRoot);
    var path = Path.Combine(tempRoot, $"input_{Guid.NewGuid():N}.txt");
    File.WriteAllText(path, text);
    return path;
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

static string BuildValidationFeedback(ExtractionResult result, IReadOnlyList<string> schemaErrors)
{
    if (!result.Success)
    {
        return string.IsNullOrWhiteSpace(result.Error)
            ? "Extraction failed without detailed error. Return strictly valid JSON."
            : result.Error;
    }

    if (schemaErrors.Count == 0)
    {
        return "Response is valid JSON but did not pass strict schema validation.";
    }

    return string.Join("; ", schemaErrors);
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
            You are an AI system that extracts structured MDR arrangement data from source text.

            Core behavior:
            - Extract only information explicitly present in the source.
            - Never infer, assume, or invent missing values.
            - If a field is missing, return null (or [] for entities).
            - Output must be deterministic and JSON-only.
            - Do not output markdown or explanations.
            """,
        ExtractionPromptTemplate: """
            Convert the source file '{{input_file}}' into a structured MDR draft JSON.

            Rules:
            - Extract only explicit facts from the source.
            - Do not infer, normalize, or guess values.
            - Keep incomplete fields as null (or [] for arrays).
            - Do not add fields that are not in the schema.
            - Always set "status" to "draft".

            Field definitions:
            - arrangementId: Arrangement identifier if explicitly present.
            - country: Country linked to the arrangement.
            - entities: Explicitly named entities involved in the arrangement.
            - description: Concise summary strictly grounded in source text.
            - transactionType: Explicit transaction category if stated.

            Output JSON schema (exact):
            {
              "arrangementId": null,
              "country": null,
              "entities": [],
              "description": null,
              "transactionType": null,
              "status": "draft"
            }

            Example behavior:
            If source says: "ABC GmbH entered a financing agreement in Germany."
            Then valid output may be:
            {
              "arrangementId": null,
              "country": "Germany",
              "entities": ["ABC GmbH"],
              "description": "Financing agreement",
              "transactionType": null,
              "status": "draft"
            }

            Return only valid JSON.
            """);
}

public sealed record InputSource(
    string FilePath,
    string DisplayName,
    string Mode,
    bool DeleteAfterRun);

public sealed record JsonValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public static class JsonContractValidator
{
    private static readonly HashSet<string> RequiredProperties =
    [
        "arrangementId",
        "country",
        "entities",
        "description",
        "transactionType",
        "status"
    ];

    public static JsonValidationResult Validate(string json)
    {
        var errors = new List<string>();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new JsonValidationResult(false, [$"Invalid JSON: {ex.Message}"]);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new JsonValidationResult(false, ["Root element must be a JSON object."]);
            }

            var actualProperties = root.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

            foreach (var required in RequiredProperties)
            {
                if (!actualProperties.Contains(required))
                {
                    errors.Add($"Missing required property '{required}'.");
                }
            }

            foreach (var name in actualProperties)
            {
                if (!RequiredProperties.Contains(name))
                {
                    errors.Add($"Unexpected property '{name}'.");
                }
            }

            ValidateNullableString(root, "arrangementId", errors);
            ValidateNullableString(root, "country", errors);
            ValidateNullableString(root, "description", errors);
            ValidateNullableString(root, "transactionType", errors);

            if (root.TryGetProperty("entities", out var entities))
            {
                if (entities.ValueKind != JsonValueKind.Array)
                {
                    errors.Add("Property 'entities' must be an array.");
                }
                else
                {
                    foreach (var entity in entities.EnumerateArray())
                    {
                        if (entity.ValueKind != JsonValueKind.String)
                        {
                            errors.Add("All 'entities' entries must be strings.");
                            break;
                        }
                    }
                }
            }

            if (root.TryGetProperty("status", out var status))
            {
                if (status.ValueKind != JsonValueKind.String)
                {
                    errors.Add("Property 'status' must be a string value 'draft'.");
                }
                else if (!string.Equals(status.GetString(), "draft", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Property 'status' must be 'draft'.");
                }
            }
        }

        return new JsonValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateNullableString(JsonElement root, string property, ICollection<string> errors)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            return;
        }

        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.String)
        {
            return;
        }

        errors.Add($"Property '{property}' must be string or null.");
    }
}

public class BackendSettings
{
    public FoundrySettings Foundry { get; set; } = new();
    public string InputFile { get; set; } = "data\\Output.json";
    public string InputText { get; set; } = string.Empty;
    public ValidationSettings Validation { get; set; } = new();
}

public class ValidationSettings
{
    public bool Enabled { get; set; } = true;
    public int MaxAttempts { get; set; } = 2;
}
