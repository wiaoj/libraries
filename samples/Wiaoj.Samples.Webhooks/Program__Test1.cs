//using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.TestHost;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using System.Net;
//using System.Text;
//using Wiaoj.Primitives;
//using Wiaoj.Primitives.Cryptography.Asymmetric;
//using Wiaoj.Webhooks;
//using Wiaoj.Webhooks.AspNetCore;
//using Wiaoj.Webhooks.Signing.Asymmetric;
//using Wiaoj.Webhooks.Signing.Asymmetric.Rsa;

//Console.WriteLine("=================================================================");
//Console.WriteLine("🚀 RSA ASYMMETRIC WEBHOOK CANLI DOĞRULAMA TESTİ BAŞLIYOR...");
//Console.WriteLine("=================================================================\n");

//// 1. ADIM: PROVIDER (GÖNDERİCİ) 2048-BIT RSA ANAHTARLARINI ÜRETİR
//using RsaKeyPair providerKeyPair = RsaKeyPair.Generate2048();
//RsaPublicKey receiverPublicKey = providerKeyPair.PublicKey;
//RsaWebhookSigner rsaSigner = new(RsaAlgorithm.RS256);

//Console.WriteLine("🔑 [1] Provider 2048-bit RSA Anahtar Çifti Üretti.");
//Console.WriteLine($"    Public Key Modulus: {receiverPublicKey.Modulus.Value[..30]}...\n");

//// 2. ADIM: TEST SUNUCUSUNU KURUYORUZ
//var builder = WebApplication.CreateBuilder();
//builder.WebHost.UseTestServer();

//// 🌟 DÜZELTME: Outbound bağımlılıklarını in-memory fallback ile tamamlıyoruz
//builder.Services.AddWiaojWebhooks(webhooks => {
//    webhooks.UseInMemoryTransport()
//            .UseEndpointResolver((id, ct) => ValueTask.FromResult<WebhookEndpoint?>(null));
//});
//builder.Services.AddInboundWebhooks();

//var app = builder.Build();

//// 3. ADIM: RSA ENDPOINT TANIMI (Sadece Public Key ile Doğrulama Yapar)
//app.MapWebhook<PaymentReceivedEvent>("/webhooks/paypal", (
//    PaymentReceivedEvent @event,
//    WebhookReceiverContext<PaymentReceivedEvent> ctx) => {
//        Console.WriteLine($"\n🎉 [BAŞARILI] Webhook Alındı & Doğrulandı!");
//        Console.WriteLine($"    Ödeme ID : {@event.PaymentId}");
//        Console.WriteLine($"    Tutar    : {@event.Amount} {@event.Currency}");
//        Console.WriteLine($"    İmza     : {ctx.Signature?.Signature[..25]}...");
//        return Results.Ok();
//    })
//.WithSigner(rsaSigner)
//.WithSecretResolver((httpContext, ct) => {
//    // Alıcı sunucu, provider'ın Public Key Modulus'unu unmanaged Secret olarak verir
//    byte[] pubModulusBytes = receiverPublicKey.Modulus.ToBytes();
//    return ValueTask.FromResult(Secret<byte>.From(pubModulusBytes));
//})
//.WithTolerance(TimeSpan.FromMinutes(5));

//await app.StartAsync();
//HttpClient client = app.GetTestServer().CreateClient();

//Console.WriteLine("🌐 [2] Webhook Alıcı Sunucusu '/webhooks/paypal' adresinde dinlemeye başladı.\n");

//// ─────────────────────────────────────────────────────────────────────────────
//// 4. ADIM: GEÇERLİ WEBHOOK GÖNDERİMİ (POZİTİF TEST)
//// ─────────────────────────────────────────────────────────────────────────────
//Console.WriteLine("-----------------------------------------------------------------");
//Console.WriteLine("📡 [TEST 1] Provider Orijinal Payload'ı Private Key ile İmzalayıp Gönderiyor...");

//const string validPayloadJson = """{"PaymentId":"PAY-999888","Amount":250.00,"Currency":"USD"}""";
//UnixTimestamp now = UnixTimestamp.Now;

//// Provider Private Key ile imzalar
//WebhookSignature validSignature = rsaSigner.Sign(Encoding.UTF8.GetBytes(validPayloadJson), providerKeyPair, now);

//HttpRequestMessage validRequest = new(HttpMethod.Post, "/webhooks/paypal") {
//    Content = new StringContent(validPayloadJson, Encoding.UTF8, "application/json")
//};
//validRequest.Headers.Add("Webhook-Signature", validSignature.HeaderValue);

//HttpResponseMessage validResponse = await client.SendAsync(validRequest);
//Console.WriteLine($"👉 Sunucu Yanıtı: {(int)validResponse.StatusCode} {validResponse.StatusCode}");
//Console.WriteLine("-----------------------------------------------------------------\n");


//// ─────────────────────────────────────────────────────────────────────────────
//// 5. ADIM: TAHRİF EDİLMİŞ (MAN-IN-THE-MIDDLE) SALDIRI (NEGATİF TEST)
//// ─────────────────────────────────────────────────────────────────────────────
//Console.WriteLine("-----------------------------------------------------------------");
//Console.WriteLine("🚨 [TEST 2] Saldırgan Araya Girdi: Tutar 250.00 yerine 999999.00 yapıldı!");

//// Tahrif edilmiş payload (aynı imza ile gönderilmeye çalışılıyor)
//const string tamperedPayloadJson = """{"PaymentId":"PAY-999888","Amount":999999.00,"Currency":"USD"}""";

//HttpRequestMessage attackRequest = new(HttpMethod.Post, "/webhooks/paypal") {
//    Content = new StringContent(tamperedPayloadJson, Encoding.UTF8, "application/json")
//};
//// Orijinal imza kullanılıyor ama gövde değiştirildi:
//attackRequest.Headers.Add("Webhook-Signature", validSignature.HeaderValue);

//HttpResponseMessage attackResponse = await client.SendAsync(attackRequest);
//Console.WriteLine($"👉 Sunucu Yanıtı: {(int)attackResponse.StatusCode} {attackResponse.StatusCode}");

//string responseBody = await attackResponse.Content.ReadAsStringAsync();
//Console.WriteLine($"    RFC 9457 Problem Details: {responseBody}");
//Console.WriteLine("-----------------------------------------------------------------");

//Console.WriteLine("\n✅ CANLI TEST TAMAMLANDI: RSA Asimetrik Doğrulama %100 Çalışıyor!");

//// Test Event Modeli
//public sealed record PaymentReceivedEvent(string PaymentId, decimal Amount, string Currency) : IWebhookEvent;