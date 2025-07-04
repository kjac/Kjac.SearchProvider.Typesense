namespace Kjac.SearchProvider.Typesense.Configuration;

public sealed class SearcherOptions
{
    public int MaxFacetValues { get; set; } = 100;

    public int BoostFactorTextR1 { get; set; } = 6;

    public int BoostFactorTextR2 { get; set; } = 4;

    public int BoostFactorTextR3 { get; set; } = 2;
}
