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

using System.Linq;
using System.Collections.Generic;
using System.IO;

using UnityEditor;
using UnityEngine;

public static class HierarchyIndentHelper
{

    private const string kResourceDirPath = "Assets/VRCHierarchyHighlighter/Editor/Resources/";
    private const string kResourceSuffix = ".png";
    private const int kIconSize = 20;
    private static readonly string[] kIconNames = {
        "DynamicBone",
        "DynamicBonePartial",
        "SkinnedMeshRenderer",
        "MeshRenderer",
        "VRC_AvatarDescriptor",
    };

    private static Dictionary<string, Texture2D> icon_resources_
        = new Dictionary<string, Texture2D>();

    private static Dictionary<string, Texture2D> optional_icon_resources_
        = new Dictionary<string, Texture2D>();

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
        foreach (string name in kIconNames)
        {
            string filepath = kResourceDirPath + name + kResourceSuffix;
            Texture2D icon = LoadIconTex2DFromPNG(filepath);
            icon_resources_.Remove(name);
            icon_resources_.Add(name, icon);
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

        if (VRChierarchyHighlighterEdit.is_draw_highlights)
        {
            var hue = ((float)(target_rect.x) / VRChierarchyHighlighterEdit.precision) % 1.0f;

            var background_color = UnityEngine.Color.HSVToRGB(
                hue,
                VRChierarchyHighlighterEdit.saturation,
                VRChierarchyHighlighterEdit.value);

            GUI.color = new Color(
                background_color.r,
                background_color.g,
                background_color.b,
                VRChierarchyHighlighterEdit.alpha
            );

            var rect = target_rect;
            rect.x = target_rect.x;
            rect.xMax = target_rect.xMax;
            GUI.Box(rect, "");
        }

        if (VRChierarchyHighlighterEdit.is_draw_icons)
        {
            int cnt = icon_resources_.Count;
            if (icon_resources_[kIconNames[0]] == null)
            {
                // 実行モードに移行して戻ると何故かメンバの中身が初期化されてしまうので再セットアップ
                // 少数のテクスチャをメインスレッドで読み込むので状況によっては一瞬ラグるかもしれない
                SetupIcons();
            }

            var obj = EditorUtility.InstanceIDToObject(instance_id) as GameObject;
            if (obj != null)
            {
                var components = obj.GetComponents(typeof(Component));
                if (components != null)
                {
                    DrawIcons_(components, target_rect);
                }
            }
        }

        GUI.color = color;
    }

    private static void DrawIcons_(Component[] components, Rect target_rect)
    {
        foreach (Component component in components)
        {
            // SkinnedMeshRendererとMeshRendererのの関係で逆順にしないと楽にContainsで判定できない
            foreach (var icon_info in icon_resources_.Reverse())
            {
                if (component != null && component.ToString().Contains(icon_info.Key))
                {
                    var icon = icon_info.Value;
                    // DynamicBoneのm_Rootに対象となるTransformが設定されていない場合は専用のアイコンに切り替える                     if (component.ToString().Contains("DynamicBone"))                     {                         var db = (DynamicBone)component;                         if (db.m_Root == null)                         {                             icon = icon_resources_["DynamicBonePartial"];                         }                     }

                    Color boxcolor = Color.white;
                    GUI.color = boxcolor;

                    target_rect.x = 0;
                    target_rect.xMax = target_rect.xMax;
                    target_rect.width = kIconSize;
                    target_rect.height = kIconSize;

                    GUI.Label(target_rect, icon);

                    if (VRChierarchyHighlighterEdit.is_draw_vers)
                    {
                        PreviewVers_(component, target_rect);
                    }
                }
            }
        }
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

public class VRChierarchyHighlighterEdit : EditorWindow
{
    [MenuItem("Window/VRChierarchyHighlighter")]
    static void Open()
    {
        GetWindow<VRChierarchyHighlighterEdit>();
    }

    public const bool kDefaultIsDrawIcons = true;
    public const bool kDefaultIsDrawHighlights = true;
    public const bool kDefaultIsDrawVers = false;
    public const float kDefaultSaturation = 0.7f;
    public const float kDefaultValue = 0.7f;
    public const float kDefaultPrecision = 100.0f;
    public const float kDefaultAlpha = 0.2f;

    private const string kSignatureIsDrawIcons = "vhh.is_draw_icons";
    private const string kSignatureIsDrawHighlights = "vhh.is_draw_highlights";
    private const string kSignatureIsDrawVers = "vhh.is_draw_Vers";
    private const string kSignatureSaturation = "vhh.saturation";
    private const string kSignatureValue = "vhh.value";
    private const string kSignaturePrecision = "vhh.precision";
    private const string kSignatureAlpha = "vhh.alpha";

    public static bool is_draw_icons
        = EditorPrefs.GetBool(kSignatureIsDrawIcons, kDefaultIsDrawIcons);
    public static bool is_draw_highlights
        = EditorPrefs.GetBool(kSignatureIsDrawHighlights, kDefaultIsDrawHighlights);
    public static bool is_draw_vers
        = EditorPrefs.GetBool(kSignatureIsDrawVers, kDefaultIsDrawVers);
    public static float saturation
        = EditorPrefs.GetFloat(kSignatureSaturation, kDefaultSaturation);
    public static float value
        = EditorPrefs.GetFloat(kSignatureValue, kDefaultValue);
    public static float precision
        = EditorPrefs.GetFloat(kSignaturePrecision, kDefaultPrecision);
    public static float alpha
        = EditorPrefs.GetFloat(kSignatureAlpha, kDefaultAlpha);

    private void OnDestroy()
    {
        EditorPrefs.SetBool(kSignatureIsDrawIcons, is_draw_icons);
        EditorPrefs.SetBool(kSignatureIsDrawHighlights, is_draw_highlights);
        EditorPrefs.SetBool(kSignatureIsDrawVers, is_draw_vers);
        EditorPrefs.SetFloat(kSignatureSaturation, saturation);
        EditorPrefs.SetFloat(kSignatureValue, value);
        EditorPrefs.SetFloat(kSignaturePrecision, precision);
        EditorPrefs.SetFloat(kSignatureAlpha, alpha);
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("General Settings: ");
        EditorGUI.indentLevel++;
        is_draw_icons = EditorGUILayout.ToggleLeft("Show Icons", is_draw_icons);
        is_draw_vers = EditorGUILayout.ToggleLeft("Show Vertexes", is_draw_vers);
        is_draw_highlights = EditorGUILayout.ToggleLeft("Draw Highlights", is_draw_highlights);
        EditorGUI.indentLevel--;

        EditorGUILayout.Separator();

        EditorGUILayout.LabelField("Highlights Settings: ");
        EditorGUI.indentLevel++;

        precision = EditorGUILayout.Slider("Hue Precision", precision, 0.0f, 100.0f);
        saturation = EditorGUILayout.Slider("Saturation", saturation, 0.0f, 1.0f);
        value = EditorGUILayout.Slider("Value", value, 0.0f, 1.0f);
        alpha = EditorGUILayout.Slider("Alpha", alpha, 0.0f, 1.0f);
        EditorGUI.indentLevel--;

        EditorGUILayout.Separator();

        if (GUI.Button(new Rect(EditorGUILayout.GetControlRect().xMax - 100, 10, 100, 20), "Default"))
        {
            is_draw_icons = kDefaultIsDrawIcons;
            is_draw_highlights = kDefaultIsDrawHighlights;
            is_draw_vers = kDefaultIsDrawVers;
            precision = kDefaultPrecision;
            saturation = kDefaultSaturation;
            value = kDefaultValue;
            alpha = kDefaultAlpha;
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}