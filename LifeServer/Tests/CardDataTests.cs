using Newtonsoft.Json;
using Server;
using Server.CardProperties;

namespace Tests;

/// <summary>
/// Loads every card JSON in LifeServer/Data/Cards through the production deserialization
/// path. Catches malformed JSON, bad enum values, and (via strict mode) misspelled
/// property names - the historical "statModfiers"/"optoinal" class of bug.
/// </summary>
[TestFixture]
public class CardDataTests {

    private static string CardsDir() {
        // Walk up from the test bin directory to find LifeServer/Data/Cards
        string? dir = AppContext.BaseDirectory;
        while (dir != null) {
            string candidate = Path.Combine(dir, "Data", "Cards");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("Could not locate Data/Cards above " + AppContext.BaseDirectory);
    }

    private static IEnumerable<TestCaseData> AllCardFiles() {
        foreach (string path in Directory.GetFiles(CardsDir(), "*.json").OrderBy(p => p)) {
            yield return new TestCaseData(path).SetName("Card_" + Path.GetFileNameWithoutExtension(path));
        }
    }

    [Test]
    public void CardsDirectory_Has281Cards() {
        Assert.That(Directory.GetFiles(CardsDir(), "*.json"), Has.Length.EqualTo(281));
    }

    [TestCaseSource(nameof(AllCardFiles))]
    public void Card_LoadsThroughProductionPath(string path) {
        int cardId = int.Parse(Path.GetFileName(path).Split('_')[0]);
        Card card = Card.GetCard(uid: 9000 + cardId, cardId);
        Assert.That(card, Is.Not.Null);
        Assert.That(card.name, Is.Not.Null.And.Not.Empty);
        Assert.That(card.id, Is.EqualTo(cardId), $"id inside {Path.GetFileName(path)} does not match its filename");
    }

    [TestCaseSource(nameof(AllCardFiles))]
    public void Card_HasNoUnknownProperties(string path) {
        // MissingMemberHandling.Error makes any JSON property that has no matching C# member
        // fail loudly instead of being silently ignored.
        JsonSerializerSettings strict = new() {
            MissingMemberHandling = MissingMemberHandling.Error,
        };
        strict.Converters.Add(new EffectTypeConverter());
        string json = File.ReadAllText(path);
        Assert.DoesNotThrow(() => JsonConvert.DeserializeObject<CardDto>(json, strict),
            $"{Path.GetFileName(path)} contains a property no C# model recognizes (typo?)");
    }
}
