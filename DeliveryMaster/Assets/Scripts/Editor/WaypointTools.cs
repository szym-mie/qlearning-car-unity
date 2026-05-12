using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WaypointTools
{
    public const string PrefLane = "WT.lane";
    public const string PrefHalf = "WT.half";
    public const string PrefAppr = "WT.appr";
    public const string PrefTol = "WT.tol";

    public static void CreateIntersection(Vector3 center, float laneOffset, float intersectionHalf, float approachDistance)
    {
        float ext = intersectionHalf + approachDistance;

        GameObject root = new GameObject(GetUniqueIntersectionName());
        Undo.RegisterCreatedObjectUndo(root, "Create Intersection");
        root.transform.position = center;

        var wpRoot = GameObject.Find("Waypoints");
        if (wpRoot != null)
            Undo.SetTransformParent(root.transform, wpRoot.transform, "Parent intersection");

        var entryLocal = new Dictionary<string, Vector3>
        {
            { "N", new Vector3(+laneOffset, 0, -ext) },
            { "E", new Vector3(-ext,        0, -laneOffset) },
            { "S", new Vector3(-laneOffset, 0, +ext) },
            { "W", new Vector3(+ext,        0, +laneOffset) },
        };
        var exitLocal = new Dictionary<string, Vector3>
        {
            { "N", new Vector3(+laneOffset, 0, +ext) },
            { "E", new Vector3(+ext,        0, -laneOffset) },
            { "S", new Vector3(-laneOffset, 0, -ext) },
            { "W", new Vector3(-ext,        0, +laneOffset) },
        };

        var wps = new Dictionary<string, Waypoint>();
        foreach (var kv in entryLocal) wps["entry" + kv.Key] = CreateWaypoint(root.transform, "entry" + kv.Key, kv.Value);
        foreach (var kv in exitLocal)  wps["exit"  + kv.Key] = CreateWaypoint(root.transform, "exit"  + kv.Key, kv.Value);

        Link(wps, "entryN", "exitN", "exitE", "exitW");
        Link(wps, "entryE", "exitE", "exitS", "exitN");
        Link(wps, "entryS", "exitS", "exitW", "exitE");
        Link(wps, "entryW", "exitW", "exitN", "exitS");

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    public static int ConnectExitsToEntries(float laneTolerance)
    {
        var all = Object.FindObjectsByType<Waypoint>(FindObjectsSortMode.None);

        var entriesByDir = new Dictionary<string, List<Waypoint>>();
        var exitsByDir = new Dictionary<string, List<Waypoint>>();
        foreach (var w in all)
        {
            string dir = ExtractDir(w.name);
            if (dir == null) continue;
            if (w.name.StartsWith("entry"))
            {
                if (!entriesByDir.ContainsKey(dir)) entriesByDir[dir] = new List<Waypoint>();
                entriesByDir[dir].Add(w);
            }
            else if (w.name.StartsWith("exit"))
            {
                if (!exitsByDir.ContainsKey(dir)) exitsByDir[dir] = new List<Waypoint>();
                exitsByDir[dir].Add(w);
            }
        }

        int linked = 0;
        foreach (var kv in exitsByDir)
        {
            string dir = kv.Key;
            if (!entriesByDir.ContainsKey(dir)) continue;
            Vector3 fwd = DirToVector(dir);

            foreach (var exit in kv.Value)
            {
                Waypoint best = null;
                float bestForward = float.MaxValue;
                foreach (var entry in entriesByDir[dir])
                {
                    Vector3 delta = entry.transform.position - exit.transform.position;
                    float forwardDist = Vector3.Dot(delta, fwd);
                    if (forwardDist <= 0.01f) continue;
                    Vector3 lateral = delta - fwd * forwardDist;
                    if (lateral.magnitude > laneTolerance) continue;
                    if (forwardDist < bestForward)
                    {
                        bestForward = forwardDist;
                        best = entry;
                    }
                }
                if (best == null) continue;
                Undo.RecordObject(exit, "Connect Exit");
                if (!exit.neighbors.Contains(best))
                {
                    exit.neighbors.Add(best);
                    linked++;
                    EditorUtility.SetDirty(exit);
                }
            }
        }
        return linked;
    }

    static Waypoint CreateWaypoint(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Waypoint");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return Undo.AddComponent<Waypoint>(go);
    }

    static void Link(Dictionary<string, Waypoint> map, string from, params string[] tos)
    {
        var src = map[from];
        Undo.RecordObject(src, "Link Waypoints");
        foreach (var t in tos)
        {
            var dst = map[t];
            if (!src.neighbors.Contains(dst)) src.neighbors.Add(dst);
        }
        EditorUtility.SetDirty(src);
    }

    static string ExtractDir(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        char c = name[name.Length - 1];
        if (c == 'N' || c == 'E' || c == 'S' || c == 'W') return c.ToString();
        return null;
    }

    static Vector3 DirToVector(string dir)
    {
        switch (dir)
        {
            case "N": return Vector3.forward;
            case "S": return Vector3.back;
            case "E": return Vector3.right;
            case "W": return Vector3.left;
        }
        return Vector3.zero;
    }

    static string GetUniqueIntersectionName()
    {
        int i = 1;
        while (GameObject.Find($"Intersection_{i}") != null) i++;
        return $"Intersection_{i}";
    }
}

public class WaypointToolsWindow : EditorWindow
{
    float laneOffset;
    float intersectionHalf;
    float approachDistance;
    float laneTolerance;

    [MenuItem("Tools/Waypoints/Window")]
    static void Open()
    {
        GetWindow<WaypointToolsWindow>("Waypoint Tools").Show();
    }

    void OnEnable()
    {
        laneOffset       = EditorPrefs.GetFloat(WaypointTools.PrefLane, 1.5f);
        intersectionHalf = EditorPrefs.GetFloat(WaypointTools.PrefHalf, 4f);
        approachDistance = EditorPrefs.GetFloat(WaypointTools.PrefAppr, 2f);
        laneTolerance    = EditorPrefs.GetFloat(WaypointTools.PrefTol,  2f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Geometria", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        laneOffset       = EditorGUILayout.FloatField(new GUIContent("Lane offset", "Odsunięcie waypointa od osi drogi (≈ pół szerokości pasa)."), laneOffset);
        intersectionHalf = EditorGUILayout.FloatField(new GUIContent("Intersection half-size", "Połowa boku prostokąta skrzyżowania."), intersectionHalf);
        approachDistance = EditorGUILayout.FloatField(new GUIContent("Approach distance", "Jak daleko za skrzyżowaniem stawiać entry/exit."), approachDistance);
        laneTolerance    = EditorGUILayout.FloatField(new GUIContent("Lane tolerance (link)", "Maks. boczne odchylenie aby exit zlinkował się z entry."), laneTolerance);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetFloat(WaypointTools.PrefLane, laneOffset);
            EditorPrefs.SetFloat(WaypointTools.PrefHalf, intersectionHalf);
            EditorPrefs.SetFloat(WaypointTools.PrefAppr, approachDistance);
            EditorPrefs.SetFloat(WaypointTools.PrefTol,  laneTolerance);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Akcje", EditorStyles.boldLabel);

        if (GUILayout.Button("Create intersection here"))
        {
            WaypointTools.CreateIntersection(GetCenter(), laneOffset, intersectionHalf, approachDistance);
        }
        EditorGUILayout.HelpBox("Pozycja: zaznaczony GameObject → środek skrzyżowania. Bez zaznaczenia: pivot Scene view.", MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("Connect exits → nearest entries"))
        {
            int n = WaypointTools.ConnectExitsToEntries(laneTolerance);
            Debug.Log($"Waypoint Tools: connected {n} exit→entry edges.");
        }
        EditorGUILayout.HelpBox("Linkuje każdy exit_X do najbliższego entry_X leżącego w przód, w obrębie lane tolerance.", MessageType.Info);
    }

    static Vector3 GetCenter()
    {
        if (Selection.activeGameObject != null) return Selection.activeGameObject.transform.position;
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) return sv.pivot;
        return Vector3.zero;
    }
}
