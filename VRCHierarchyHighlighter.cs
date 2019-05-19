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

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

using UnityEditor;
using UnityEngine;


public static class HierarchyIndentHelper
{
    private const string kResourceDirPath = "Assets/VRCHierarchyHighlighter/Editor/Resources/";
    private const string kResourceSuffix = ".png";
    private static readonly string[] kIconNames = {
        "DynamicBone",
        "SkinnedMeshRenderer",
        "VRC_AvatarDescriptor",
        //"Cloth",
        //"Rigidbody",
        //"Joint",
        //"Collider"
    };
    private static Dictionary<string, Texture2D> icon_resources_ = new Dictionary<string, Texture2D>();

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
        var precision = 100.0f;
        var s = 0.7f;
        var v = 0.7f;
        var h = ((float)(target_rect.x) / precision) % 1.0f;
        var alpha = 0.2f;

        var background_color = UnityEngine.Color.HSVToRGB(h, s, v);
        var color = GUI.color;

        GUI.color = new Color(
            background_color.r,
            background_color.g,
            background_color.b,
            alpha
        );

        var rect = target_rect;
        rect.x = target_rect.x;
        rect.xMax = target_rect.xMax;
        GUI.Box(rect, "");

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
                foreach (Component component in components)
                {
                    foreach (var icon_info in icon_resources_)
                    {
                        if (component != null && component.ToString().Contains(icon_info.Key))
                        {
                            Color boxcolor = Color.white;
                            GUI.color = boxcolor;
                            rect.x = 0; //target_rect.xMax - target_rect.height - 20; // 右寄せにする場合
                            rect.xMax = target_rect.xMax;
                            rect.width = 20;
                            rect.height = 20;

                            GUI.Label(rect, icon_info.Value);
                        }
                    }

                }
            }
        }

        GUI.color = color;
    }
}
