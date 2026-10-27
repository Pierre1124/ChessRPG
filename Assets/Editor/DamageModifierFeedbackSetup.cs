using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DamageModifierFeedbackSetup
{
    [MenuItem("Tools/Chess/Presentation/Setup Shared Damage Modifiers")]
    public static void Setup()
    {
        const string path = "Assets/Resources/DamageModifierFeedback.asset";
        var settings = AssetDatabase.LoadAssetAtPath<DamageModifierFeedback>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<DamageModifierFeedback>();
            AssetDatabase.CreateAsset(settings, path);
        }
        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/ChessCardImages/CountingIcon/CountingIcon.png").OfType<Sprite>().ToArray();
        settings.increaseIcon = sprites.Single(sprite => sprite.name == "ATK");
        settings.decreaseIcon = sprites.Single(sprite => sprite.name == "AntiATK");
        settings.increaseSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Prefabs/Sound/振奮士氣.mp3");
        settings.decreaseSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Prefabs/Sound/能力下降.mp3");
        EditorUtility.SetDirty(settings);
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:CardAsset", new[] { "Assets/Resources/Cards" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<CardAsset>(AssetDatabase.GUIDToAssetPath(guid));
            string before = JsonUtility.ToJson(asset.definition);
            CardPresentationPolicy.RemoveIndividualEffects(asset.definition);
            if (before == JsonUtility.ToJson(asset.definition)) continue;
            EditorUtility.SetDirty(asset); changed++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Removed individual effects/audio from " + changed + " non-field cards; shared damage modifiers configured.");
    }

    public static void ManagedChecks(Action<bool, string> check)
    {
        foreach (var phase in new[] { DamageModifierPhase.Increase, DamageModifierPhase.Decrease })
        {
            var step = new DamageCalculationStep { modifierPhase = phase, hasContribution = true, contribution = 2 };
            check(DamageModifierFeedback.Direction(step, false) == 1, "Positive damage modifier plays up in " + phase);
            step.contribution = -2;
            check(DamageModifierFeedback.Direction(step, false) == -1, "Negative damage modifier plays down in " + phase);
            step.contribution = 0;
            check(DamageModifierFeedback.Direction(step, false) == 0, "Zero modifier stays silent in " + phase);
            step.contribution = 2;
            check(DamageModifierFeedback.Direction(step, true) == 0, "Healing does not play damage modifier sound");
        }
        check(DamageModifierFeedback.Direction(new DamageCalculationStep { modifierPhase = DamageModifierPhase.Activation, hasContribution = true, contribution = 3 }, false) == 0,
            "Base damage activation is not a damage increase");
    }

    [MenuItem("Tools/Chess/Presentation/Verify Shared Damage Modifiers")]
    public static void Verify()
    {
        var results = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); results.Add("PASS: " + label); }
        try
        {
            ManagedChecks(Check);
            var settings = DamageModifierFeedback.Settings;
            Check(settings != null && settings.increaseIcon != null && settings.decreaseIcon != null, "Both shared modifier icons are included");
            Check(settings.increaseSound != null && settings.decreaseSound != null && settings.increaseSound != settings.decreaseSound,
                "Damage up and down use distinct audio clips");
            foreach (var asset in Resources.LoadAll<CardAsset>("Cards"))
            {
                var card = asset.definition;
                if (card.cardType == CardType.Field)
                {
                    string before = JsonUtility.ToJson(card);
                    CardPresentationPolicy.RemoveIndividualEffects(card);
                    Check(before == JsonUtility.ToJson(card), card.id + " field presentation is preserved");
                    continue;
                }
                Check(card.skillEffectPrefab == null && card.skillSound == null &&
                    card.animations.All(a => a == null || (a.effectPrefab == null && a.sound == null)) &&
                    card.statusesToApply.All(s => s == null || (s.triggerEffectPrefab == null &&
                        (card.cardType == CardType.JobChange || s.idleEffectPrefab == null) && s.triggerSound == null)),
                    card.id + " has no individual trigger effects or audio");
                if (card.cardType == CardType.JobChange)
                {
                    var copy = asset.CreateDefinition();
                    Check(card.statusesToApply.Select(s => s?.idleEffectPrefab).SequenceEqual(copy.statusesToApply.Select(s => s?.idleEffectPrefab)),
                        card.id + " preserves idle effects when loaded");
                }
            }
        }
        catch (Exception error) { results.Add("FAIL: " + error); throw; }
        finally { Directory.CreateDirectory("output"); File.WriteAllLines("output/shared-damage-modifiers.txt", results); }
        Debug.Log("Shared damage modifiers: " + results.Count + " passed");
    }
}
