using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CompanyInfo.Api.Shared.Converters;

public class JsonDateTimeConverter : JsonConverter<DateTime>
{
    private static readonly Regex DateRegex = new Regex(
        @"\/Date\((\-?\d+)[+-](\d{4})?\)\/",
        RegexOptions.Compiled
    );

    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var dateString = reader.GetString();
            if (dateString != null)
            {
                if (DateRegex.IsMatch(dateString))
                {
                    var match = DateRegex.Match(dateString);
                    var milliseconds = long.Parse(match.Groups[1].Value);
                    var timeZone = match.Groups[2].Value;
                    if (timeZone != "")
                    {
                        TimeSpan.TryParseExact(
                            timeZone,
                            "hhmm",
                            CultureInfo.InvariantCulture,
                            out var offset
                        );
                        return DateTimeOffset
                            .FromUnixTimeMilliseconds(milliseconds)
                            .UtcDateTime.Add(offset);
                    }
                }
            }
        }
        return reader.GetDateTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("o"));
    }
}
