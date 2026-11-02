using System.Xml;
using XsltCraft.Application.Validation.Safety;

namespace XsltCraft.Application.Xslt.Engines;

/// <summary>
/// Ham XSLT ve tema hattı motor seçimi (ADR 007 §2-3): <c>version="2.0"</c> bildiren stylesheet XSLT 1.0 motorunun
/// (.NET ileri-uyumlu kip) çalıştıramayacağı bir yapı kullanıyor mu? GİB şablonlarının çoğu <c>version="2.0"</c>
/// bildirip yalnız 1.0 kullanır (<c>xsl:character-map</c> gibi bildirimler .NET'te yok sayılır) — bunlar .NET'te
/// kalır, çıktıları bit-aynıdır. Yalnız şunlar 2.0 sayılır:
/// <list type="bullet">
/// <item>Şablon içinde 2.0 komutu (<c>xsl:for-each-group</c>, <c>xsl:sequence</c>, <c>xsl:analyze-string</c> …).</item>
/// <item>.NET'in sessizce yok saydığı 2.0 özniteliği (<c>xsl:value-of/@separator</c>, <c>xsl:attribute/@select</c> …).</item>
/// <item>XPath'te XPath 1.0 / XSLT 1.0 dışı ön eksiz fonksiyon (<c>exists()</c>, <c>replace()</c>, <c>if (</c> …),
///   msxsl/exsl dışı ön ekli fonksiyon (<c>fn:</c>, <c>xs:</c>, kullanıcı <c>xsl:function</c>'ı).</item>
/// <item>2.0 sözdizimi: <c>for/some/every $v</c>, <c>instance of</c>, <c>cast(able)/treat as</c>, işlenenden sonra
///   <c>eq ne lt le gt ge is idiv to intersect except</c>, dizi virgülü <c>(a, b)</c>, boş dizi <c>()</c>,
///   parantezli adım <c>a/(b|c)</c>.</item>
/// </list>
/// Saf, durumsuz; ayrıştırılamayan metin → <c>false</c> (motor kendi hatasını verir). DTD yasak.
/// </summary>
public static class Xslt2FeatureDetector
{
    private const string XslNamespace = "http://www.w3.org/1999/XSL/Transform";
    private const string MsxslNamespace = "urn:schemas-microsoft-com:xslt";
    private const string ExslNamespace = "http://exslt.org/common";

    /// <summary>.NET'in ileri-uyumlu kipte çalışırken hata verdiği 2.0 komutları (bildirimler hariç).</summary>
    private static readonly HashSet<string> Xslt2Instructions = new(StringComparer.Ordinal)
    {
        "for-each-group", "sequence", "analyze-string", "matching-substring", "non-matching-substring",
        "next-match", "perform-sort", "namespace", "document", "result-document",
    };

    /// <summary>XPath 1.0 çekirdek + XSLT 1.0 fonksiyonları ve düğüm türü sınamaları.</summary>
    private static readonly HashSet<string> Xpath1Functions = new(StringComparer.Ordinal)
    {
        "last", "position", "count", "id", "local-name", "namespace-uri", "name",
        "string", "concat", "starts-with", "contains", "substring-before", "substring-after", "substring",
        "string-length", "normalize-space", "translate", "boolean", "not", "true", "false", "lang",
        "number", "sum", "floor", "ceiling", "round",
        "document", "key", "format-number", "current", "unparsed-entity-uri", "generate-id",
        "system-property", "element-available", "function-available",
        "node", "text", "comment", "processing-instruction",
    };

    /// <summary>XPath 1.0 ad işleçleri — ardından <c>(</c> gelebilir, fonksiyon değildir.</summary>
    private static readonly HashSet<string> Xpath1Operators = new(StringComparer.Ordinal) { "and", "or", "div", "mod" };

    /// <summary>İşlenenden sonra gelince yalnız XPath 2.0'da geçerli ikili işleçler.</summary>
    private static readonly HashSet<string> Xpath2Operators = new(StringComparer.Ordinal)
    {
        "eq", "ne", "lt", "le", "gt", "ge", "is", "idiv", "to", "intersect", "except",
    };

    /// <summary>AVT taşıyabilen xsl: elemanları (diğerlerinin öznitelikleri ifade değildir).</summary>
    private static readonly HashSet<string> AvtXslElements = new(StringComparer.Ordinal)
    {
        "element", "attribute", "processing-instruction", "sort", "number",
    };

    public static bool UsesXslt2Features(string? xslt)
    {
        if (string.IsNullOrWhiteSpace(xslt))
            return false;

        try
        {
            using var reader = XmlReader.Create(new StringReader(xslt.TrimStart('﻿')), SecureXslt.ReaderSettings());
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && ElementUsesXslt2(reader))
                    return true;
            }
            return false;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static bool ElementUsesXslt2(XmlReader reader)
    {
        var isXsl = reader.NamespaceURI == XslNamespace;
        if (isXsl && Xslt2Instructions.Contains(reader.LocalName))
            return true;
        if (!reader.HasAttributes)
            return false;

        var owner = reader.LocalName;
        try
        {
            while (reader.MoveToNextAttribute())
            {
                if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/")
                    continue;

                var noNamespace = reader.NamespaceURI.Length == 0;
                if (isXsl && noNamespace && IsXslt2OnlyAttribute(owner, reader.LocalName))
                    return true;

                if (isXsl && noNamespace && XsltSafetyRules.XPathAttributes.Contains(reader.LocalName))
                {
                    if (ExpressionUsesXpath2(reader.Value, reader))
                        return true;
                }
                else if ((!isXsl && reader.NamespaceURI != XslNamespace) || (isXsl && noNamespace && AvtXslElements.Contains(owner)))
                {
                    if (AvtUsesXpath2(reader.Value, reader))
                        return true;
                }
            }
            return false;
        }
        finally
        {
            reader.MoveToElement();
        }
    }

    /// <summary>XSLT 1.0'da olmayan, .NET ileri-uyumlu kipte hatasız yok sayılıp yanlış çıktı veren öznitelikler.</summary>
    private static bool IsXslt2OnlyAttribute(string element, string attribute) => (element, attribute) switch
    {
        ("value-of", "separator") => true,
        ("attribute", "separator" or "select") => true,
        ("comment" or "processing-instruction", "select") => true,
        _ => false,
    };

    private static bool AvtUsesXpath2(string value, XmlReader scope)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '{')
                continue;
            if (i + 1 < value.Length && value[i + 1] == '{')
            {
                i++;
                continue;
            }

            var end = XPathLexer.FindEnclosedExpressionEnd(value, i + 1, out _);
            if (ExpressionUsesXpath2(value[(i + 1)..Math.Min(end, value.Length)], scope))
                return true;
            i = end;
        }
        return false;
    }

    private static bool ExpressionUsesXpath2(string expression, XmlReader scope)
    {
        if (expression.Length == 0)
            return false;

        var tokens = XPathTokenizer.Tokenize(expression);
        var parens = new Stack<bool>(); // true: fonksiyon çağrısı parantezi (içindeki virgül argüman ayırıcıdır)
        for (var k = 0; k < tokens.Count; k++)
        {
            var token = tokens[k];
            if (token.Kind == XPathTokenKind.EqName)
                return true;

            if (token.Kind == XPathTokenKind.LParen)
            {
                var previous = k > 0 ? tokens[k - 1] : default;
                var isCall = k > 0 && previous.Kind == XPathTokenKind.Name && !Xpath1Operators.Contains(previous.Text);
                if (!isCall && k + 1 < tokens.Count && tokens[k + 1].Kind == XPathTokenKind.RParen)
                    return true; // () boş dizi
                if (k > 0 && previous.Kind == XPathTokenKind.Other && previous.Text == "/")
                    return true; // a/(b|c) parantezli adım
                parens.Push(isCall);
                continue;
            }
            if (token.Kind == XPathTokenKind.RParen)
            {
                parens.TryPop(out _);
                continue;
            }
            if (token.Kind == XPathTokenKind.Other && token.Text == "," && (parens.Count == 0 || !parens.Peek()))
                return true; // (a, b) dizi oluşturma

            if (token.Kind != XPathTokenKind.Name)
                continue;

            var next = k + 1 < tokens.Count ? tokens[k + 1] : default;
            if (next.Kind == XPathTokenKind.LParen && !Xpath1Operators.Contains(token.Text) && FunctionIsXpath2(token.Text, scope))
                return true;
            if (next.Kind == XPathTokenKind.Var && token.Text is "for" or "some" or "every")
                return true;
            if (next.Kind == XPathTokenKind.Name
                && ((token.Text == "instance" && next.Text == "of")
                    || (token.Text is "cast" or "castable" or "treat" && next.Text == "as")))
                return true;
            if (Xpath2Operators.Contains(token.Text) && k > 0 && EndsOperand(tokens[k - 1]))
                return true;
        }
        return false;
    }

    private static bool FunctionIsXpath2(string name, XmlReader scope)
    {
        var colon = name.IndexOf(':');
        if (colon < 0)
            return !Xpath1Functions.Contains(name);

        var ns = scope.LookupNamespace(name[..colon]);
        return ns is not (MsxslNamespace or ExslNamespace);
    }

    private static bool EndsOperand(XPathToken previous) => previous.Kind switch
    {
        XPathTokenKind.Name or XPathTokenKind.Var or XPathTokenKind.String or XPathTokenKind.Number or XPathTokenKind.RParen => true,
        XPathTokenKind.Other => previous.Text is "]" or ".",
        _ => false,
    };
}
