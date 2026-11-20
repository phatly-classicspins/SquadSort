using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Application;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>
    /// D-059: <c>Tools/Firebase/remote-config.template.json</c> is what goes into the Firebase console. Every config key
    /// is in it exactly once (Firebase keys allow no dot, so '.' becomes '_'), with the bundled default as its value.
    /// </summary>
    public class RemoteConfigTemplateTests
    {
        static Dictionary<string, (string Value, string Type)> Template()
        {
            var root = JObject.Parse(File.ReadAllText(Path.Combine(RepoLayout.RepoRoot, "Tools/Firebase/remote-config.template.json")));
            var all = new Dictionary<string, (string, string)>();
            var groups = ((JObject)root["parameterGroups"]).Properties().Select(g => (JObject)g.Value["parameters"]);
            // Keep the template in the shape of the console's own export ("Download current config file"):
            // a file with an empty "conditions" array and root-level "parameters" was refused by "Publish from a file".
            Assert.That(root["version"], Is.Not.Null, "template needs the console export's version block");
            Assert.That(root["conditions"], Is.Null, "the console export has no empty conditions array");
            Assert.That(root["parameters"], Is.Null, "every parameter lives in a parameter group");
            foreach (var ps in groups)
                foreach (var p in ps.Properties())
                {
                    Assert.That(all.ContainsKey(p.Name), Is.False, $"{p.Name} is in the template twice");
                    all[p.Name] = ((string)p.Value["defaultValue"]["value"], (string)p.Value["valueType"]);
                }
            return all;
        }

        static string Firebase(string key) => key.Replace('.', '_');

        static string Text(object v) => v is double d ? d.ToString(CultureInfo.InvariantCulture) : v is bool b ? (b ? "true" : "false")
            : System.Convert.ToString(v, CultureInfo.InvariantCulture);

        static string Type(object v) => v is string ? "STRING" : v is bool ? "BOOLEAN" : "NUMBER";

        [Test]
        public void Every_config_key_is_in_the_template_with_its_bundled_default()
        {
            var template = Template();
            var defaults = EconomyConfig.Defaults().Values;
            Assert.That(template.Keys, Is.EquivalentTo(defaults.Keys.Select(Firebase)));
            foreach (var kv in defaults)
            {
                var (value, type) = template[Firebase(kv.Key)];
                Assert.That(value, Is.EqualTo(Text(kv.Value)), kv.Key);
                Assert.That(type, Is.EqualTo(Type(kv.Value)), kv.Key);
            }
        }

        [Test]
        public void Firebase_keys_are_legal_and_do_not_collide()
        {
            var keys = EconomyConfig.Defaults().Values.Keys.Select(Firebase).ToList();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count));
            Assert.That(keys.All(k => System.Text.RegularExpressions.Regex.IsMatch(k, "^[A-Za-z_][A-Za-z0-9_]{0,255}$")), Is.True);
        }
    }
}
