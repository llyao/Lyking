using Lyking.GameplayAbilities.Definitions;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Lyking.GameplayAbilities.EditModeTests
{
    public sealed class DefinitionConversionTests
    {
        [Test]
        public void 默认火球定义转换为运行时能力()
        {
            GameplayAbilityDefinition definition = ScriptableObject.CreateInstance<GameplayAbilityDefinition>();
            try
            {
                var ability = definition.ToRuntimeAbility();

                Assert.AreEqual("Fireball", ability.Id);
                Assert.AreEqual(20d, ability.Cost);
                Assert.AreEqual(2d, ability.CooldownSeconds);
                Assert.AreEqual(25d, ability.Damage);
                Assert.IsTrue(ability.BlockedTags.HasTag("State.Stunned"));
                Assert.IsNull(ability.DamageEffect);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void 序列化火球资产转换为运行时能力()
        {
            GameplayAbilityDefinition definition = AssetDatabase.LoadAssetAtPath<GameplayAbilityDefinition>(
                "Assets/Definitions/FireballAbility.asset");

            Assert.IsNotNull(definition);
            var ability = definition!.ToRuntimeAbility();

            Assert.AreEqual("Fireball", ability.Id);
            Assert.AreEqual(20d, ability.Cost);
            Assert.AreEqual(2d, ability.CooldownSeconds);
            Assert.AreEqual(25d, ability.Damage);
            Assert.IsTrue(ability.BlockedTags.HasTag("State.Stunned"));
            Assert.IsNotNull(ability.DamageEffect);
            Assert.AreEqual(-25d, ability.DamageEffect!.Modifiers["Health"]);
        }

        [Test]
        public void 序列化火球效果资产转换为运行时规格()
        {
            GameplayEffectDefinition definition = AssetDatabase.LoadAssetAtPath<GameplayEffectDefinition>(
                "Assets/Definitions/FireballDamageEffect.asset");

            Assert.IsNotNull(definition);
            var effect = definition!.ToRuntimeSpec();

            Assert.AreEqual(-25d, effect.Modifiers["Health"]);
        }
    }
}
