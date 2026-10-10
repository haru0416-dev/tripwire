using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Tripwire.Core;
using UdonSharp;
using UdonBridge;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    // The generated behaviour on the trigger's object: reused, switched in place or recreated, then its fields bound.
    public static partial class TripwireCompiler
    {
        /// <summary>A behaviour Tripwire generated on the trigger's object that no other trigger on it points at.</summary>
        static UdonBehaviour Unowned(TripwireTrigger t)
        {
            var others = t.GetComponents<TripwireTrigger>().Where(o => o != t).Select(o => o.generated).ToList();
            foreach (var ub in t.GetComponents<UdonBehaviour>())
            {
                if (others.Contains(ub)) continue;
                if (!(ub.programSource is UdonSharpProgramAsset p) || p.sourceCsScript == null) continue;
                if (AssetDatabase.GetAssetPath(p.sourceCsScript).StartsWith(OutputDir + "/", StringComparison.Ordinal)) return ub;
            }
            return null;
        }

        /// <summary>
        /// Whether a component comes with a prefab instance (not added to it in the scene): adding or removing
        /// components there must happen in the prefab itself.
        /// </summary>
        static bool FromPrefab(GameObject go, Component c) => PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsAddedComponentOverride(c);

        /// <summary>Make sure the trigger's GameObject carries the right generated behaviour.</summary>
        static bool EnsureBehaviour(TripwireTrigger t, GeneratedProgram g)
        {
            var type = FindGeneratedType(g.ClassName);
            if (type == null) return false;

            var programAsset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(type);
            if (programAsset == null)
                return Fail(t, Texts.T("No UdonSharp program asset for " + g.ClassName + ". Press Apply now to try again.", g.ClassName + " の UdonSharp プログラムアセットがありません。「今すぐ反映」でやり直してください。"));

            var current = t.generated;
            // A component copied/pasted from another object still points at that object's behaviour: never touch it.
            if (current != null && current.gameObject != t.gameObject) current = null;
            // Pasted values, Reset or a reverted prefab override lose the link: a generated behaviour on this object that
            // no other trigger here owns is this trigger's, so it is reused rather than a second one added beside it.
            if (current == null) current = Unowned(t);
            if (current != null && current.programSource == programAsset)
            {
                if (t.generated != current || t.generatedClass != g.ClassName) { Undo.RecordObject(t, "Apply Tripwire Trigger"); t.generated = current; t.generatedClass = g.ClassName; }
                return true;
            }

            // Adding a component to a prefab instance must happen in the prefab: decided before anything changes.
            bool triggerFromPrefab = FromPrefab(t.gameObject, t);
            string inPrefab = Texts.T(t.name + " comes from a prefab. Open the prefab and apply the trigger there.", t.name + " はプレハブから来ています。プレハブを開いて、そこで反映してください。");
            Undo.RecordObject(t, "Apply Tripwire Trigger");
            if (current != null)
            {
                // Keep the UdonBehaviour itself so references to it (UI events, other scripts) survive the program change.
                if (UdonSharpPrograms.SwapProgram(current, type, programAsset, "Apply Tripwire Trigger"))
                {
                    t.generated = current;
                    t.generatedClass = g.ClassName;
                    EditorUtility.SetDirty(t);
                    return true;
                }
                // Recreating destroys the old behaviour: stop first if it can't be added back here.
                if (triggerFromPrefab || FromPrefab(t.gameObject, current)) return Fail(t, inPrefab);
                Debug.LogWarning("[Tripwire] " + Texts.T("Could not switch the program in place on " + t.name + "; recreating its UdonBehaviour (references to it from elsewhere must be re-assigned).", t.name + " のプログラムをその場で差し替えられなかったので、UdonBehaviour を作り直します（ほかの場所からの参照は入れ直してください）。"), t);
                var oldProxy = UdonSharpEditorUtility.GetProxyBehaviour(current);
                if (oldProxy != null) UdonSharpUndo.DestroyImmediate(oldProxy);
                else Undo.DestroyObjectImmediate(current);
            }

            if (triggerFromPrefab) return Fail(t, inPrefab);

            var proxy = UdonSharpUndo.AddComponent(t.gameObject, type);
            t.generated = proxy != null ? UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy) : null;
            t.generatedClass = g.ClassName;
            EditorUtility.SetDirty(t);
            if (t.generated == null)
                return Fail(t, Texts.T("UdonSharp couldn't set up the behaviour on " + t.name + ". If it is part of a prefab, apply it in the prefab.", "UdonSharp が " + t.name + " の準備をできませんでした。プレハブの一部なら、プレハブを開いて反映してください。"));
            return true;
        }

        /// <summary>Fills the generated behaviour's fields with the dragged-in references. False (with the reason recorded) if some can't be.</summary>
        static bool Bind(TripwireTrigger t, GeneratedProgram g)
        {
            var proxy = UdonSharpEditorUtility.GetProxyBehaviour(t.generated);
            if (proxy == null)
                return Fail(t, Texts.T("UdonSharp hasn't set up the generated behaviour on " + t.name + " yet. Press Apply now to try again.", t.name + " の生成コンポーネントの準備を UdonSharp がまだ終えていません。「今すぐ反映」でやり直してください。"));
            // Compared with what the UdonBehaviour stores (the proxy may lag behind it, e.g. after a revert on the behaviour).
            UdonSharpEditorUtility.CopyUdonToProxy(proxy);
            var type = proxy.GetType();

            // What each field should hold; written (and recorded for undo) only when something differs, so applying
            // an unchanged trigger leaves the scene unchanged too.
            var values = new List<(System.Reflection.FieldInfo field, object value)>();
            string failed = null;
            var flat = new Dictionary<int, List<KAction>>();
            foreach (var b in g.Bindings)
            {
                var field = type.GetField(b.Field);
                if (b.Kind == BindingKind.Constant)
                {
                    // A value typed in the Inspector (CodeGenerator.ConstantsInFields): the code no longer holds it.
                    if (field != null) values.Add((field, ConstantFor(field.FieldType, b.Constant)));
                    continue;
                }
                if (b.UrlValue != null)
                {
                    // URLs cannot be built at runtime in VRChat; the editor stores them.
                    if (field != null) values.Add((field, new VRC.SDKBase.VRCUrl(b.UrlValue)));
                    continue;
                }
                var elementType = TripwireModel.ResolveType(b.UnityType);
                if (field == null || elementType == null)
                {
                    // Usually a type from a package that is no longer installed (a video player's, for example).
                    failed = Texts.T("Can't fill in " + b.UnityType + ": the type isn't in this project (a removed package?). Pick the objects again, or remove the action that uses it.",
                                     b.UnityType + " の値を入れられません。この型がプロジェクトにありません（パッケージを外しましたか）。オブジェクトを選び直すか、それを使うアクションを外してください。");
                    continue;
                }
                List<Object> objects;
                switch (b.Kind)
                {
                    case BindingKind.VariableInitial: objects = t.variables[b.Variable].initial.objects; break;
                    case BindingKind.ListenTarget: objects = new List<Object> { t.events[b.Event].listenTarget }; break;
                    case BindingKind.UiElement: objects = new List<Object> { t.events[b.Event].uiTarget }; break;
                    default:
                        if (!flat.TryGetValue(b.Event, out var actions)) flat[b.Event] = actions = TripwireModel.FlatActions(t.events[b.Event].actions);
                        objects = actions[b.Action].args[b.Arg].objects;
                        break;
                }
                if (b.IsArray)
                {
                    var array = Array.CreateInstance(elementType, objects.Count);
                    for (int i = 0; i < objects.Count; i++)
                        array.SetValue(TripwireModel.Coerce(objects[i], elementType), i);
                    values.Add((field, array));
                }
                else
                {
                    values.Add((field, objects.Count > 0 ? TripwireModel.Coerce(objects[0], elementType) : null));
                }
            }

            var text = g.InteractText ?? "Use";
            bool fieldsChanged = values.Any(v => !SameValue(v.field.GetValue(proxy), v.value));
            if (fieldsChanged)
            {
                Undo.RecordObject(proxy, "Apply Tripwire Trigger");
                Undo.RecordObject(t.generated, "Apply Tripwire Trigger");
                foreach (var (field, value) in values) field.SetValue(proxy, value);
                UdonSharpEditorUtility.CopyProxyToUdon(proxy);
            }
            UiWiring.Wire(t);
            bool textChanged = t.generated.InteractionText != text;
            if (textChanged)
            {
                Undo.RecordObject(t.generated, "Apply Tripwire Trigger");
                t.generated.InteractionText = text;
            }
            if (fieldsChanged || textChanged)
            {
                EditorUtility.SetDirty(t.generated);
                if (t.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            }
            return failed == null || Fail(t, failed);
        }

        /// <summary>A constant binding's value as the field's type: float[] becomes the vector, color or rotation.</summary>
        static object ConstantFor(Type fieldType, object value)
        {
            if (!(value is float[] f)) return value;
            float At(int i) => i < f.Length ? f[i] : 0f;
            if (fieldType == typeof(Vector2)) return new Vector2(At(0), At(1));
            if (fieldType == typeof(Vector3)) return new Vector3(At(0), At(1), At(2));
            if (fieldType == typeof(Color)) return new Color(At(0), At(1), At(2), At(3));
            if (fieldType == typeof(Quaternion)) return new Quaternion(At(0), At(1), At(2), At(3));
            return value;
        }

        /// <summary>Field values compared as the behaviour stores them (URLs by text, arrays element by element).</summary>
        static bool SameValue(object a, object b)
        {
            if (a is VRC.SDKBase.VRCUrl ua && b is VRC.SDKBase.VRCUrl ub) return ua.Get() == ub.Get();
            if (a is Array aa && b is Array ba)
            {
                if (aa.Length != ba.Length || aa.GetType() != ba.GetType()) return false;
                for (int i = 0; i < aa.Length; i++) if (!SameValue(aa.GetValue(i), ba.GetValue(i))) return false;
                return true;
            }
            if (a is Object || b is Object) return (a as Object) == (b as Object);
            return Equals(a, b);
        }
    }
}
