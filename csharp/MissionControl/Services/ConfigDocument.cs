using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MissionControl.Models;

namespace MissionControl.Services;

public sealed record ConfigDocument(string Source, ModConfig Settings)
{
    private static readonly JsonSerializerOptions Options = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static ConfigDocument Open(string path) => Parse(File.Exists(path) ? File.ReadAllText(path) : "{}");

    public static ConfigDocument Parse(string source)
    {
        var config = JsonSerializer.Deserialize<ModConfig>(source, Options) ?? throw new JsonException("Expected a settings object.");
        config.max_slots_by_prestige ??= [];
        config.trader_whitelist ??= [];
        config.reroll_traders ??= new ModConfig().reroll_traders;
        return new(source, config);
    }

    // Replace only edited property values. Keep the user's comments, formatting and unknown keys.
    public string Apply(ModConfig edited)
    {
        var original = JsonSerializer.SerializeToNode(Settings)!.AsObject();
        var changed = JsonSerializer.SerializeToNode(edited)!.AsObject()
            .Where(p => !JsonNode.DeepEquals(original[p.Key], p.Value))
            .ToDictionary(p => p.Key, p => p.Value!.ToJsonString());
        if (changed.Count == 0) return Source;

        var bytes = Encoding.UTF8.GetBytes(Source);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        reader.Read();
        var afterBrace = (int)reader.BytesConsumed;
        var edits = new List<(int Start, int End, string Text)>();
        bool hasProperties = false;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            hasProperties = true;
            var key = reader.GetString()!;
            reader.Read();
            var start = (int)reader.TokenStartIndex;
            reader.Skip();
            if (changed.Remove(key, out var replacement))
                edits.Add((start, (int)reader.BytesConsumed, replacement));
        }
        if (changed.Count > 0)
        {
            var newline = Source.Contains("\r\n") ? "\r\n" : "\n";
            var entries = string.Join("," + newline, changed.Select(p => $"  \"{p.Key}\": {p.Value}"));
            edits.Insert(0, (afterBrace, afterBrace, newline + entries + (hasProperties ? "," : newline)));
        }
        var result = new StringBuilder();
        int position = 0;
        foreach (var edit in edits)
        {
            result.Append(Encoding.UTF8.GetString(bytes, position, edit.Start - position)).Append(edit.Text);
            position = edit.End;
        }
        return result.Append(Encoding.UTF8.GetString(bytes, position, bytes.Length - position)).ToString();
    }
}
