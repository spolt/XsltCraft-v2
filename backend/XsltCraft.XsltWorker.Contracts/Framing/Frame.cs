namespace XsltCraft.XsltWorker.Contracts.Framing;

/// <summary>
/// Çözülmüş bir çerçeve: JSON başlık baytları + ham UTF-8 gövdeler.
/// Gövdeler (XSLT, XML, HTML çıktısı) JSON'a gömülmez; kaçış/şişme maliyeti yoktur.
/// </summary>
public sealed record Frame(FrameType Type, byte[] Header, IReadOnlyList<byte[]> Bodies);
