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
        check(!DamageModifierFeedback.IsTargetPresentation(new DamageCalculationStep { side = DamageStepSide.Attack }, new DamageCalculationSequence()), "Outgoing modifier belongs to source");
        check(DamageModifierFeedback.IsTargetPresentation(new DamageCalculationStep { side = DamageStepSide.Defense }, new DamageCalculationSequence()), "Defense belongs to target");
        check(DamageModifierFeedback.IsTargetPresentation(new DamageCalculationStep { modifierPhase = DamageModifierPhase.Increase }, new DamageCalculationSequence { presentationOnly = true }), "Black Market stat gain belongs to target");
        var idleStatus = new StatusRuntime(new StatusDefinition { maxCharges = 1, initialCharges = 1, durationTurns = 2 }, null, null, false);
        check(BattleFeedback.ShouldShowStatusIdle(idleStatus), "Event status can display idle while active");
        idleStatus.charges = 0;
        check(!BattleFeedback.ShouldShowStatusIdle(idleStatus), "Exhausted status hides idle");
        idleStatus.charges = 1;
        check(BattleFeedback.ShouldShowStatusIdle(idleStatus), "Recharged status restores idle");
        idleStatus.remainingTurns = 0;
        check(!BattleFeedback.ShouldShowStatusIdle(idleStatus), "Expired status hides idle");
        check(DamageModifierFeedback.SymbolType(new DamageCalculationStep()) == BattlePresentationType.Neutral, "Unrestricted source uses physical icon regardless of hit element");
        check(DamageModifierFeedback.SymbolType(new DamageCalculationStep { modifierTags = DamageTag.Fire }) == BattlePresentationType.Fire, "Fire-restricted source uses fire icon");
        check(DamageModifierFeedback.Stage(new DamageCalculationStep { modifierPhase = DamageModifierPhase.Activation }) == 0, "Skill activation precedes modifiers");
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
                step.modifierPhase = sign > 0 ? DamageModifierPhase.Increase : DamageModifierPhase.Decrease;
                check(DamageModifierFeedback.Stage(step) == (defense ? sign > 0 ? 4 : 2 : sign > 0 ? 1 : 3), "Presentation order: buff, defense, weakness, vulnerability");
                check(DamageModifierFeedback.PresentationType(step) == ((defense ? sign < 0 : sign > 0) ? BattlePresentationType.ModifierUp : BattlePresentationType.ModifierDown), "Shared audio follows arrow direction");
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
            foreach (string scenePath in new[] { "Assets/Scenes/ChessScene.unity", "Assets/Scenes/CardScene.unity", "Assets/Scenes/StartScene.unity" })
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(scenePath);
                try
                {
                    var audio = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BattleFeedback>(true)).Single();
                    var up = audio.presentationStyles.Single(style => style.type == BattlePresentationType.ModifierUp);
                    var down = audio.presentationStyles.Single(style => style.type == BattlePresentationType.ModifierDown);
                    var visualizer = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DamageCalculationVisualizer>(true)).FirstOrDefault();
                    if (visualizer != null && scenePath.Contains("ChessScene"))
                    {
                        var mageObject = new GameObject("Source fixture");
                        var victimObject = new GameObject("Target fixture");
                        var maryObject = new GameObject("Bloody Mary fixture");
                        try
                        {
                            var mage = mageObject.AddComponent<Pawn>(); var victim = victimObject.AddComponent<Pawn>();
                            var resolve = typeof(DamageCalculationVisualizer).GetMethod("ResolvePresentationAnchor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var sequence = new DamageCalculationSequence { attacker = mage, target = victim };
                            var step = new DamageCalculationStep { modifierPhase = DamageModifierPhase.Activation, iconPiece = mage };
                            Check((Transform)resolve.Invoke(visualizer, new object[] { step, sequence, new CardDefinition { id = "J07", cardType = CardType.JobChange } }) == mage.transform, "Mage activation uses explicit source piece");
                            var mary = maryObject.AddComponent<Queen>();
                            var maryCard = Resources.Load<CardAsset>("Cards/J09").CreateDefinition();
                            var bonus = new DamageCalculationStep { modifierPhase = DamageModifierPhase.Increase,
                                iconPiece = mary, sourceCardId = maryCard.id, icon = maryCard.skillImage,
                                hasContribution = true, contribution = maryCard.friendlyDamageBonus, countingIcon = DamageCountingIcon.Attack };
                            var statusOwner = (Transform)resolve.Invoke(visualizer, new object[] { bonus, sequence, maryCard });
                            var resolveRecipient = typeof(DamageCalculationVisualizer).GetMethod("ResolveModifierRecipient",
                                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                            Check(statusOwner == mary.transform && bonus.icon != null && bonus.contribution > 0 &&
                                (Transform)resolveRecipient.Invoke(null, new object[] { bonus, sequence, maryCard, statusOwner }) == mage.transform,
                                "J09 status belongs to Bloody Mary; the capturing pawn receives the increase effect");
                            foreach (string statusCardId in new[] { "E05", "E09" })
                            {
                                var statusCard = Resources.LoadAll<CardAsset>("Cards").Single(a => a.definition.id == statusCardId).CreateDefinition();
                                var definition = statusCard.statusesToApply[0];
                                Check(definition.idleEffectPrefab != null && definition.idleEffectPrefab.GetComponent<CardIdleEffectMotion>() != null, statusCardId + " loads looping status idle prefab");
                                var status = new StatusRuntime(definition, victim, mage, false);
                                var context = new DamageContext { source = mage, target = victim, sourceStatus = status };
                                step.iconPiece = DamageModifierFeedback.ActivationSource(context);
                                Check((Transform)resolve.Invoke(visualizer, new object[] { step, sequence, statusCard }) == victim.transform, statusCardId + " activation uses status bearer, not caster or field");
                                context.source = null;
                                Check(DamageModifierFeedback.ActivationSource(context) == victim, statusCardId + " event status without caster keeps bearer anchor");
                                BattleFeedback.StartStatusIdle(status);
                                Check(status.idleEffectInstance != null && status.idleEffectInstance.transform.parent == victim.transform, statusCardId + " idle attaches to bearer");
                                UnityEngine.Object.DestroyImmediate(status.idleEffectInstance);
                                status.owner = mage;
                                Check(DamageModifierFeedback.ActivationSource(context) == mage, statusCardId + " transfer follows new bearer");
                            }
                            sequence.attacker = null; step.iconPiece = null;
                            var fireballAnchor = (Transform)resolve.Invoke(visualizer, new object[] { step, sequence, new CardDefinition { id = "E02", cardType = CardType.Event } });
                            Check(fireballAnchor != null && fireballAnchor != victim.transform && fireballAnchor.GetComponent<FieldCardPlace>() != null, "Fireball without source uses field area, never victim");
                            var fieldAnchor = (Transform)resolve.Invoke(visualizer, new object[] { step, sequence, new CardDefinition { id = "F01", cardType = CardType.Field } });
                            Check(fieldAnchor != null && fieldAnchor.GetComponent<FieldCardPlace>() != null, "Lava source uses field location");
                            sequence.presentationOnly = true; step.modifierPhase = DamageModifierPhase.Increase;
                            Check((Transform)resolve.Invoke(visualizer, new object[] { step, sequence, new CardDefinition { id = "F09", cardType = CardType.Field } }) == victim.transform, "Black Market gain uses recipient even though source is field");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(mageObject); UnityEngine.Object.DestroyImmediate(victimObject); UnityEngine.Object.DestroyImmediate(maryObject); }
                    }
                    Check(up.sound != null && down.sound != null && up.sound != down.sound && up.icon != null && down.icon != null,
                        scenePath + " Presentation Styles contains both shared arrow styles");
                    foreach (var style in new[] { up, down })
                    {
                        if (style.effectPrefab == null) continue;
                        var create = typeof(BattleFeedback).GetMethod("CreateTypeEffect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        Vector3 anchorPosition = new Vector3(7, 2, 5);
                        var legacy = UnityEngine.Object.Instantiate(style.effectPrefab, anchorPosition, style.effectPrefab.transform.rotation);
                        try
                        {
                            var oldAnimator = legacy.GetComponentInChildren<Animator>();
                            if (oldAnimator != null)
                            {
                                oldAnimator.Rebind(); oldAnimator.Update(0); oldAnimator.Update(.25f);
                                results.Add("DIAGNOSTIC: " + style.type + " bare animated root requested=" + anchorPosition + " actual=" + legacy.transform.position);
                            }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(legacy); }
                        var effect = (GameObject)create.Invoke(null, new object[] { style, anchorPosition, audio.typeEffectRoot != null ? audio.typeEffectRoot : audio.transform });
                        try
                        {
                            Check(effect.activeInHierarchy && effect.transform.lossyScale.sqrMagnitude > .001f && effect.GetComponentInParent<Canvas>() == null,
                                style.type + " stays visible through configured TypeEffects parent");
                            Check(Quaternion.Angle(effect.transform.GetChild(0).rotation, style.effectPrefab.transform.rotation) < .01f, style.type + " preserves prefab world rotation");
                            var animator = effect.GetComponentInChildren<Animator>();
                            var arrows = effect.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("_Arrow_")).ToArray();
                            if (animator != null && arrows.Length > 0)
                            {
                                Check(Vector3.Distance(effect.transform.lossyScale, Vector3.one * style.effectScale) < .001f &&
                                    Mathf.Approximately(animator.speed, style.effectPlaybackSpeed) &&
                                    Mathf.Approximately(style.PlaybackLifetime * animator.speed, style.effectLifetime),
                                    style.type + " honors the configured size, speed and playback lifetime");
                                animator.Rebind(); animator.Update(0); animator.Update(.25f / animator.speed);
                                var positions = arrows.Select(t => t.position.y).ToArray();
                                Check(arrows.All(t => t.localScale.sqrMagnitude > .001f), style.type + " spawned arrows become visible");
                                var renderers = effect.GetComponentsInChildren<Renderer>();
                                var bounds = renderers[0].bounds;
                                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                                Check(Mathf.Max(bounds.size.x, bounds.size.z) < .84f,
                                    style.type + " arrow cluster fits inside the .84-unit idle aura diameter");
                                results.Add("INFO: " + style.type + " bounds=" + bounds + " scale=" + effect.transform.lossyScale + " rotation=" + effect.transform.eulerAngles);
                                animator.Update(.45f / animator.speed);
                                Check(Vector3.Distance(effect.transform.position, anchorPosition + style.effectOffset) < .01f, style.type + " animation cannot reset world anchor");
                                Check(arrows.All(t => Mathf.Abs(t.position.x - anchorPosition.x - style.effectOffset.x) < 3f && Mathf.Abs(t.position.z - anchorPosition.z - style.effectOffset.z) < 3f), style.type + " animated arrows remain at requested source instead of board origin");
                                Check(arrows.Select((t, i) => style.type == BattlePresentationType.ModifierUp ? t.position.y > positions[i] + .1f * style.effectScale : t.position.y < positions[i] - .1f * style.effectScale).All(v => v), style.type + " spawned arrows move vertically in correct direction");
                            }
                        }
                        finally { UnityEngine.Object.DestroyImmediate(effect); }
                    }
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            }
            Check(settings != null && settings.increaseIcon != null && settings.decreaseIcon != null, "Both shared modifier icons are included");
            Check(settings.arrowUp != null && settings.arrowDown != null && settings.defenseIcon != null, "Arrows and defense shield are assigned");
            var attackerObject = new GameObject("Modifier test attacker");
            var targetObject = new GameObject("Modifier test target");
            var auraObject = new GameObject("Modifier test aura owner");
            var panel = DamageResolutionPanel.Create(null);
            var audioFixture = new GameObject("Style fixture").AddComponent<BattleFeedback>();
            audioFixture.presentationStyles = new[] {
                new BattlePresentationStyle { type = BattlePresentationType.ModifierUp, icon = settings.arrowUp },
                new BattlePresentationStyle { type = BattlePresentationType.ModifierDown, icon = settings.arrowDown },
                new BattlePresentationStyle { type = BattlePresentationType.Defense, icon = settings.defenseIcon },
                new BattlePresentationStyle { type = BattlePresentationType.Neutral, icon = settings.increaseIcon }
            };
            var instanceField = typeof(BattleFeedback).GetField("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var previousAudio = instanceField.GetValue(null);
            instanceField.SetValue(null, audioFixture);
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
                        Check(panel.AttachedPiece == aura.transform, "Modifier anchors to effect source: defense=" + defense + " sign=" + sign);
                        var arrow = panel.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i => i.name == "Modifier arrow");
                        bool up = defense ? sign < 0 : sign > 0;
                        Check(DamageModifierFeedback.PresentationType(step) == (up ? BattlePresentationType.ModifierUp : BattlePresentationType.ModifierDown), "Sound follows arrow rather than damage sign: defense=" + defense + " sign=" + sign);
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
                instanceField.SetValue(null, previousAudio);
                UnityEngine.Object.DestroyImmediate(audioFixture.gameObject);
                UnityEngine.Object.DestroyImmediate(panel.gameObject);
                UnityEngine.Object.DestroyImmediate(attackerObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(auraObject);
            }

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
                        s.triggerSound == null)),
                    card.id + " has no individual trigger effects or audio");
                if (card.statusesToApply.Count > 0)
                {
                    var copy = asset.CreateDefinition();
                    Check(card.statusesToApply.Count == copy.statusesToApply.Count &&
                        card.statusesToApply.Select((s, i) => s?.idleEffectPrefab == copy.statusesToApply[i]?.idleEffectPrefab).All(equal => equal),
                        card.id + " preserves idle effects when loaded");
                }
            }
        }
        catch (Exception error) { results.Add("FAIL: " + error); throw; }
        finally { Directory.CreateDirectory("output"); File.WriteAllLines("output/shared-damage-modifiers.txt", results); }
        Debug.Log("Shared damage modifiers: " + results.Count + " passed");
    }
}
