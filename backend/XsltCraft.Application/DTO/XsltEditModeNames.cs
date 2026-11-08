using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.DTO;

/// <summary>API'deki <see cref="XsltEditMode"/> adları: <c>"editor"</c> / <c>"easyEdit"</c>.</summary>
public static class XsltEditModeNames
{
    public const string Editor = "editor";
    public const string EasyEdit = "easyEdit";

    public static string ToName(XsltEditMode mode) => mode == XsltEditMode.EasyEdit ? EasyEdit : Editor;

    /// <summary>Beyaz listedeki adı çözer; null ya da bilinmeyen değerde false döner.</summary>
    public static bool TryParse(string? name, out XsltEditMode mode)
    {
        switch (name)
        {
            case Editor:
                mode = XsltEditMode.Editor;
                return true;
            case EasyEdit:
                mode = XsltEditMode.EasyEdit;
                return true;
            default:
                mode = XsltEditMode.Editor;
                return false;
        }
    }
}
