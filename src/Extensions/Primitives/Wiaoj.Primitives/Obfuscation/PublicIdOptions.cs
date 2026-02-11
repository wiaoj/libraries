namespace Wiaoj.Primitives.Obfuscation;

public record PublicIdOptions {
    /// <summary>
    /// The secret seed used to shuffle bits. 
    /// Change this and all your PublicIds will change!
    /// Keep it secret, keep it safe.
    /// </summary>
    public string Seed { get; set; } = "WiaojDefaultSeed_ChangeMe"; // Güvenli varsayılan yok, değiştirmeye zorlamalıyız ama kod çalışsın diye koyduk.
}