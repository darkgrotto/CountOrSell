namespace MtgHelper.Core.Models;

/// <summary>
/// Canonical tag values that can be applied to a set.
/// Tags that map directly from Scryfall's set_type are applied automatically on sync.
/// Others (universes_beyond, art, dci, secret_lair, reprint) must be applied manually via the API.
/// </summary>
public static class KnownSetTags
{
    // ── Auto-tagged from Scryfall set_type ────────────────────────────────────
    public const string Core            = "core";
    public const string Expansion       = "expansion";
    public const string Masters         = "masters";
    public const string Commander       = "commander";
    public const string DraftInnovation = "draft_innovation";
    public const string Starter         = "starter";
    public const string Funny           = "funny";
    public const string Memorabilia     = "memorabilia";
    public const string Token           = "token";
    public const string Promo           = "promo";
    public const string Planechase      = "planechase";
    public const string Archenemy       = "archenemy";
    public const string Alchemy         = "alchemy";
    public const string Digital         = "digital";       // treasure_chest / Arena-only

    // ── Manually applied ─────────────────────────────────────────────────────
    public const string UniversesBeyond = "universes_beyond"; // cross-IP sets
    public const string Art             = "art";              // Art Series cards
    public const string Dci             = "dci";              // DCI/WPN promo events
    public const string SecretLair      = "secret_lair";      // Secret Lair drops
    public const string Reprint         = "reprint";          // reprint-focused products

    /// <summary>Every valid tag value, for validation.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Core, Expansion, Masters, Commander, DraftInnovation,
        Starter, Funny, Memorabilia, Token, Promo, Planechase, Archenemy,
        Alchemy, Digital, UniversesBeyond, Art, Dci, SecretLair, Reprint
    };

    /// <summary>
    /// Returns the tag that should be auto-applied for the given Scryfall set_type,
    /// or <c>null</c> if no automatic mapping exists.
    /// </summary>
    public static string? FromSetType(string setType) => setType switch
    {
        "core"            => Core,
        "expansion"       => Expansion,
        "masters"         => Masters,
        "commander"       => Commander,
        "draft_innovation"=> DraftInnovation,
        "starter"         => Starter,
        "funny"           => Funny,
        "memorabilia"     => Memorabilia,
        "token"           => Token,
        "promo"           => Promo,
        "planechase"      => Planechase,
        "archenemy"       => Archenemy,
        "alchemy"         => Alchemy,
        "treasure_chest"  => Digital,
        _                 => null
    };
}
