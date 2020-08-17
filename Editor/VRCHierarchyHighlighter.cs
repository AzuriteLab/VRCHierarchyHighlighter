/*  ヒエラルキを階層別に色分けするのと、VRC向けの重要コンポーネントがある場合にアイコンで可視化するやつ
 * 
 *  see also: http://baba-s.hatenablog.com/entry/2015/05/09/122713
 */

/*
The MIT License (MIT)

Copyright (c) 2019 AzuriteLab

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
using System.Linq;
using System.Collections.Generic;
using System.IO;

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HierarchyIndentHelper
{

    private const string kResourceDirPath = "Assets/VRCHierarchyHighlighter/Editor/Resources/";
    private const string kResourceSuffix = ".png";
    private const int kIconSize = 20;
    // TODO SDK2とSDK3で名前空間が異なるため、それぞれのコンポーネント名を区別するようにする。現状は省略された形で応急的に対応している
    // TODO SDKに含まれるMirror prefabを使ってしまうと、MeshRendererが優先されてしまう。アイコンの適用方法をコンポーネント名を一旦キャッシュするなどして変更する必要がある
    private static readonly IDictionary<string, Type> kIconNamesAndTypes = new Dictionary<string, Type>()
    {
        { "DynamicBone", null },
        { "DynamicBonePartial", null },
        { "DynamicBoneRoot", null },
        { "DynamicBoneCollider", null },
        { "MeshRenderer", typeof(MeshRenderer) },
        { "SkinnedMeshRenderer", typeof(SkinnedMeshRenderer) },
        { "AvatarDescriptor", null },
        { "AudioSource", typeof(AudioSource) },
        { "Light", typeof(Light) },
        { "LightProbe", typeof(LightProbes) },
        { "ReflectionProbe", typeof(ReflectionProbe) },
        { "MirrorReflection", null },
    };
    private static readonly Type kDynamicBoneType = Type.GetType("DynamicBone, Assembly-CSharp");

    private static Dictionary<string, Texture2D> icon_resources_
        = new Dictionary<string, Texture2D>();

    private static IEnumerable<Transform> dynamic_bone_roots_ = new List<Transform>();

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
        foreach (var nameAndType in kIconNamesAndTypes)
        {
            Texture2D icon = nameAndType.Value != null
                ? EditorGUIUtility.ObjectContent(null, nameAndType.Value).image as Texture2D
                : LoadIconTex2DFromPNG(kResourceDirPath + nameAndType.Key + kResourceSuffix);
            icon_resources_.Remove(nameAndType.Key);
            icon_resources_.Add(nameAndType.Key, icon);
        }
    }

    [InitializeOnLoadMethod]
    private static void Startup()
    {
        SetupIcons();
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
    }

    private static void OnHierarchyWindowItemOnGUI
    (int instance_id, Rect target_rect)
    {
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
            rect.xMax = target_rect.xMax;
            GUI.Box(rect, "");
        }

        if (VRChierarchyHighlighterEdit.is_draw_icons.GetValue())
        {
            int cnt = icon_resources_.Count;
            if (icon_resources_[kIconNamesAndTypes.First().Key] == null)
            {
                // 実行モードに移行して戻ると何故かメンバの中身が初期化されてしまうので再セットアップ
                // 少数のテクスチャをメインスレッドで読み込むので状況によっては一瞬ラグるかもしれない
                SetupIcons();
            }

            var obj = EditorUtility.InstanceIDToObject(instance_id) as GameObject;
            if (obj != null)
            {
                // シーンの最初のGameObjectであれば、シーン全体のDynamicBoneのm_Rootを取得する
                if (kDynamicBoneType != null)
                {
                    var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                    if (rootGameObjects.FirstOrDefault() == obj)
                    {
                        dynamic_bone_roots_ = rootGameObjects
                            .SelectMany(root => root.GetComponentsInChildren(kDynamicBoneType))
                            .Select(db => kDynamicBoneType.GetField("m_Root").GetValue(db) as Transform)
                            .Where(db_root => db_root != null);
                    }
                }

                var components = obj.GetComponents(typeof(Component));
                if (components.Length > 0)
                {
                    DrawIcons_(components, target_rect);
                }
            }
        }

        GUI.color = color;
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
                        if (kDynamicBoneType.GetField("m_Root").GetValue(component) == null)
                        {
                            icon = icon_resources_["DynamicBonePartial"];
                        }
                    }

                    DrawIcon_(icon, target_rect);

                    if (VRChierarchyHighlighterEdit.is_draw_vers.GetValue())
                    {
                        PreviewVers_(component, target_rect);
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

        target_rect.x = 0;
        target_rect.xMax = target_rect.xMax;
        target_rect.width = kIconSize;
        target_rect.height = kIconSize;

        GUI.Label(target_rect, icon);
    }

    private static void PreviewVers_(Component component, Rect target_rect)
    {
        if (!component.ToString().Contains("SkinnedMeshRenderer"))
        {
            return;
        }

        var rect = EditorGUILayout.GetControlRect();

        Color boxcolor = Color.black;
        GUI.color = boxcolor;
        target_rect.x = rect.xMax - 80; // 右寄せにする場合
        target_rect.width = 100;
        target_rect.height = kIconSize;

        var mesh = ((SkinnedMeshRenderer)component).sharedMesh;
        GUI.Label(target_rect, string.Format("Vers: {0}", mesh.vertexCount));
    }
}

public struct VHHParameter<T>
{
    public VHHParameter(T default_value, string signature, Func<string, T, T> init, Action<string, T> teardown)
    {
        default_value_ = default_value;
        signature_ = signature;
        value_ = init(signature_, default_value_);
        teardown_ = teardown;
    }

    // IDisposableで実装したDisposableが確実に呼び出される保証がないので普通の関数にしてる
    public void Destroy()
    {
        teardown_(signature_, value_);
    }

    public T GetDefault() { return default_value_; }
    public T GetValue() { return value_; }
    public string GetSignature() { return signature_; }

    public void SetValue(T value) { value_ = value; }
    public void SetDefault() { value_ = default_value_; }

    private T value_;
    private T default_value_;
    private string signature_;
    private Action<string, T> teardown_;
}

public class VRChierarchyHighlighterEdit : EditorWindow
{
    [MenuItem("Window/VRChierarchyHighlighter")]
    static void Open()
    {
        GetWindow<VRChierarchyHighlighterEdit>();
    }

    public static VHHParameter<bool> is_draw_icons
        = new VHHParameter<bool>(true, "vhh.is_draw_icons", EditorPrefs.GetBool, EditorPrefs.SetBool);
    public static VHHParameter<bool> is_draw_highlights
        = new VHHParameter<bool>(true, "vhh.is_draw_highlights", EditorPrefs.GetBool, EditorPrefs.SetBool);
    public static VHHParameter<bool> is_draw_vers
        = new VHHParameter<bool>(false, "vhh.is_draw_vers", EditorPrefs.GetBool, EditorPrefs.SetBool);
    public static VHHParameter<float> saturation
        = new VHHParameter<float>(0.7f, "vhh.saturation", EditorPrefs.GetFloat, EditorPrefs.SetFloat);
    public static VHHParameter<float> value
        = new VHHParameter<float>(0.7f, "vhh.value", EditorPrefs.GetFloat, EditorPrefs.SetFloat);
    public static VHHParameter<float> hue
        = new VHHParameter<float>(0.3f, "vhh.hue", EditorPrefs.GetFloat, EditorPrefs.SetFloat);
    public static VHHParameter<float> hue_offset
        = new VHHParameter<float>(0.2f, "vhh.hue_offset", EditorPrefs.GetFloat, EditorPrefs.SetFloat);
    public static VHHParameter<float> alpha
        = new VHHParameter<float>(0.2f, "vhh.alpha", EditorPrefs.GetFloat, EditorPrefs.SetFloat);

    private void OnDestroy()
    {
        is_draw_icons.Destroy();
        is_draw_highlights.Destroy();
        is_draw_vers.Destroy();
        saturation.Destroy();
        value.Destroy();
        hue_offset.Destroy();
        hue.Destroy();
        alpha.Destroy();
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        if (GUI.Button(new Rect(EditorGUILayout.GetControlRect().xMax - 100, 10, 100, 20), "Default"))
        {
            is_draw_icons.SetDefault();
            is_draw_highlights.SetDefault();
            is_draw_vers.SetDefault();
            hue_offset.SetDefault();
            hue.SetDefault();
            saturation.SetDefault();
            value.SetDefault();
            alpha.SetDefault();
        }

        EditorGUILayout.LabelField("General Settings: ");
        EditorGUI.indentLevel++;
        is_draw_icons.SetValue(EditorGUILayout.ToggleLeft("Show Icons", is_draw_icons.GetValue()));
        is_draw_vers.SetValue(EditorGUILayout.ToggleLeft("Show Vertexes", is_draw_vers.GetValue()));
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("(Only when `Show Icons` is enabled)");
        EditorGUI.indentLevel--;
        is_draw_highlights.SetValue(EditorGUILayout.ToggleLeft("Draw Highlights", is_draw_highlights.GetValue()));
        EditorGUI.indentLevel--;

        EditorGUILayout.Separator();

        EditorGUILayout.LabelField("Highlights Settings: ");
        EditorGUI.indentLevel++;

        hue.SetValue(EditorGUILayout.Slider("Hue", hue.GetValue(), 0.0f, 1.0f));
        hue_offset.SetValue(EditorGUILayout.Slider("Hue offset", hue_offset.GetValue(), 0.0f, 1.0f));
        saturation.SetValue(EditorGUILayout.Slider("Saturation", saturation.GetValue(), 0.0f, 1.0f));
        value.SetValue(EditorGUILayout.Slider("Value", value.GetValue(), 0.0f, 1.0f));
        alpha.SetValue(EditorGUILayout.Slider("Alpha", alpha.GetValue(), 0.0f, 1.0f));
        EditorGUI.indentLevel--;

        if (EditorGUI.EndChangeCheck())
        {
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}