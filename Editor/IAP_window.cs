using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using System;

public class IAP_window : EditorWindow
{
    string pathDeffault
    {
        get
        {
            if (folder)
            {
                string path = AssetDatabase.GetAssetPath(folder);
                if (!path.EndsWith("/"))
                    path += "/";
                path += pathDeffaultResources;
                return path;
            }
            else
            {
                return "Assets/" + pathDeffaultResources;
            }
        }
    }
    string pathDeffaultResources = "Resources/IAP/Data/";

    UnityEngine.Object folder;

    [MenuItem("Tools/IAP_creator")]
    static void Init()
    {
        IAP_window window = (IAP_window)EditorWindow.GetWindow(typeof(IAP_window));
        window.Show();
    }

    List<Data> datas = new List<Data>();
    class Data
    {
        public Iap_data iapData;
        public Editor editor;

        public Data(Iap_data data)
        {
            if (!data)
                return;
            iapData = data;
            editor = Editor.CreateEditor(data);
        }
    }

    void AddData(Iap_data iapData)
    {
        if (!iapData)
            return;
        if (!datas.Any(item => item.iapData == iapData))
            datas.Add(new Data(iapData));
    }

    void OnEnable()
    {
       // Search();
       //if (uniquePaths.Count == 0)
       //    uniquePaths.Add(pathDeffault);
    }

    void OnGUI()
    {
        //  if (GUILayout.Button("Focus to path"))
        //      FocusToSaveDirectory(); 
       uniquePaths = Search(typeof(Iap_data), true);
        
        if (uniquePaths.Count > 1)
        {
            GUI.color = Color.red;
            EditorGUILayout.LabelField($"You must store ScriptableObjects in one folder! Current folders: {uniquePaths.Count}");
            GUI.color = Color.white;
        }else if(uniquePaths.Count == 0)
            folder = EditorGUILayout.ObjectField("Folder to crate:", folder, typeof(UnityEngine.Object), true);

        if (uniquePaths.Count == 0)
            EditorGUILayout.LabelField((!folder ? "No folder selected. Deffault create path is: " : "Create path is: ") + pathDeffault);

        for (int i = 0; i < uniquePaths.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"path[{i}]: " + uniquePaths[i]);
            if (GUILayout.Button("Focus", GUILayout.Width(85)))
                FocusToDirectory(uniquePaths[i]);
            EditorGUILayout.EndHorizontal();
        }

        GUI.enabled = uniquePaths.Count <= 1;

        if (GUILayout.Button("Create new asset"))
            CreateMyAsset(uniquePaths.Count == 0 ? pathDeffault : uniquePaths[0]);

        GUI.enabled = true;

        if (GUILayout.Button("Show Package"))
        {
            List<string> list = Search(typeof(IAP_window), false);

            Debug.Log("list: " + list.Count);
            if (list.Count > 0)
                ShowExplorer(list[0], true);
        }
           
    }


    

    //[MenuItem("Assets/Create/My Scriptable Object")]
    void CreateMyAsset(string dir_path)
    {
        string path = dir_path;

        Iap_data asset = ScriptableObject.CreateInstance<Iap_data>();

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        if (!path.EndsWith("/"))
            path += "/";

        AssetDatabase.CreateAsset(asset,$"{path}Iap_data{asset.GetInstanceID()}.asset");
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();

        Selection.activeObject = asset;
    }
    void FocusToDirectory(string dir_path)
    {
        string path = dir_path;

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
     
        EditorUtility.FocusProjectWindow();

       if (path.EndsWith("/"))
           path = path.Remove(path.Length - 1);
     
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
    }

    public void ShowExplorer(string itemPath, bool isFolder)
    {
        itemPath = itemPath.Replace(@"/", @"\");   // explorer doesn't like front slashes
        System.Diagnostics.Process.Start("explorer.exe", "/select," + itemPath);
    }

    List<string> uniquePaths = new List<string>();

    List<string> Search(Type type, bool isFilterScriptableObject)
    {
        string[] guids = AssetDatabase.FindAssets(type.Name);  //("t:Iap_data", new[] { "Assets/" });

       // if (isOnlyDebug)
       // {
       //     for (int i = 0; i < guids.Length; i++)
       //         Debug.Log($"guids[{i}]: {AssetDatabase.GUIDToAssetPath(guids[i])}");
       //     return;
       // }

        List<string> uniquePaths = new List<string>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (isFilterScriptableObject && Path.GetExtension(path) != ".asset")
                continue;

            if (!uniquePaths.Contains(Path.GetDirectoryName(path)))
                uniquePaths.Add(Path.GetDirectoryName(path));
        }

        return uniquePaths;
    }
}
