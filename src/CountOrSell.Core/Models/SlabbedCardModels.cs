namespace CountOrSell.Core.Models;

public record SlabbedCardRequest(
    string ScryfallCardId,
    string CardName,
    string SetCode,
    string SetName,
    string CollectorNumber,
    string CardVariant,
    string GradingCompany,
    string Grade,
    string CertificationNumber,
    DateTime? PurchaseDate,
    string? PurchasedFrom,
    decimal? PurchaseCost,
    string? Notes);
