namespace MaestroGenXcs.Domain;

/// <summary>Pozícia šufle v skrinke (od spodu nahor).</summary>
public enum SufelPozicia
{
    Nezadana = 0,
    Spodny = 1,
    Stredny = 2,
    Vrchny = 3,
}

public static class SufelPoziciaExtensions
{
    public static int SortOrder(this SufelPozicia pozicia) => pozicia switch
    {
        SufelPozicia.Spodny => 0,
        SufelPozicia.Stredny => 1,
        SufelPozicia.Vrchny => 2,
        _ => 99,
    };

    public static string ToShortLabel(this SufelPozicia pozicia) => pozicia switch
    {
        SufelPozicia.Spodny => "spodný",
        SufelPozicia.Stredny => "stredný",
        SufelPozicia.Vrchny => "vrchný",
        _ => "",
    };
}
