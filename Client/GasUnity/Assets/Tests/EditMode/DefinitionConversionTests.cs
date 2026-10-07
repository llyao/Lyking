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

        [Test]
        public void 序列化持续时间和授予标签转换为活动效果规格()
        {
            GameplayEffectDefinition definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            try
            {
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("durationSeconds").floatValue = 5;
                SerializedProperty tags = serialized.FindProperty("grantedTags");
                tags.arraySize = 1;
                tags.GetArrayElementAtIndex(0).stringValue = "State.Burning";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var effect = definition.ToRuntimeSpec();

                Assert.AreEqual(5d, effect.DurationSeconds, 0.0001d);
                Assert.IsFalse(effect.IsInfinite);
                Assert.IsTrue(effect.GrantedTags.HasTag("State.Burning"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void 序列化无限标记转换为无限效果规格()
        {
            GameplayEffectDefinition definition = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            try
            {
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("isInfinite").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var effect = definition.ToRuntimeSpec();

                Assert.IsTrue(effect.IsInfinite);
                Assert.AreEqual(0d, effect.DurationSeconds);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }
    }
}
