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
        settings.defenseIcon = sprites.Single(sprite => sprite.name == "DEF");
        var arrows = AssetDatabase.LoadAllAssetsAtPath("Assets/ChessCardImages/CountingIcon/Arrow.png").OfType<Sprite>().ToArray();
        settings.arrowUp = arrows.Single(sprite => sprite.name == "Arrow_up");
        settings.arrowDown = arrows.Single(sprite => sprite.name == "Arrow_down");
        foreach (var prefabPath in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/SceneTemplates" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("/Damage calculation.prefab")))
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try { root.GetComponent<DamageResolutionPanel>().ConfigureModifierArrow(); PrefabUtility.SaveAsPrefabAsset(root, prefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
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
        foreach (bool defense in new[] { false, true })
            foreach (int sign in new[] { -1, 1 })
            {
                var step = new DamageCalculationStep { side = defense ? DamageStepSide.Defense : DamageStepSide.Attack, contribution = sign };
                check(DamageModifierFeedback.PointsUp(step) == (defense ? sign < 0 : sign > 0), "Arrow direction for defense=" + defense + " contribution=" + sign);
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
            Check(settings.arrowUp != null && settings.arrowDown != null && settings.defenseIcon != null, "Arrows and defense shield are assigned");
            var attackerObject = new GameObject("Modifier test attacker");
            var targetObject = new GameObject("Modifier test target");
            var auraObject = new GameObject("Modifier test aura owner");
            var panel = DamageResolutionPanel.Create(null);
            try
            {
                var attacker = attackerObject.AddComponent<Pawn>();
                var target = targetObject.AddComponent<Pawn>();
                var aura = auraObject.AddComponent<Pawn>();
                var sequence = new DamageCalculationSequence { attacker = attacker, target = target };
                foreach (bool defense in new[] { false, true })
                    foreach (int sign in new[] { -1, 1 })
                    {
                        var step = new DamageCalculationStep { side = defense ? DamageStepSide.Defense : DamageStepSide.Attack, contribution = sign, iconPiece = aura };
                        panel.ShowModifier(step, sequence, settings, 0);
                        Check(panel.AttachedPiece == (defense ? target.transform : attacker.transform), "Modifier anchors to combatant, not aura owner: defense=" + defense + " sign=" + sign);
                        var arrow = panel.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i => i.name == "Modifier arrow");
                        bool up = defense ? sign < 0 : sign > 0;
                        Check(settings.Sound(step) == (up ? settings.increaseSound : settings.decreaseSound), "Sound follows arrow rather than damage sign: defense=" + defense + " sign=" + sign);
                        Check(arrow.sprite == (up ? settings.arrowUp : settings.arrowDown) && Mathf.Abs(Mathf.DeltaAngle(arrow.transform.localEulerAngles.z, up ? 0 : 180)) < .1f, "Arrow sprite and rotation: defense=" + defense + " sign=" + sign);
                        if (defense) Check(settings.Symbol(step, sequence) == settings.defenseIcon, "Defense modifiers use shield");
                        panel.ShowModifier(step, sequence, settings, 0, aura.transform);
                        Check(panel.AttachedPiece == aura.transform, "Field override keeps modifier on field source");
                    }
                panel.ShowSource(new DamageCalculationStep { iconPiece = target }, settings.increaseIcon, 0, target, aura.transform);
                Check(panel.AttachedPiece == aura.transform, "Field activation stays on field instead of damage target");
                panel.ShowSource(new DamageCalculationStep(), settings.increaseIcon, 0);
                Check(!panel.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(i => i.name == "Modifier arrow").gameObject.activeSelf, "Pooled arrow is hidden for ordinary steps");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panel.gameObject);
                UnityEngine.Object.DestroyImmediate(attackerObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(auraObject);
            }
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
