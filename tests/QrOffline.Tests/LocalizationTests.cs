using System.Collections;
using System.Globalization;
using System.Resources;
using QrOffline.Localization;
using Xunit;

namespace QrOffline.Tests;

public class LocalizationTests
{
    private static readonly ResourceManager Manager =
        new("QrOffline.Localization.Strings", typeof(Strings).Assembly);

    [Fact]
    public void NeutralResourcesAreEnglish()
    {
        Assert.Equal("Offline QR Generator", Manager.GetString("AppTitle", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SimplifiedChineseResourcesAreAvailable()
    {
        Assert.Equal("离线二维码生成器", Manager.GetString("AppTitle", CultureInfo.GetCultureInfo("zh-Hans")));
    }

    [Fact]
    public void EveryNeutralKeyHasSimplifiedChineseValue()
    {
        var neutral = Manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true);
        Assert.NotNull(neutral);

        var zh = CultureInfo.GetCultureInfo("zh-Hans");
        foreach (DictionaryEntry entry in neutral)
        {
            var key = (string)entry.Key;
            var value = Manager.GetString(key, zh);
            Assert.False(string.IsNullOrEmpty(value), $"Missing zh-Hans value for '{key}'.");
        }
    }

    [Fact]
    public void StronglyTypedAccessorsReturnValues()
    {
        Assert.False(string.IsNullOrEmpty(Strings.GenerateButton));
        Assert.False(string.IsNullOrEmpty(Strings.StatusReady));
        Assert.Contains("{0}", Strings.CharCountFormat);
    }
}
