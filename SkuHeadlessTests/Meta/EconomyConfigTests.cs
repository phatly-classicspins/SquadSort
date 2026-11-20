using System.Collections.Generic;
using System.Threading;
using ClassicSpins.PrototypeFramework.Application;
using Game.Application;
using Game.Gen;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>CR-012 G (D-043): every balance number is a <c>GameConfigKeys</c> entry, defaulting to <see cref="EconomyTuning"/>.</summary>
    public class EconomyConfigTests
    {
        [Test]
        public void With_no_remote_the_tuning_is_the_code_defaults()
        {
            var read = EconomyConfig.Read(InMemoryConfig.Create(EconomyConfig.Defaults().Values));
            var code = new EconomyTuning();
            foreach (var f in typeof(EconomyTuning).GetFields())
                Assert.That(f.GetValue(read), Is.EqualTo(f.GetValue(code)), f.Name);
        }

        [Test]
        public void Every_generated_key_has_a_default()
        {
            var defaults = EconomyConfig.Defaults().Values;
            foreach (var f in typeof(GameConfigKeys).GetFields())
            {
                var value = (string)f.FieldType.GetProperty("Value")!.GetValue(f.GetValue(null))!;
                Assert.That(defaults.ContainsKey(value), value);
            }
        }

        [Test]
        public void A_remote_value_overrides_and_garbage_keeps_the_default()
        {
            var source = new FakeRemoteConfigSource(new RemoteConfigPayload("e1", 1, new Dictionary<string, string>
            {
                [GameConfigKeys.WinReward.Value] = "35",
                [GameConfigKeys.LivesMax.Value] = "lots",
            }));
            var config = InMemoryConfig.Create(EconomyConfig.Defaults().Values, source);
            config.RefreshRemoteAsync(CancellationToken.None).GetAwaiter().GetResult();
            var t = EconomyConfig.Read(config);
            Assert.That(t.WinReward, Is.EqualTo(35));
            Assert.That(t.LivesMax, Is.EqualTo(5));
        }

        [Test]
        public void Each_key_reaches_the_tuning_field_of_the_same_name()
        {
            // A distinct remote value per key: a swapped key or a field Read forgot both show up as a mismatch, which
            // comparing against the defaults cannot see (Hand / Remove unlock gifts, amounts … share default values).
            var keys = typeof(GameConfigKeys).GetFields();
            var payload = new Dictionary<string, string>();
            var expected = new Dictionary<string, object>();
            for (int i = 0; i < keys.Length; i++)
            {
                var key = (string)keys[i].FieldType.GetProperty("Value")!.GetValue(keys[i].GetValue(null))!;
                var field = typeof(EconomyTuning).GetField(keys[i].Name);
                Assert.That(field, Is.Not.Null, $"{keys[i].Name} has no EconomyTuning field of the same name");
                object value = field!.FieldType == typeof(string) ? $"https://t.example/{i}"
                    : field.FieldType == typeof(double) ? (object)(1000.0 + i)
                    : field.FieldType == typeof(bool) ? (object)!(bool)field.GetValue(new EconomyTuning())!   // the opposite of its default
                    : 1000 + i;
                payload[key] = System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
                expected[field.Name] = value;
            }
            var config = InMemoryConfig.Create(EconomyConfig.Defaults().Values,
                new FakeRemoteConfigSource(new RemoteConfigPayload("map", 1, payload)));
            config.RefreshRemoteAsync(CancellationToken.None).GetAwaiter().GetResult();
            var t = EconomyConfig.Read(config);
            foreach (var f in typeof(EconomyTuning).GetFields())
            {
                if (f.Name == nameof(EconomyTuning.LevelCount)) continue;   // code, not config (shipped level files)
                Assert.That(expected.ContainsKey(f.Name), $"{f.Name} is a tunable with no GameConfigKeys entry");
                Assert.That(f.GetValue(t), Is.EqualTo(expected[f.Name]), f.Name);
            }
        }

        [Test]
        public void A_remote_value_the_game_cannot_run_on_is_pulled_to_the_nearest_safe_one()
        {
            var config = InMemoryConfig.Create(EconomyConfig.Defaults().Values, new FakeRemoteConfigSource(
                new RemoteConfigPayload("bad", 1, new Dictionary<string, string>
                {
                    [GameConfigKeys.UndoPrice.Value] = "0",
                    [GameConfigKeys.RevivePrice.Value] = "-50",
                    [GameConfigKeys.LivesMax.Value] = "0",
                    [GameConfigKeys.LivesRefillMinutes.Value] = "-1",
                    [GameConfigKeys.StartCoins.Value] = "-10",
                    [GameConfigKeys.InterstitialEveryNWins.Value] = "0",
                })));
            config.RefreshRemoteAsync(CancellationToken.None).GetAwaiter().GetResult();
            var t = EconomyConfig.Read(config);
            Assert.That(t.UndoPrice, Is.EqualTo(1), "a booster is never free");
            Assert.That(t.RevivePrice, Is.EqualTo(1), "a paid revive is never free");
            Assert.That(t.LivesMax, Is.EqualTo(1));
            Assert.That(t.LivesRefillMinutes, Is.EqualTo(1));
            Assert.That(t.StartCoins, Is.EqualTo(0));
            Assert.That(t.InterstitialEveryNWins, Is.EqualTo(1));
        }
    }
}
