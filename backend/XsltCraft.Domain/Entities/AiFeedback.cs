namespace XsltCraft.Domain.Entities;

/// <summary>
/// Kullanıcının bir AI asistan yanıtı için verdiği geri bildirim.
/// Pozitif kayıtlar sonraki sorularda few-shot örnek (exemplar) olarak prompt'a enjekte edilir;
/// admin tarafından <see cref="IsGlobal"/> ile tüm kullanıcıların havuzuna terfi ettirilebilir.
/// </summary>
public class AiFeedback
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AiFeedbackRating Rating { get; set; }

    /// <summary>Geri bildirimin ait olduğu kullanıcı sorusu (servis 2.000 karaktere kırpar).</summary>
    public string UserMessage { get; set; } = "";

    /// <summary>Değerlendirilen asistan yanıtı (servis 8.000 karaktere kırpar).</summary>
    public string AssistantAnswer { get; set; } = "";

    /// <summary>Yanıttaki değişiklik editöre "Uygula" ile uygulandıysa true.</summary>
    public bool Applied { get; set; }

    /// <summary>Admin bu örneği global havuza terfi ettiyse true.</summary>
    public bool IsGlobal { get; set; }

    public DateTime? PromotedAt { get; set; }
    public Guid? PromotedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}

public enum AiFeedbackRating
{
    Positive,
    Negative
}
