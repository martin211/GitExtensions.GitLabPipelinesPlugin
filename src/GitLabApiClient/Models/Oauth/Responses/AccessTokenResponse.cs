using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GitLabApiClient.Models.Oauth.Responses
{
    /// <summary>
    /// Converts a <see cref="DateTime"/> to and from Unix epoch time.
    /// </summary>
    public class UnixDateTimeConverter : DateTimeConverterBase
    {
        internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="JsonWriter"/> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            long seconds;

            if (value is DateTime dateTime)
            {
                seconds = (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalSeconds;
            }
#if HAVE_DATE_TIME_OFFSET
            else if (value is DateTimeOffset dateTimeOffset)
            {
                seconds = (long)(dateTimeOffset.ToUniversalTime() - UnixEpoch).TotalSeconds;
            }
#endif
            else
            {
                throw new JsonSerializationException("Expected date object value.");
            }

            if (seconds < 0)
            {
                throw new JsonSerializationException("Cannot convert date value that is before Unix epoch of 00:00:00 UTC on 1 January 1970.");
            }

            writer.WriteValue(seconds);
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="JsonReader"/> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing property value of the JSON that is being converted.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The object value.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            long seconds;

            if (reader.TokenType == JsonToken.Integer)
            {
                seconds = (long)reader.Value!;
            }
            else if (reader.TokenType == JsonToken.String)
            {
                if (!long.TryParse((string)reader.Value!, out seconds))
                {
                    throw new JsonSerializationException("Cannot convert invalid value.");
                }
            }
            else
            {
                throw new JsonSerializationException("Unexpected token parsing date. Expected Integer or String");
            }

            if (seconds >= 0)
            {
                DateTime d = UnixEpoch.AddSeconds(seconds);

#if HAVE_DATE_TIME_OFFSET
                Type t = (nullable)
                    ? Nullable.GetUnderlyingType(objectType)
                    : objectType;
                if (t == typeof(DateTimeOffset))
                {
                    return new DateTimeOffset(d, TimeSpan.Zero);
                }
#endif
                return d;
            }
            else
            {
                throw new JsonSerializationException("Cannot convert value that is before Unix epoch of 00:00:00 UTC on 1 January 1970.");
            }
        }
    }

    public class AccessTokenResponse
    {
        [JsonProperty("access_token")] public string AccessToken { get; set; }

        [JsonProperty("token_type")] public string TokenType { get; set; }

        [JsonProperty("refresh_token")] public string RefreshToken { get; set; }

        [JsonProperty("scope")] public string Scope { get; set; }

        [JsonProperty("created_at")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime? CreatedAt { get; set; }
    }
}
