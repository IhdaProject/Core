using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Core.Domain.Common;

[Keyless, ComplexType]
public class MultiLanguageField
{
    /// <summary>
    /// O'zbek (Lotin) tilida
    /// </summary>
    public string uz { get; set; } = string.Empty;

    /// <summary>
    /// Rus tilida
    /// </summary>
    public string ru { get; set; } = string.Empty;

    /// <summary>
    /// Ingliz tilida
    /// </summary>
    public string en { get; set; } = string.Empty;

    public static implicit operator MultiLanguageField(string data) => new()
    {
        ru = data,
        uz = data,
        en = data
    };

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }

    public string Label(string language)
        => language switch
        {
            "ru" => ru,
            "en" => en,
            _ => uz
        };
}
