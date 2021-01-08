using Newtonsoft.Json;

namespace GitLabApiClient.Models.Issues.Responses
{
    public sealed class ClosedBy : ModifiableObject
    {
        [JsonProperty("avatar_url")] public string AvatarUrl;

        [JsonProperty("name")] public string Name;
        [JsonProperty("active")] public string State;

        [JsonProperty("username")] public string Username;

        [JsonProperty("web_url")] public string WebUrl;
    }
}
