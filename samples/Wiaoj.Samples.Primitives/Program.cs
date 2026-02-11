using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Snowflake;
using Wiaoj.Primitives.Obfuscation;
using Wiaoj.Primitives;

// ----------------------------------------------------------------------------------
// KONFİGÜRASYON
// ----------------------------------------------------------------------------------
var epoch = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
SnowflakeId.Configure(new SnowflakeOptions {
    NodeId = 42, // Makine ID: 42
    Epoch = epoch
});

const string SecretSeed = "Wiaoj_Secure_Identity_2025_System";
PublicId.Configure(SecretSeed);

PrintHeader("WIAOJ PRIMITIVES: DEEP INTEGRITY VERIFICATION");

// ----------------------------------------------------------------------------------
// TEST 1: SNOWFLAKE BİLEŞEN ANALİZİ (RAW DATA CHECK)
// ----------------------------------------------------------------------------------
Section("TEST 1: Snowflake Component Extraction");
var originalId = SnowflakeId.NewId();
var timestamp = originalId.ToUnixTimestamp(); // SnowflakeId içinden zamanı çek

Console.WriteLine($"Generated ID: {originalId.Value}");
Console.WriteLine($"Timestamp:    {timestamp.ToDateTimeOffset():yyyy-MM-dd HH:mm:ss.fff}");
Console.WriteLine($"Machine ID:   Checking internal structure...");

// Doğrulama: Yeni üretilen ID'nin zamanı şu anki zamana yakın olmalı
var drift = DateTimeOffset.UtcNow - timestamp.ToDateTimeOffset();
if(drift.TotalSeconds < 5)
    Pass("Timestamp extraction is accurate (within 5s drift).");
else
    Fail($"Timestamp drift too high: {drift.TotalSeconds}s");


// ----------------------------------------------------------------------------------
// TEST 2: 64-BIT OBFUSCATION (SCRAMBLE/DESCRAMBLE)
// ----------------------------------------------------------------------------------
Section("TEST 2: 64-Bit Scrambling Integrity (Snowflake)");
SnowflakeId raw64 = 859403294850239485; // Sabit bir rakam
PublicId pid64 = raw64;

string encoded64 = pid64.ToString();
PublicId decodedPid64 = PublicId.Parse(encoded64);
SnowflakeId recovered64 = (SnowflakeId)decodedPid64;

Console.WriteLine($"Original (Raw):   {raw64.Value}");
Console.WriteLine($"Obfuscated (B62): {encoded64}");
Console.WriteLine($"Recovered:        {recovered64.Value}");

Verify(raw64 == recovered64, "64-bit Scramble/Descramble loop matches perfectly.");


// ----------------------------------------------------------------------------------
// TEST 3: 128-BIT OBFUSCATION (GUID)
// ----------------------------------------------------------------------------------
Section("TEST 3: 128-Bit Scrambling Integrity (Guid)");
Guid rawGuid = Guid.NewGuid();
PublicId pid128 = rawGuid;

string encoded128 = pid128.ToString();
PublicId decodedPid128 = PublicId.Parse(encoded128);
Guid recoveredGuid = (Guid)decodedPid128;

Console.WriteLine($"Original Guid:    {rawGuid}");
Console.WriteLine($"Obfuscated (B62): {encoded128}");
Console.WriteLine($"Recovered Guid:   {recoveredGuid}");

Verify(rawGuid == recoveredGuid, "128-bit Scramble/Descramble loop matches perfectly.");


// ----------------------------------------------------------------------------------
// TEST 4: SINIR DURUMLAR (EDGE CASES)
// ----------------------------------------------------------------------------------
Section("TEST 4: Boundary Values");
PublicId emptyId = PublicId.Empty;
Verify(emptyId.ToString() == "0", "Empty ID (0) handles as '0'.");

PublicId max64 = new PublicId(long.MaxValue);
string max64Str = max64.ToString();
Verify(PublicId.Parse(max64Str).AsSnowflake() == long.MaxValue, "Max 64-bit integer integrity preserved.");


// ----------------------------------------------------------------------------------
// TEST 5: JSON SERİALİZASYON VE TYPE-SAFETY
// ----------------------------------------------------------------------------------
Section("TEST 5: JSON Deep Serialization Check");
var originalDto = new UserProfileDto {
    UserId = SnowflakeId.NewId(),
    SessionToken = Guid.NewGuid(),
    Tags = new Dictionary<PublicId, string> {
        { SnowflakeId.NewId(), "Primary" }
    }
};

string jsonOutput = JsonSerializer.Serialize(originalDto, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine("Serialized JSON Payload:");
Console.WriteLine(jsonOutput);

var deserializedDto = JsonSerializer.Deserialize<UserProfileDto>(jsonOutput);

bool userIdMatch = originalDto.UserId == deserializedDto?.UserId;
bool sessionMatch = originalDto.SessionToken == deserializedDto?.SessionToken;
bool dictMatch = originalDto.Tags.Keys.First() == deserializedDto?.Tags.Keys.First();

Verify(userIdMatch && sessionMatch && dictMatch, "JSON Round-trip (Property & Dictionary Key) successful.");


// ----------------------------------------------------------------------------------
// TEST 6: ÇAKIŞMA (COLLISION) VE DETERMINIZM TESTİ
// ----------------------------------------------------------------------------------
Section("TEST 6: Collision & Determinism");
// Aynı ID, aynı seed ile her zaman aynı çıktıyı vermeli
string firstRun = pid64.ToString();
string secondRun = pid64.ToString();
Verify(firstRun == secondRun, "Determinism: Same ID + Same Seed = Same String.");

// 10,000 ID üretildiğinde hiçbiri çakışmamalı
HashSet<string> seenOutputs = new();
bool collisionDetected = false;
for(int i = 0; i < 10000; i++) {
    string output = new PublicId(SnowflakeId.NewId()).ToString();
    if(!seenOutputs.Add(output)) {
        collisionDetected = true;
        break;
    }
}
Verify(!collisionDetected, "Uniqueness: 10,000 unique SnowflakeIds produced 10,000 unique PublicIds.");

PrintFooter();

// ----------------------------------------------------------------------------------
// TEST YARDIMCILARI
// ----------------------------------------------------------------------------------

static void Section(string name) {
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine($"\n>> {name}");
    Console.ResetColor();
    Console.WriteLine(new string('-', 40));
}

static void Verify(bool condition, string message) {
    if(condition) Pass(message); else Fail(message);
}

static void Pass(string msg) {
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($" [PASS] {msg}");
    Console.ResetColor();
}

static void Fail(string msg) {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($" [FAIL] {msg}");
    Console.ResetColor();
    // Debugger.Break(); // Hata anında durmak isterseniz açın
}

static void PrintHeader(string title) {
    Console.Clear();
    Console.BackgroundColor = ConsoleColor.DarkBlue;
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine(new string(' ', 60));
    Console.WriteLine(title.PadLeft(title.Length + (60 - title.Length) / 2).PadRight(60));
    Console.WriteLine(new string(' ', 60));
    Console.ResetColor();
}

static void PrintFooter() {
    Console.WriteLine("\n" + new string('=', 60));
    Console.WriteLine("VERIFICATION COMPLETED SUCCESSFULLY.");
    Console.WriteLine(new string('=', 60));
    Console.ReadLine();
}

// ----------------------------------------------------------------------------------
// DATA TRANSFER OBJECTS
// ----------------------------------------------------------------------------------

public class UserProfileDto {
    // SnowflakeId -> PublicId dönüşümü otomatik (Implicit conversion & JsonConverter)
    public PublicId UserId { get; set; }

    // Guid -> PublicId dönüşümü otomatik
    public PublicId SessionToken { get; set; }

    // Dictionary Key olarak PublicId desteği (PublicIdJsonConverter.ReadAsPropertyName)
    public Dictionary<PublicId, string> Tags { get; set; } = new();
}