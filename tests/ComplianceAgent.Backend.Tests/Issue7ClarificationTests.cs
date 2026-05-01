using Xunit;

namespace ComplianceAgent.Backend.Tests;

public class Issue7ClarificationTests
{
    [Fact]
    public void DetectMissingFields_ReturnsAllMissing_WhenAllNullableValuesAreNull()
    {
        const string json = """
            {
              "arrangementId": null,
              "country": null,
              "entities": [],
              "description": null,
              "transactionType": null,
              "status": "draft"
            }
            """;

        var missing = ClarificationLoop.DetectMissingFields(json);

        Assert.Contains("arrangementId", missing);
        Assert.Contains("country", missing);
        Assert.Contains("entities", missing);
        Assert.Contains("description", missing);
        Assert.Contains("transactionType", missing);
        Assert.DoesNotContain("status", missing);
    }

    [Fact]
    public void DetectMissingFields_ReturnsEmpty_WhenAllFieldsHaveValues()
    {
        const string json = """
            {
              "arrangementId": "A-1",
              "country": "US",
              "entities": ["Acme"],
              "description": "Some description",
              "transactionType": "merger",
              "status": "draft"
            }
            """;

        var missing = ClarificationLoop.DetectMissingFields(json);

        Assert.Empty(missing);
    }

    [Fact]
    public void DetectMissingFields_TreatsEmptyStringAndEmptyArrayAsMissing()
    {
        const string json = """
            {
              "arrangementId": "",
              "country": "   ",
              "entities": [],
              "description": "ok",
              "transactionType": "ok",
              "status": "draft"
            }
            """;

        var missing = ClarificationLoop.DetectMissingFields(json);

        Assert.Contains("arrangementId", missing);
        Assert.Contains("country", missing);
        Assert.Contains("entities", missing);
        Assert.DoesNotContain("description", missing);
        Assert.DoesNotContain("transactionType", missing);
    }

    [Fact]
    public void BuildFollowUpQuestion_ListsMissingFields()
    {
        var question = ClarificationLoop.BuildFollowUpQuestion(new[] { "arrangementId", "country" });

        Assert.Contains("arrangementId", question);
        Assert.Contains("country", question);
        Assert.Contains("create", question);
        Assert.Contains("proceed", question);
        Assert.Contains("skip", question);
    }

    [Fact]
    public void MergeAnswerIntoPrompt_IncludesBasePromptQuestionAndAnswer()
    {
        const string basePrompt = "BASE_PROMPT_TEXT";
        const string suffix = "SUFFIX_TEXT";
        const string question = "What is the country?";
        const string answer = "United States";

        var merged = ClarificationLoop.MergeAnswerIntoPrompt(basePrompt, suffix, question, answer);

        Assert.Contains("BASE_PROMPT_TEXT", merged);
        Assert.Contains("SUFFIX_TEXT", merged);
        Assert.Contains("What is the country?", merged);
        Assert.Contains("United States", merged);
    }

    [Fact]
    public void SummarizeExtractedFields_ListsAllRequiredFieldsWithRenderedValues()
    {
        const string json = """
            {
              "arrangementId": "A-1",
              "country": null,
              "entities": ["Acme"],
              "description": "",
              "transactionType": "merger",
              "status": "draft"
            }
            """;

        var summary = ClarificationLoop.SummarizeExtractedFields(json);

        Assert.Contains("arrangementId", summary);
        Assert.Contains("\"A-1\"", summary);
        Assert.Contains("country", summary);
        Assert.Contains("null", summary);
        Assert.Contains("entities", summary);
        Assert.Contains("Acme", summary);
        Assert.Contains("description", summary);
        Assert.Contains("(empty)", summary);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("skip", true)]
    [InlineData("SKIP", true)]
    [InlineData("create", true)]
    [InlineData("Proceed", true)]
    [InlineData("done", true)]
    [InlineData("finalize", true)]
    [InlineData("United States", false)]
    [InlineData("the country is US", false)]
    public void IsFinalizeAnswer_RecognizesExitKeywordsAndEmpty(string? input, bool expected)
    {
        Assert.Equal(expected, ClarificationLoop.IsFinalizeAnswer(input));
    }
}
