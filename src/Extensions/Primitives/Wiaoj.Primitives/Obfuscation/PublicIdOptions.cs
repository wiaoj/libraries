#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Primitives;
#pragma warning restore IDE0130 // Namespace does not match folder structure
public record PublicIdOptions {
    /// <summary>
    /// The secret seed used to shuffle bits. 
    /// Change this and all your PublicIds will change!
    /// Keep it secret, keep it safe.
    /// </summary>
    public string Seed { get; set; } = "WiaojDefaultSeed_ChangeMe";
}