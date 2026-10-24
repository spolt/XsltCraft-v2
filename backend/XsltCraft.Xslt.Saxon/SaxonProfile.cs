namespace XsltCraft.Xslt.Saxon;

/// <summary>
/// Saxon yürütme profili. Sandbox (G/Ç, dinamik değerlendirme, collation) iki profilde AYNIDIR;
/// yalnız kaynak sınırları yürütme yerine göre değişir (ADR 008 §6–§7).
/// </summary>
public enum SaxonProfile
{
    /// <summary>İzole worker süreci (Raw / Validate / UserTemplate) — 64 MB yığınlı motor thread'i varsayılır.</summary>
    Isolated,

    /// <summary>Api süreci içi grid önizlemesi — 16 MB yığınlı guard thread'i varsayılır.</summary>
    InProcessGrid,
}
