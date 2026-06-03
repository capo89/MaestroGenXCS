namespace MaestroGenXcs.Domain;

/// <summary>
/// Typ korpusu skrinky (podľa vzorov v <c>VzoryXcs/Korpus_Skrinka_Bok_*</c>).
/// Referenčný bok ostáva Bok L (prípadne Bok P).
/// </summary>
public enum AssemblyCorpusMode
{
    /// <summary>Boky medzi dnom a vrchom; dno/vrch prekrývajú boky (širší panel).</summary>
    BokVlozeny,

    /// <summary>Boky po celej výške; dno/vrch vložené medzi bokmi (užší panel).</summary>
    BokNalozeny
}
