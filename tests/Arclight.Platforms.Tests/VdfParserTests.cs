using Arclight.Platforms.Steam;

namespace Arclight.Platforms.Tests;

public class VdfParserTests
{
    [Fact]
    public void Parses_nested_objects_case_insensitively()
    {
        const string text = """
            "AppState"
            {
                "appid"     "730"
                "name"      "Counter-Strike 2"
                "UserConfig"
                {
                    "language"  "english"
                }
            }
            """;

        var root = VdfParser.Parse(text);

        Assert.Equal("730", root["appstate"]!.GetString("APPID"));
        Assert.Equal("english", root["AppState"]!["userconfig"]!.GetString("language"));
    }

    [Fact]
    public void Handles_escapes_comments_conditionals_and_unquoted_tokens()
    {
        const string text = """
            // comment line
            "root"
            {
                "path"  "C:\\Program Files (x86)\\Steam"   // trailing comment
                "quote" "say \"hi\""
                unquoted value
                "win"   "1" [$WIN32]
            }
            """;

        var root = VdfParser.Parse(text)["root"]!;

        Assert.Equal(@"C:\Program Files (x86)\Steam", root.GetString("path"));
        Assert.Equal("say \"hi\"", root.GetString("quote"));
        Assert.Equal("value", root.GetString("unquoted"));
        Assert.Equal("1", root.GetString("win"));
    }

    [Theory]
    [InlineData("\"a\" {")]
    [InlineData("\"a\" \"unterminated")]
    [InlineData("}")]
    public void Malformed_input_throws_a_format_exception(string text)
    {
        Assert.Throws<VdfFormatException>(() => VdfParser.Parse(text));
    }
}
