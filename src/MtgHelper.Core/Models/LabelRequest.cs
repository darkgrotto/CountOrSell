using System.Text.Json.Serialization;

namespace MtgHelper.Core.Models;

public class LabelRequest
{
    [JsonPropertyName("setCode")]
    public string SetCode { get; set; } = string.Empty;

    [JsonPropertyName("boxType")]
    public string BoxType { get; set; } = "Set Box";
}
