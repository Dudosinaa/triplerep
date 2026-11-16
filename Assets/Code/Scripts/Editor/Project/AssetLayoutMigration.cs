using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit menu operation. No automatic execution on import or domain reload.
public static class AssetLayoutMigration
{
    public static string Backup => Path.GetFullPath("../_merge-backups/assets-layout-2026-11-15");
    [Serializable] public class Move { public string from, to; }
    [Serializable] public class Entry { public string from,to,guid,sha256,metaSha256; public bool directory; }
    [Serializable] public class Plan { public Move[] operations; public Entry[] entries; public string[] createFolders; }
    [Serializable] public class SceneState { public string path; public bool dirty,active; }
    [Serializable] public class Scenes { public SceneState[] scenes; }
    private static string Hash(string path) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant(); }
    public static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));
        string guid=AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
        if(string.IsNullOrEmpty(guid))throw new IOException("Cannot create "+path);
    }
    [MenuItem("Tools/Project/Apply Assets layout")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning)throw new InvalidOperationException("Stop Play Mode or wait for light baking before migration");
        if(File.Exists(Path.Combine(Backup,"moves-complete.txt")))throw new InvalidOperationException("Migration already completed; inspect journal before attempting again");
        var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Backup,"plan.json")));
        foreach(var e in plan.entries)
        {
            if(AssetDatabase.AssetPathToGUID(e.from)!=e.guid)throw new InvalidOperationException("GUID changed since snapshot: "+e.from);
            if(!e.directory&&Hash(e.from)!=e.sha256)throw new InvalidOperationException("File changed since snapshot: "+e.from);
            if(e.metaSha256!=null&&Hash(e.from+".meta")!=e.metaSha256)throw new InvalidOperationException("Importer changed since snapshot: "+e.from);
        }
        var states=new List<SceneState>();
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);
            states.Add(new SceneState{path=scene.path,dirty=scene.isDirty,active=scene==SceneManager.GetActiveScene()});
            if(scene.isDirty&&!EditorSceneManager.SaveScene(scene,Path.Combine(Backup,"unsaved-scene-"+i+".unity"),true))throw new IOException("Cannot back up dirty scene "+scene.path);
        }
        File.WriteAllText(Path.Combine(Backup,"open-scenes.json"),JsonUtility.ToJson(new Scenes{scenes=states.ToArray()},true));
        var beforeScenes=EditorBuildSettings.scenes.Select(s=>new EditorBuildSettingsScene(s.path,s.enabled)).ToArray();
        EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
        bool completed=false;
        try
        {
            foreach(var operation in plan.operations)
            {
                EnsureFolder(Path.GetDirectoryName(operation.to).Replace('\\','/'));
                string error=AssetDatabase.MoveAsset(operation.from,operation.to);
                if(!string.IsNullOrEmpty(error))throw new IOException(operation.from+" -> "+operation.to+": "+error);
                File.AppendAllText(Path.Combine(Backup,"move-journal.tsv"),operation.from+"\t"+operation.to+Environment.NewLine);
            }
            foreach(string folder in plan.createFolders)EnsureFolder(folder);
            RewritePaths(plan);
            EditorBuildSettings.scenes=beforeScenes.Select(s=>new EditorBuildSettingsScene(Map(s.path,plan),s.enabled)).ToArray();
            completed=true;
        }
        catch(Exception e) {File.WriteAllText(Path.Combine(Backup,"migration-error.txt"),e.ToString());throw;}
        finally
        {
            AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
        }
        if(completed)File.WriteAllText(Path.Combine(Backup,"moves-complete.txt"),DateTime.UtcNow.ToString("O"));
        AssetDatabase.Refresh();Debug.Log("ASSETS_LAYOUT_MOVE_PASS");
    }
    public static string Map(string path,Plan plan)
    {
        foreach(var e in plan.entries.OrderByDescending(e=>e.from.Length))
            if(path==e.from||path.StartsWith(e.from+"/",StringComparison.Ordinal))return e.to+path.Substring(e.from.Length);
        return path;
    }
    [MenuItem("Tools/Project/Normalize scene scripts folder casing")]
    public static void NormalizeSceneFolder()
    {
        const string expected="Assets/Code/Scripts/Scenes",temporary="Assets/Code/Scripts/Scenes_CaseFix";
        string actual=AssetDatabase.GUIDToAssetPath(AssetDatabase.AssetPathToGUID(expected));
        if(actual==expected)return;
        if(string.IsNullOrEmpty(actual)||!actual.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Unexpected scene script folder "+actual);
        foreach(var move in new[]{new Move{from=actual,to=temporary},new Move{from=temporary,to=expected}})
        {
            string error=AssetDatabase.MoveAsset(move.from,move.to);if(!string.IsNullOrEmpty(error))throw new IOException(error);
            File.AppendAllText(Path.Combine(Backup,"move-journal.tsv"),move.from+"\t"+move.to+Environment.NewLine);
        }
        AssetDatabase.Refresh();
    }
    private static void RewritePaths(Plan plan)
    {
        var replacements=plan.entries.Where(e=>e.from!=e.to).OrderByDescending(e=>e.from.Length).ToArray();
        var files=Directory.GetFiles("Assets","*",SearchOption.AllDirectories)
            .Where(p=>new[]{".cs",".md",".json",".wlt"}.Contains(Path.GetExtension(p))).ToList();
        files.AddRange(Directory.GetFiles("ProjectSettings","*.asset"));
        files.AddRange(Directory.GetFiles("Tests","*.ps1"));
        files.AddRange(Directory.GetFiles(".","*.md"));
        foreach(var file in files)
        {
            string old=File.ReadAllText(file),text=old;
            foreach(var e in replacements)
            {
                text=text.Replace(e.from,e.to).Replace(e.from.Replace('/','\\'),e.to.Replace('/','\\'));
            }
            text=text.Replace("Docs/TutorialInfo/Layout.wlt","Docs/Docs/TutorialInfo/Layout.wlt");
            if(file.Replace('\\','/').EndsWith("/HandsReloadBaker.cs",StringComparison.Ordinal))
            {
                text=text.Replace("public const string Model = Folder+\"/HandsReload.prefab\";","public const string Model = \"Assets/Level/Prefabs/Weapons/HandsReload.prefab\";");
                text=text.Replace("string path=Folder+\"/HandsPistolReloadMesh.asset\";","string path=\"Assets/Art/Models/Weapons/Hands/Derived/HandsPistolReloadMesh.asset\";");
            }
            if(file.Replace('\\','/').EndsWith("/HomeBuilder.cs",StringComparison.Ordinal))
            {
                text=text.Replace("PrefabPath=Folder+\"/Home.prefab\"","PrefabPath=\"Assets/Level/Prefabs/Environment/Home.prefab\"");
                text=text.Replace("Folder+\"/Materials/\"","\"Assets/Art/Materials/Home/\"");
                text=text.Replace("Directory.CreateDirectory(Folder+\"/Materials\")","Directory.CreateDirectory(\"Assets/Art/Materials/Home\")");
            }
            // These tools create their material folders when invoked, including on a clean checkout.
            if(file.Replace('\\','/').EndsWith("/SetupItemInspection.cs",StringComparison.Ordinal))
                text=text.Replace("AssetDatabase.CreateFolder(\"Assets\", \"Materials\")","AssetLayoutMigration.EnsureFolder(\"Assets/Art/Materials/Gameplay\")");
            if(file.Replace('\\','/').EndsWith("/SetupNpcDialogue.cs",StringComparison.Ordinal))
                text=text.Replace("AssetDatabase.CreateFolder(\"Assets\", \"Materials\")","AssetLayoutMigration.EnsureFolder(folder)");
            if(text!=old)
            {
                File.WriteAllText(file,text,new UTF8Encoding(false));
                File.AppendAllText(Path.Combine(Backup,"rewritten-files.txt"),file+Environment.NewLine);
            }
        }
    }
}

