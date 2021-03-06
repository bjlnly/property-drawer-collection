using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;

#endif

/// <summary>
/// ラベル名からアセットを検索し、アタッチします
/// </summary>
public class LabelSearchAttribute : PropertyAttribute
{
    /// <summary>
    /// <para> falseで検索が完了していなくても、とりあえずインスペクターを表示するようになる</para>
    /// <para>trueだと検索が終わるまでインスペクターが表示されない</para>
    /// </summary>
    public bool init = false;

    /// <summary>
    /// <para>インスペクターが表示された初回のみ検索が行われる</para>
    /// <para>falseだと検索が行われず描画されない</para>
    /// </summary>
    public bool search = true;

    /// <summary>
    /// <para>検索するラベル名</para>
    /// </summary>
    public string labelName;

    /// <summary>
    /// <para>インスペクターにラベル名を表示するかどうか</para>
    /// <para>trueで表示する</para>
    /// </summary>
    public bool canPrintLabelName = false;

    /// <summary>
    /// <para>配列の時に使用する</para>
    /// <para>インスペクターを表示した時、最初から配列のフィールドを描画したい場合trueにする</para>
    /// </summary>
    public bool foldout = false;

    /// <summary>
    /// <para>検索順が降順か昇順か</para>
    /// </summary>
    public Direction direction = Direction.ASC;

    /// <summary>
    /// <para>取得する最大数</para>
    /// <para>負や0を指定しても2147483647になる
    /// </summary>
    public int limit = 2147483647;

    /// <summary>
    /// <para>検索の高速化のためTypeをキャッシュしておく</para>
    /// </summary>
    public static Dictionary<string, System.Type> assetTypes = new Dictionary<string, System.Type>();

    public LabelSearchAttribute(string labelName)
    {
        this.labelName = labelName;
    }

    public LabelSearchAttribute(string labelName, int limit)
    {
        if (Mathf.Sign(limit) == 1)
        {
            this.limit = limit;
        }

        this.labelName = labelName;
    }

    public LabelSearchAttribute(string labelName, Direction direction)
    {
        this.labelName = labelName;
        this.direction = direction;
    }

    public LabelSearchAttribute(string labelName, int limit, Direction direction)
    {
        this.labelName = labelName;

        if (Mathf.Sign(limit) == 1)
        {
            this.limit = limit;
        }

        this.direction = direction;
    }

    public enum Direction
    {
        ASC,
        DESC
    }
}


#if UNITY_EDITOR
/// <summary>
/// Label drawer.
/// </summary>
[CustomPropertyDrawer(typeof(LabelSearchAttribute))]
public class LabelSearchDrawer : PropertyDrawer
{
    /// <summary>
    /// GUIの高さ
    /// </summary>
    private const int CONTENT_HEIGHT = 16;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (labelSearchAttribute.init == false)
        {
            labelSearchAttribute.init = true;
            return;
        }

        //デバッグ時使用 ( 検索時間 )
        //double start = EditorApplication.timeSinceStartup;

        if (labelSearchAttribute.canPrintLabelName)
        {
            label.text += string.Format(" ( Label = {0} )", labelSearchAttribute.labelName);
        }
        if (property.isArray)
        {
            EditorGUI.indentLevel = 0;
            labelSearchAttribute.foldout = EditorGUI.Foldout(position, labelSearchAttribute.foldout, label);
            if (labelSearchAttribute.search)
            {
                DrawArrayProperty(position, property, label);
                //デバッグ時使用 ( 検索時間 )
                //Debug.Log (((float)EditorApplication.timeSinceStartup - start) + "ms");
            }
            else
            {
                DrawCachedArrayProperty(position, property, label);
            }
        }
        else
        {
            if (labelSearchAttribute.search)
            {
                DrawSingleProperty(position, property, label);
                ////デバッグ時使用 ( 検索時間 )
                //Debug.Log (((float)EditorApplication.timeSinceStartup - start) + "ms");
            }
            else
            {
                DrawCachedSingleProperty(position, property, label);
            }
        }

        labelSearchAttribute.search = false;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = 0;

        if (property.isArray && labelSearchAttribute.foldout)
        {
            height = (property.arraySize + 1) * CONTENT_HEIGHT;
        }

        return base.GetPropertyHeight(property, label) + height;
    }

    LabelSearchAttribute labelSearchAttribute
    {
        get { return (LabelSearchAttribute) attribute; }
    }

    /// <summary>
    /// <para>キャッシュされたプロパティを使用して描画する</para>
    /// </summary>
    /// <param name='position'>
    /// Position.
    /// </param>
    /// <param name='property'>
    /// Property.
    /// </param>
    /// <param name='label'>
    /// Label.
    /// </param>
    void DrawCachedSingleProperty(Rect position, SerializedProperty property, GUIContent label)
    {
        property.objectReferenceValue = EditorGUI.ObjectField(position, label, property.objectReferenceValue,
            GetType(property), false);
    }

    /// <summary>
    /// <para>キャッシュされた配列を使用して描画する</para>
    /// <para>負荷削減のため一定間隔で検索を行うようにした</para>
    /// </summary>
    /// <param name='position'>
    /// Position.
    /// </param>
    /// <param name='property'>
    /// Property.
    /// </param>
    /// <param name='label'>
    /// Label.
    /// </param>
    void DrawCachedArrayProperty(Rect position, SerializedProperty property, GUIContent label)
    {
        if (labelSearchAttribute.foldout)
        {
            position.y += CONTENT_HEIGHT;
            EditorGUI.indentLevel = 2;
            System.Type type = GetType(property.GetArrayElementAtIndex(0));
            EditorGUI.LabelField(position, "Size", property.arraySize.ToString());
            for (int i = 0; i < property.arraySize; i++)
            {
                position.y += CONTENT_HEIGHT;
                position.height = CONTENT_HEIGHT;
                GUIContent content =
                    EditorGUIUtility.ObjectContent(property.GetArrayElementAtIndex(i).objectReferenceValue, type);
                content.image = AssetPreview.GetMiniTypeThumbnail(type);
                // 要素1つ1つにフォーカスが当たらないためObjectFieldである必要はないのでLabelFieldで描画
                // PingObjectの機能使いたいけど...
                // EditorGUI.ObjectField (position, new GUIContent (ObjectNames.NicifyVariableName ("Element" + i)), property.GetArrayElementAtIndex (i).objectReferenceValue, type, false);
                EditorGUI.LabelField(position, new GUIContent(ObjectNames.NicifyVariableName("Element" + i)), content);
            }
        }
    }

    /// <summary>
    /// <para>アセットを検索して描画する</para>
    /// </summary>
    /// <param name='position'>
    /// Position.
    /// </param>
    /// <param name='property'>
    /// Property.
    /// </param>
    /// <param name='label'>
    /// Label.
    /// </param>
    void DrawSingleProperty(Rect position, SerializedProperty property, GUIContent label)
    {
        System.Type type = GetType(property);

        property.objectReferenceValue = null;


        foreach (string path in GetAllAssetPath())
        {
            System.Type assetType = null;
            Object asset = null;

            if (LabelSearchAttribute.assetTypes.TryGetValue(path, out assetType) == false)
            {
                asset = AssetDatabase.LoadMainAssetAtPath(path);

                if (asset == null)
                {
                    continue;
                }

                assetType = asset.GetType();
                LabelSearchAttribute.assetTypes.Add(path, assetType);
            }

            if (type != assetType)
            {
                continue;
            }

            if (asset == null)
            {
                asset = AssetDatabase.LoadMainAssetAtPath(path);

                if (asset == null)
                {
                    continue;
                }
            }
            string[] labels = AssetDatabase.GetLabels(asset);
            if (
                string.IsNullOrEmpty(
                    labels.FirstOrDefault(l => l.Equals(labelSearchAttribute.labelName))) ==
                false)
            {
                property.objectReferenceValue = asset;
                break;
            }
        }

        property.objectReferenceValue = EditorGUI.ObjectField(position, label, property.objectReferenceValue, type,
            false);
    }

    /// <summary>
    /// <para>該当するアセットを複数検索して描画する</para>
    /// </summary>
    /// <param name='position'>
    /// Position.
    /// </param>
    /// <param name='property'>
    /// Property.
    /// </param>
    /// <param name='label'>
    /// Label.
    /// </param>
    void DrawArrayProperty(Rect position, SerializedProperty property, GUIContent label)
    {
        int size = 0;

        EditorGUI.indentLevel = 2;

        if (labelSearchAttribute.foldout)
        {
            position.y += CONTENT_HEIGHT;
            EditorGUI.LabelField(position, "Size", property.arraySize.ToString());
        }

        property.arraySize = 0;
        property.InsertArrayElementAtIndex(0);
        System.Type type = GetType(property.GetArrayElementAtIndex(0));

        foreach (string path in GetAllAssetPath())
        {
            System.Type assetType = null;
            Object asset = null;

            if (LabelSearchAttribute.assetTypes.TryGetValue(path, out assetType) == false)
            {
                asset = AssetDatabase.LoadMainAssetAtPath(path);
                assetType = asset.GetType();
                LabelSearchAttribute.assetTypes.Add(path, assetType);
            }

            if (type != assetType)
            {
                continue;
            }

            if (asset == null)
            {
                asset = AssetDatabase.LoadMainAssetAtPath(path);
            }

            if (
                string.IsNullOrEmpty(
                    AssetDatabase.GetLabels(asset).FirstOrDefault(l => l.Equals(labelSearchAttribute.labelName))) ==
                false)
            {
                property.arraySize = ++size;
                property.GetArrayElementAtIndex(size - 1).objectReferenceValue = asset;

                if (labelSearchAttribute.foldout)
                {
                    position.y += CONTENT_HEIGHT;
                    position.height = CONTENT_HEIGHT;
                    GUIContent content =
                        EditorGUIUtility.ObjectContent(property.GetArrayElementAtIndex(size - 1).objectReferenceValue,
                            type);
                    content.image = AssetPreview.GetMiniTypeThumbnail(type);
                    // 要素1つ1つにフォーカスが当たらないためObjectFieldである必要はないのでLabelFieldで描画
                    // PingObjectの機能使いたいけど...
                    // EditorGUI.ObjectField (position, new GUIContent (ObjectNames.NicifyVariableName ("Element" + i)), property.GetArrayElementAtIndex (i).objectReferenceValue, type, false);
                    EditorGUI.LabelField(position,
                        new GUIContent(ObjectNames.NicifyVariableName("Element" + (size - 1))), content);
                }
            }

            if (labelSearchAttribute.limit <= property.arraySize)
            {
                break;
            }
        }
    }


    /// <summary>
    /// 全てのアセットのパスを取得
    /// </summary>
    /// <returns>
    /// The all asset path.
    /// </returns>
    string[] GetAllAssetPath()
    {
        string[] allAssetPath = AssetDatabase.GetAllAssetPaths();

        System.Array.Sort(allAssetPath);

        if (labelSearchAttribute.direction.Equals(LabelSearchAttribute.Direction.DESC))
        {
            System.Array.Reverse(allAssetPath);
        }
        return allAssetPath;
    }

    /// <summary>
    /// プロパティからTypeを取得
    /// </summary>
    /// <returns>
    /// The type.
    /// </returns>
    /// <param name='property'>
    /// Property.
    /// </param>
    System.Type GetType(SerializedProperty property)
    {
        return
            Assembly.Load("UnityEngine.dll")
                .GetType("UnityEngine." + property.type.Replace("PPtr<$", "").Replace(">", ""));
    }
}

#endif