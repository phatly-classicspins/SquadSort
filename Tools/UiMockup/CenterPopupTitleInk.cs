using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json.Linq;
public static class CenterPopupTitleInk
{
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var ops=JArray.Parse(File.ReadAllText("docs/art/popup-content-review-20261127/optical-center-plan.json"));
        foreach(var group in ops.GroupBy(o=>(string)o["prefab"]))
        {
            var root=PrefabUtility.LoadPrefabContents(group.Key);
            var canvas=new GameObject("MeasurementCanvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(canvas,root.scene);((RectTransform)canvas.transform).sizeDelta=new Vector2(720,1280);canvas.GetComponent<Canvas>().referencePixelsPerUnit=1;root.transform.SetParent(canvas.transform,false);
            try{foreach(var op in group){var node=root.transform.Find((string)op["node"]);if(node==null)throw new Exception("Missing title");node.position+=root.transform.TransformVector(new Vector3((float)op["dx"],(float)op["dy"],0));}root.transform.SetParent(null,false);PrefabUtility.SaveAsPrefabAsset(root,group.Key);}
            finally{PrefabUtility.UnloadPrefabContents(root);UnityEngine.Object.DestroyImmediate(canvas);}
        }
        return "Optically centered "+ops.Count+" title labels";
    }
}
