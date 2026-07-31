/*  ヒエラルキを階層別に色分けするのと、VRC向けの重要コンポーネントがある場合にアイコンで可視化するやつ
 * 
 *  see also: http://baba-s.hatenablog.com/entry/2015/05/09/122713
 */

/*
The MIT License (MIT)

Copyright (c) 2019-2021 AzuriteLab

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.IO;

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Immutable;

#if (VRC_SDK_VRCSDK3 && !UDON)
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

public enum HighlightMode
{
    Fill = 0,
    Under = 1,
    Left = 2
}

public static class HierarchyIndentHelper
{
    public const string kVersion = "2024.08.30.0";
    public const string kSemanticVersion = "1.1.1";
    private const string kResourceSuffix = ".png"; 
    private const int kIconSize = 20;
    private const string kModularAvatarAssemblyName = "nadena.dev.modular-avatar.core";
    private const string kModularAvatarIconPath
        = "Packages/nadena.dev.modular-avatar/Runtime/Icons/Icon_MA_Script.png";
    // TODO SDK2とSDK3で名前空間が異なるため、それぞれのコンポーネント名を区別するようにする。現状は省略された形で応急的に対応している
    // TODO SDKに含まれるMirror prefabを使ってしまうと、MeshRendererが優先されてしまう。アイコンの適用方法をコンポーネント名を一旦キャッシュするなどして変更する必要がある
    private static readonly IDictionary<string, Type> kIconNamesAndTypes = new Dictionary<string, Type>()
    {
        { "VRCPhysBone", null },
        { "VRCPhysBoneCollider", null },
        { "VRCPhysBonePartial", null },
        { "VRCPhysBoneRoot", null },

        { "DynamicBone", null },
        { "DynamicBonePartial", null },
        { "DynamicBoneRoot", null },
        { "DynamicBoneCollider", null },

        { "MeshRenderer", typeof(MeshRenderer) },
        { "SkinnedMeshRenderer", typeof(SkinnedMeshRenderer) },
        { "AvatarDescriptor", null },
        { "VRC Avatar Descriptor", null },
        { "AudioSource", typeof(AudioSource) },
        { "Light", typeof(Light) },
        { "LightProbe", typeof(LightProbes) },
        { "ReflectionProbe", typeof(ReflectionProbe) },
        { "MirrorReflection", null },
        //
        { "InactiveObject", null },
        { "ToggleActiveObject", null },
    };
    private static readonly Type kDynamicBoneType = Type.GetType("DynamicBone, Assembly-CSharp");
    private static readonly FieldInfo kDynamicBoneMRoot = kDynamicBoneType?.GetField("m_Root");

    private static Dictionary<string, Texture2D> icon_resources_
        = new Dictionary<string, Texture2D>();
    private static Texture2D modular_avatar_icon_;

    private sealed class HierarchyItemCache
    {
        public Texture2D icon;
        public bool has_icon;
        public ulong polygon_count;
        public bool has_polygon_count;
    }

    private static readonly Dictionary<int, HierarchyItemCache> hierarchy_item_cache_
        = new Dictionary<int, HierarchyItemCache>();
    private static readonly Dictionary<Type, Texture2D> component_type_icon_cache_
        = new Dictionary<Type, Texture2D>();
    private static readonly HashSet<Type> component_types_without_icons_
        = new HashSet<Type>();
    private static readonly List<Component> component_buffer_ = new List<Component>();
    private static int hovered_toggle_instance_id_;

    private static ImmutableHashSet<Transform> dynamic_bone_roots_ = ImmutableHashSet<Transform>.Empty;
    private static bool dynamic_bone_roots_dirty_ = true;

    private static Texture2D LoadIconTex2DFromPNG(string path)
    {
        BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read));
        byte[] binary = reader.ReadBytes((int)reader.BaseStream.Length);
        Texture2D tex = new Texture2D(16, 16);
        tex.LoadImage(binary);
        return tex;
    }

    private static void SetupIcons()
    {
        modular_avatar_icon_ = AssetDatabase.LoadAssetAtPath<Texture2D>(kModularAvatarIconPath);

        var resource_dir_path = AssetDatabase.GUIDToAssetPath("31f0293911fb9e44aacd0542a80f0c70") + "/"; // Editor/Resource.meta GUID
        foreach (var nameAndType in kIconNamesAndTypes)
        {
            Texture2D icon = nameAndType.Value != null
                ? EditorGUIUtility.ObjectContent(null, nameAndType.Value).image as Texture2D
                : LoadIconTex2DFromPNG(resource_dir_path + nameAndType.Key + kResourceSuffix);
            icon_resources_.Remove(nameAndType.Key);
            icon_resources_.Add(nameAndType.Key, icon);
        }

        ClearHierarchyItemCache_();
    }

    private static void ClearHierarchyItemCache_()
    {
        hierarchy_item_cache_.Clear();
        component_type_icon_cache_.Clear();
        component_types_without_icons_.Clear();
    }

    private static void OnHierarchyChanged_()
    {
        hierarchy_item_cache_.Clear();
        dynamic_bone_roots_dirty_ = true;
    }

    private static UndoPropertyModification[] OnPostprocessModifications_(
        UndoPropertyModification[] modifications)
    {
        foreach (var modification in modifications)
        {
            var target = modification.currentValue.target;
            var component = target as Component;
            var game_object = target as GameObject;
            if (component != null)
            {
                game_object = component.gameObject;
            }
            if (game_object != null)
            {
                hierarchy_item_cache_.Remove(game_object.GetInstanceID());
            }

            if (kDynamicBoneType != null && target != null
                && kDynamicBoneType.IsInstanceOfType(target))
            {
                dynamic_bone_roots_dirty_ = true;
                hierarchy_item_cache_.Clear();
            }
        }
        return modifications;
    }

    private static void EnsureDynamicBoneRoots_()
    {
        if (!dynamic_bone_roots_dirty_)
        {
            return;
        }

        dynamic_bone_roots_dirty_ = false;
        if (kDynamicBoneType == null)
        {
            dynamic_bone_roots_ = ImmutableHashSet<Transform>.Empty;
            return;
        }

        var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        dynamic_bone_roots_ = rootGameObjects
            .SelectMany(root => root.GetComponentsInChildren(kDynamicBoneType, true))
            .Select(db => kDynamicBoneMRoot.GetValue(db) as Transform)
            .Where(db_root => db_root != null)
            .ToImmutableHashSet();
    }

    private static void EnableHierarchyMouseMoveEvents_()
    {
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (window.GetType().FullName == "UnityEditor.SceneHierarchyWindow")
            {
                window.wantsMouseMove = true;
            }
        }
    }

    private static void EnableMouseMoveEventsForHoveredHierarchy_()
    {
        var window = EditorWindow.mouseOverWindow;
        if (window != null
            && window.GetType().FullName == "UnityEditor.SceneHierarchyWindow"
            && !window.wantsMouseMove)
        {
            window.wantsMouseMove = true;
        }
    }

    [InitializeOnLoadMethod]
    private static void Startup()
    {
        SetupIcons();

        // Unityのバージョン2019以上になった時のため、またはその逆の判定
        if (EditorGUIUtility.isProSkin && !VRChierarchyHighlighterEdit.is_dark_mode.GetValue())
        {
            Debug.Log("VRCHierarchyHighlighter: Standard Skin -> Pro Skin");
            // 通常用からダークモードにプリセットに切り替え
            VRChierarchyHighlighterEdit.SetDefaultAllParameters();

        }
        else if (!EditorGUIUtility.isProSkin && VRChierarchyHighlighterEdit.is_dark_mode.GetValue())
        {
            Debug.Log("VRCHierarchyHighlighter: Pro Skin -> Standard Skin");
            // ダークモードから通常用のプリセットに切り替える
            VRChierarchyHighlighterEdit.SetDefaultAllParameters();
        }


        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
        EditorApplication.hierarchyChanged += () =>
        {
            OnHierarchyChanged_();

            // シーンの最初のGameObjectであれば、シーン全体のDynamicBoneのm_Rootを取得する 
        };
        EditorApplication.projectChanged += ClearHierarchyItemCache_;
        Undo.undoRedoPerformed += OnHierarchyChanged_;
        Undo.postprocessModifications += OnPostprocessModifications_;
        EditorApplication.delayCall += EnableHierarchyMouseMoveEvents_;
    }

    private static void OnHierarchyWindowItemOnGUI
    (int instance_id, Rect target_rect)
    {
        EnableMouseMoveEventsForHoveredHierarchy_();

        var obj = EditorUtility.InstanceIDToObject(instance_id) as GameObject;
        if (obj == null)
        {
            return;
        }

        Rect icon_rect = target_rect;
        icon_rect.y -= 2;
        icon_rect.x = target_rect.xMax - kIconSize;
        icon_rect.width = kIconSize;
        icon_rect.height = kIconSize;

        Event ev = Event.current;
        UpdateToggleHover_(instance_id, icon_rect, ev);
        if (VRChierarchyHighlighterEdit.use_active_checkbox.GetValue() && ev.type == EventType.MouseUp)
        {
            if (icon_rect.Contains(Event.current.mousePosition)) {
                bool shouldBeActive = !obj.activeSelf;
                Undo.RecordObject(obj, "Toggle GameObject Active State");
                obj.SetActive(shouldBeActive);
                obj.tag = shouldBeActive ? "Untagged" : "EditorOnly";
            }
        }

        var color = GUI.color;

        if (VRChierarchyHighlighterEdit.is_draw_highlights.GetValue())
        {
            var hue = ((float)(target_rect.x)
                * VRChierarchyHighlighterEdit.hue_offset.GetValue()
                + VRChierarchyHighlighterEdit.hue.GetValue()) % 1.0f;

            var background_color = Color.HSVToRGB(
                hue, VRChierarchyHighlighterEdit.saturation.GetValue(),
                VRChierarchyHighlighterEdit.value.GetValue()
            );

            GUI.color = new Color(
                background_color.r,
                background_color.g,
                background_color.b,
                VRChierarchyHighlighterEdit.alpha.GetValue()
            );

            var rect = target_rect;
            rect.x = target_rect.x;
            rect.xMax = target_rect.xMax - kIconSize;

            var mode = VRChierarchyHighlighterEdit.highlight_mode.GetValue();
            switch(mode)
            {
                case HighlightMode.Under:
                    rect.yMin += (rect.height - 2);
                    break;
                case HighlightMode.Left:
                    rect.xMax = target_rect.x;
                    rect.xMin += (rect.width - 2);
                    break;
            }

            GUI.Box(rect, "");
        }

        if (VRChierarchyHighlighterEdit.is_draw_icons.GetValue() && ev.type == EventType.Repaint)
        {
            if (icon_resources_[kIconNamesAndTypes.First().Key] == null)
            {
                // 実行モードに移行して戻ると何故かメンバの中身が初期化されてしまうので再セットアップ
                // 少数のテクスチャをメインスレッドで読み込むので状況によっては一瞬ラグるかもしれない
                SetupIcons();
            }

            target_rect.y -= 2;

            var item_cache = GetHierarchyItemCache_(obj);
            if (item_cache.has_icon) {
                DrawIcon_(item_cache.icon, target_rect);
            }
            if (VRChierarchyHighlighterEdit.is_draw_polygons.GetValue()
                && item_cache.has_polygon_count) {
                PreviewPolygons_(item_cache.polygon_count, target_rect);
            }
            if (!obj.activeSelf) {
                DrawIcon_(icon_resources_["InactiveObject"], target_rect);
            }
        }

        if (VRChierarchyHighlighterEdit.is_draw_toggle_icons.GetValue()
            && instance_id == hovered_toggle_instance_id_) {
            DrawIcon_(icon_resources_["ToggleActiveObject"], target_rect);
        }

        GUI.color = color;
    }

    private static void UpdateToggleHover_(int instance_id, Rect icon_rect, Event ev)
    {
        if (!VRChierarchyHighlighterEdit.use_active_checkbox.GetValue()
            || !VRChierarchyHighlighterEdit.is_draw_toggle_icons.GetValue())
        {
            if (hovered_toggle_instance_id_ != 0)
            {
                hovered_toggle_instance_id_ = 0;
                EditorApplication.RepaintHierarchyWindow();
            }
            return;
        }

        if (ev.type == EventType.MouseLeaveWindow)
        {
            if (hovered_toggle_instance_id_ != 0)
            {
                hovered_toggle_instance_id_ = 0;
                EditorApplication.RepaintHierarchyWindow();
            }
            return;
        }

        if (!ev.isMouse)
        {
            return;
        }

        bool contains_mouse = icon_rect.Contains(ev.mousePosition);
        if (contains_mouse && hovered_toggle_instance_id_ != instance_id)
        {
            hovered_toggle_instance_id_ = instance_id;
            EditorApplication.RepaintHierarchyWindow();
        }
        else if (!contains_mouse && hovered_toggle_instance_id_ == instance_id)
        {
            hovered_toggle_instance_id_ = 0;
            EditorApplication.RepaintHierarchyWindow();
        }
    }

    private static HierarchyItemCache GetHierarchyItemCache_(GameObject obj)
    {
        HierarchyItemCache item_cache;
        if (hierarchy_item_cache_.TryGetValue(obj.GetInstanceID(), out item_cache))
        {
            return item_cache;
        }

        item_cache = BuildHierarchyItemCache_(obj);
        hierarchy_item_cache_[obj.GetInstanceID()] = item_cache;
        return item_cache;
    }

    private static HierarchyItemCache BuildHierarchyItemCache_(GameObject obj)
    {
        EnsureDynamicBoneRoots_();

        var item_cache = new HierarchyItemCache();
        component_buffer_.Clear();
        obj.GetComponents(component_buffer_);

        if (component_buffer_.Count == 0)
        {
            return item_cache;
        }

        if (modular_avatar_icon_ != null && component_buffer_.Any(IsModularAvatarComponent_))
        {
            item_cache.icon = modular_avatar_icon_;
            item_cache.has_icon = true;
            CachePolygonCount_(component_buffer_, item_cache);
            return item_cache;
        }

        if (dynamic_bone_roots_.Contains(component_buffer_[0].transform))
        {
            item_cache.icon = icon_resources_["DynamicBoneRoot"];
            item_cache.has_icon = true;
            return item_cache;
        }

        foreach (Component component in component_buffer_)
        {
            if (component == null)
            {
                continue;
            }

            Texture2D icon;
            if (!TryGetComponentIcon_(component.GetType(), out icon))
            {
                continue;
            }

            if (kDynamicBoneType != null && component.GetType() == kDynamicBoneType
                && kDynamicBoneMRoot.GetValue(component) == null)
            {
                icon = icon_resources_["DynamicBonePartial"];
            }

#if (VRC_SDK_VRCSDK3 && !UDON)
            var phys_bone = component as VRCPhysBone;
            if (phys_bone != null)
            {
                icon = phys_bone.rootTransform != null
                    ? icon_resources_["VRCPhysBoneRoot"]
                    : icon_resources_["VRCPhysBonePartial"];
            }
#endif

            item_cache.icon = icon;
            item_cache.has_icon = true;

            var skinned_mesh_renderer = component as SkinnedMeshRenderer;
            if (skinned_mesh_renderer != null && skinned_mesh_renderer.sharedMesh != null)
            {
                item_cache.polygon_count = GetPolygonCount_(skinned_mesh_renderer.sharedMesh);
                item_cache.has_polygon_count = true;
            }
            break;
        }

        return item_cache;
    }

    private static bool IsModularAvatarComponent_(Component component)
    {
        return component != null
            && component.GetType().Assembly.GetName().Name == kModularAvatarAssemblyName;
    }

    private static void CachePolygonCount_(
        IEnumerable<Component> components, HierarchyItemCache item_cache)
    {
        foreach (var component in components)
        {
            var skinned_mesh_renderer = component as SkinnedMeshRenderer;
            if (skinned_mesh_renderer == null || skinned_mesh_renderer.sharedMesh == null)
            {
                continue;
            }

            item_cache.polygon_count = GetPolygonCount_(skinned_mesh_renderer.sharedMesh);
            item_cache.has_polygon_count = true;
            return;
        }
    }

    private static bool TryGetComponentIcon_(Type component_type, out Texture2D icon)
    {
        if (component_type_icon_cache_.TryGetValue(component_type, out icon))
        {
            return true;
        }
        if (component_types_without_icons_.Contains(component_type))
        {
            return false;
        }

        foreach (var icon_info in icon_resources_.Reverse())
        {
            if (component_type.Name.Contains(icon_info.Key))
            {
                icon = icon_info.Value;
                component_type_icon_cache_[component_type] = icon;
                return true;
            }
        }

        component_types_without_icons_.Add(component_type);
        icon = null;
        return false;
    }

    private static ulong GetPolygonCount_(Mesh mesh)
    {
        ulong polygon_count = 0;
        for (int sub_mesh_index = 0; sub_mesh_index < mesh.subMeshCount; ++sub_mesh_index)
        {
            var index_count = (ulong)mesh.GetIndexCount(sub_mesh_index);
            switch (mesh.GetTopology(sub_mesh_index))
            {
                case MeshTopology.Triangles:
                    polygon_count += index_count / 3;
                    break;
                case MeshTopology.Quads:
                    polygon_count += index_count / 4;
                    break;
            }
        }
        return polygon_count;
    }

    private static void PreviewPolygons_(ulong polygon_count, Rect target_rect)
    {
        GUI.color = EditorGUIUtility.isProSkin ? Color.white : Color.black;

        target_rect.x = target_rect.xMax - 80 - kIconSize;
        target_rect.width = 100;
        target_rect.height = kIconSize;
        GUI.Label(target_rect, string.Format("△{0}", polygon_count));
    }

    private static void DrawIcons_(Component[] components, Rect target_rect)
    {
        // DynamicBoneのm_Rootの対象となるGameObject
        if (dynamic_bone_roots_.Contains(components[0].transform))
        {
            DrawIcon_(icon_resources_["DynamicBoneRoot"], target_rect);
            return;
        }

        foreach (Component component in components)
        {
            foreach (var icon_info in icon_resources_.Reverse())
            {
                if (component != null && component.GetType().Name.Contains(icon_info.Key))
                {
                    var icon = icon_info.Value;
                    // DynamicBoneのm_Rootに対象となるTransformが設定されていない場合は専用のアイコンに切り替える 
                    if (kDynamicBoneType != null && component.GetType() == kDynamicBoneType)
                    {
                        if (kDynamicBoneMRoot.GetValue(component) == null)
                        {
                            icon = icon_resources_["DynamicBonePartial"];
                        }
                    }

#if (VRC_SDK_VRCSDK3 && !UDON)
                    // PBがアタッチされていたら専用のアイコンに切り替える. rootTransformが設定されていない場合は警告用のアイコンを表示する
                    foreach (var pb in component.GetComponents<VRCPhysBone>().Where(obj => obj != null))
                    {
                        if (pb.rootTransform != null) {
                            icon = icon_resources_["VRCPhysBoneRoot"];
                        } else {
                            icon = icon_resources_["VRCPhysBonePartial"];
                        }
                    }
#endif
                    DrawIcon_(icon, target_rect);

                    if (VRChierarchyHighlighterEdit.is_draw_polygons.GetValue())
                    {
                        PreviewPolygons_(component, target_rect);
                    }
                    return;
                }
            }
        }
    }

    private static void DrawIcon_(Texture2D icon, Rect target_rect)
    {
        Color boxcolor = Color.white;
        GUI.color = boxcolor;

        target_rect.x = target_rect.xMax - kIconSize;
        target_rect.width = kIconSize;
        target_rect.height = kIconSize;

        GUI.Label(target_rect, icon);
    }

    private static void PreviewPolygons_(Component component, Rect target_rect)
    {
        if (!component.ToString().Contains("SkinnedMeshRenderer"))
        {
            return;
        }

        GUI.color = EditorGUIUtility.isProSkin ? Color.white : Color.black;

        target_rect.x = target_rect.xMax - 80 - kIconSize; // 右寄せにする場合
        target_rect.width = 100;
        target_rect.height = kIconSize;

        var mesh = ((SkinnedMeshRenderer)component).sharedMesh;
        if (mesh)
            GUI.Label(target_rect, string.Format("△{0}", mesh.triangles.Length / 3));
    }
}

public struct VHHParameter<T>
{
    public VHHParameter(Func<T> set_default_value, string signature, Func<string, T, T> init, Action<string, T> teardown)
    {
        do_set_default_ = set_default_value;
        signature_ = signature;
        value_ = init(signature_, do_set_default_());
        teardown_ = teardown;
    }

    // IDisposableで実装したDisposableが確実に呼び出される保証がないので普通の関数にしてる
    public void Destroy()
    {
        teardown_(signature_, value_);
    }

    public T GetDefault() { return do_set_default_(); }
    public T GetValue() { return value_; }
    public string GetSignature() { return signature_; }

    public void SetValue(T value) { value_ = value; }
    public void SetDefault() { value_ = do_set_default_(); }

    private T value_;
    private string signature_;
    private Func<T> do_set_default_;
    private Action<string, T> teardown_;
}

public class VRChierarchyHighlighterEdit : EditorWindow
{
    [MenuItem("Window/VRChierarchyHighlighter")]
    static void Open()
    {
        GetWindow<VRChierarchyHighlighterEdit>();
    }

    static private Func<T> SetDefault_<T>(T for_std, T for_pro)
    {
        return () => EditorGUIUtility.isProSkin ? for_pro : for_std;
    }
    static private Func<T> SetDefault_<T>(T value)
    {
        return () => value;
    }

    static private HighlightMode GetHighlightMode(string key, HighlightMode defaultValue)
    {
        return (HighlightMode)EditorPrefs.GetInt(key, (int)defaultValue);
    }

    static private void SetHighlightMode(string key, HighlightMode value)
    {
        EditorPrefs.SetInt(key, (int)value);
    }

    public static VHHParameter<bool> is_draw_icons
        = new VHHParameter<bool>(SetDefault_(true), "vhh.is_draw_icons", EditorPrefs.GetBool, EditorPrefs.SetBool);
    public static VHHParameter<bool> is_draw_toggle_icons
        = new VHHParameter<bool>(SetDefault_(true), "vhh.is_draw_toggle_icons", EditorPrefs.GetBool, EditorPrefs.SetBool);
    public static VHHParameter<bool> is_draw_highlights
        = new VHHParameter<bool>(SetDefault_(true), "vhh.is_draw_highlights", EditorPrefs.GetBool, EditorPrefs.SetBool);
    public static VHHParameter<bool> is_draw_polygons
        = new VHHParameter<bool>(SetDefault_(false), "vhh.is_draw_polygons", EditorPrefs.GetBool, EditorPrefs.SetBool);
    public static VHHParameter<bool> is_dark_mode
        = new VHHParameter<bool>(SetDefault_(for_std: false, for_pro: true), "vhh.is_dark_mode", EditorPrefs.GetBool, EditorPrefs.SetBool);
    public static VHHParameter<bool> use_active_checkbox
        = new VHHParameter<bool>(SetDefault_(false), "vhh.use_active_checkbox", EditorPrefs.GetBool, EditorPrefs.SetBool);
    public static VHHParameter<float> saturation
        = new VHHParameter<float>(SetDefault_(for_std: 0.7f, for_pro: 0.7f), "vhh.saturation", EditorPrefs.GetFloat, EditorPrefs.SetFloat);
    public static VHHParameter<float> value
        = new VHHParameter<float>(SetDefault_(for_std: 0.7f, for_pro: 8.5f), "vhh.value", EditorPrefs.GetFloat, EditorPrefs.SetFloat);
    public static VHHParameter<float> hue
        = new VHHParameter<float>(SetDefault_(for_std: 0.3f, for_pro: 0.2f), "vhh.hue", EditorPrefs.GetFloat, EditorPrefs.SetFloat);
    public static VHHParameter<float> hue_offset
        = new VHHParameter<float>(SetDefault_(for_std: 0.2f, for_pro: 0.2f), "vhh.hue_offset", EditorPrefs.GetFloat, EditorPrefs.SetFloat);
    public static VHHParameter<float> alpha
        = new VHHParameter<float>(SetDefault_(for_std: 0.2f, for_pro: 1.0f), "vhh.alpha", EditorPrefs.GetFloat, EditorPrefs.SetFloat);
    public static VHHParameter<HighlightMode> highlight_mode
        = new VHHParameter<HighlightMode>(SetDefault_(for_std: HighlightMode.Fill, for_pro: HighlightMode.Under), "vhh.highlight_mode", GetHighlightMode, SetHighlightMode);

    static public void SetDefaultAllParameters()
    {
        is_draw_icons.SetDefault();
        is_draw_highlights.SetDefault();
        is_draw_toggle_icons.SetDefault();
        is_draw_polygons.SetDefault();
        is_dark_mode.SetDefault();
        use_active_checkbox.SetDefault();
        hue_offset.SetDefault();
        hue.SetDefault();
        saturation.SetDefault();
        value.SetDefault();
        alpha.SetDefault();
        highlight_mode.SetDefault();
    }

    private void OnDestroy()
    {
        is_draw_icons.Destroy();
        is_draw_highlights.Destroy();
        is_draw_toggle_icons.Destroy();
        is_draw_polygons.Destroy();
        is_dark_mode.Destroy();
        use_active_checkbox.Destroy();
        saturation.Destroy();
        value.Destroy();
        hue_offset.Destroy();
        hue.Destroy();
        alpha.Destroy();
        highlight_mode.Destroy();
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        if (GUI.Button(new Rect(EditorGUILayout.GetControlRect().xMax - 100, 10, 100, 20), "Default"))
        {
            SetDefaultAllParameters();
        }

        EditorGUILayout.LabelField("General Settings: ");
        EditorGUI.indentLevel++;
        is_draw_icons.SetValue(EditorGUILayout.ToggleLeft("Show Icons", is_draw_icons.GetValue()));
        is_draw_polygons.SetValue(EditorGUILayout.ToggleLeft("Show Polygons", is_draw_polygons.GetValue()));
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("(Only when `Show Icons` is enabled)");
        EditorGUI.indentLevel--;
        is_draw_highlights.SetValue(EditorGUILayout.ToggleLeft("Draw Highlights", is_draw_highlights.GetValue()));
        use_active_checkbox.SetValue(EditorGUILayout.ToggleLeft("Enable Object Toggle Checkbox", use_active_checkbox.GetValue()));
        EditorGUI.indentLevel++;
        is_draw_toggle_icons.SetValue(EditorGUILayout.ToggleLeft("Show Toggle Icons", is_draw_toggle_icons.GetValue()));
        EditorGUI.indentLevel--;
        if (use_active_checkbox.GetValue() == false) {
            is_draw_toggle_icons.SetValue(false);
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Separator();

        EditorGUILayout.LabelField("Highlights Settings: ");
        EditorGUI.indentLevel++;

        hue.SetValue(EditorGUILayout.Slider("Hue", hue.GetValue(), 0.0f, 2.0f));
        hue_offset.SetValue(EditorGUILayout.Slider("Hue offset", hue_offset.GetValue(), 0.0f, 1.0f));
        saturation.SetValue(EditorGUILayout.Slider("Saturation", saturation.GetValue(), 0.0f, 2.0f));
        value.SetValue(EditorGUILayout.Slider("Value", value.GetValue(), 0.0f, 10.0f));
        alpha.SetValue(EditorGUILayout.Slider("Alpha", alpha.GetValue(), 0.0f, 2.0f));

        highlight_mode.SetValue((HighlightMode)EditorGUILayout.EnumPopup("Highlight Mode : ", highlight_mode.GetValue()));

        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField(" ");
        EditorGUILayout.LabelField("---");
        EditorGUILayout.LabelField("Version: " + HierarchyIndentHelper.kVersion);

        if (EditorGUI.EndChangeCheck())
        {
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
