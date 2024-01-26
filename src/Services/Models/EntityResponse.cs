using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Marketplace.SaaS.Accelerator.Services.Models;
public class EntityResponse
{
    [JsonProperty("value")]
    public List<JObject> Value { get; set; }

    [JsonProperty("@odata.nextLink")]
    public string NextLink { get; set; }

    [JsonProperty("@odata.context")]
    public string Context { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("displayName")]
    public string DisplayName { get; set; }
}
