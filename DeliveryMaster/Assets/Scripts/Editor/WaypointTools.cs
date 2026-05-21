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

    public static int ConnectPair(Waypoint src, Waypoint dst, bool twoWay)
    {
        if (src == null || dst == null || src == dst) return 0;
        int added = 0;
        if (AddNeighbor(src, dst)) added++;
        if (twoWay && AddNeighbor(dst, src)) added++;
        return added;
    }

    public static int RenameDuplicateIntersections()
    {
        var all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        var intersections = new List<GameObject>();
        foreach (var go in all)
        {
            if (go.name.StartsWith("Intersection_")) intersections.Add(go);
        }

        var existing = new HashSet<string>();
        foreach (var go in intersections) existing.Add(go.name);

        int nextId = 1;
        string NextFreeName()
        {
            while (existing.Contains($"Intersection_{nextId}")) nextId++;
            string n = $"Intersection_{nextId}";
            existing.Add(n);
            nextId++;
            return n;
        }

        var seen = new HashSet<string>();
        int renamed = 0;
        foreach (var go in intersections)
        {
            if (seen.Add(go.name)) continue;
            string newName = NextFreeName();
            Undo.RecordObject(go, "Rename Duplicate Intersection");
            go.name = newName;
            seen.Add(newName);
            EditorUtility.SetDirty(go);
            renamed++;
        }
        return renamed;
    }

    public enum CleanupIssue { EmptySlot, ExitToOwnEntry, UTurn, Reversed }

    public struct BadEdge
    {
        public Waypoint owner;
        public int neighborIndex;
        public Waypoint neighbor;
        public CleanupIssue reason;
    }

    public static List<BadEdge> ScanIssues()
    {
        var all = Object.FindObjectsByType<Waypoint>(FindObjectsSortMode.None);

        var byParent = new Dictionary<Transform, List<Waypoint>>();
        foreach (var w in all)
        {
            var p = w.transform.parent;
            if (p == null) continue;
            if (!byParent.ContainsKey(p)) byParent[p] = new List<Waypoint>();
            byParent[p].Add(w);
        }

        var centers = new Dictionary<Transform, Vector3>();
        foreach (var kv in byParent)
        {
            Vector3 sum = Vector3.zero;
            foreach (var w in kv.Value) sum += w.transform.position;
            centers[kv.Key] = sum / kv.Value.Count;
        }

        Vector3 OutwardOf(Waypoint w)
        {
            var p = w.transform.parent;
            if (p == null || !centers.ContainsKey(p)) return Vector3.zero;
            var d = w.transform.position - centers[p];
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
        }

        var bad = new List<BadEdge>();
        foreach (var w in all)
        {
            if (w.neighbors == null) continue;
            for (int i = 0; i < w.neighbors.Count; i++)
            {
                var nb = w.neighbors[i];
                if (nb == null)
                {
                    bad.Add(new BadEdge { owner = w, neighborIndex = i, neighbor = null, reason = CleanupIssue.EmptySlot });
                    continue;
                }

                bool wIsExit = w.name.StartsWith("exit");
                bool wIsEntry = w.name.StartsWith("entry");
                bool nbIsExit = nb.name.StartsWith("exit");
                bool nbIsEntry = nb.name.StartsWith("entry");
                bool sameParent = w.transform.parent != null && w.transform.parent == nb.transform.parent;

                Vector3 wOut = OutwardOf(w);
                Vector3 nbOut = OutwardOf(nb);

                if (wIsExit && nbIsEntry && sameParent)
                {
                    bad.Add(new BadEdge { owner = w, neighborIndex = i, neighbor = nb, reason = CleanupIssue.ExitToOwnEntry });
                }
                else if (wIsEntry && nbIsExit && sameParent)
                {
                    if (Vector3.Dot(wOut, nbOut) < -0.85f)
                        bad.Add(new BadEdge { owner = w, neighborIndex = i, neighbor = nb, reason = CleanupIssue.UTurn });
                }
                else if (wIsExit && nbIsEntry && !sameParent)
                {
                    Vector3 travelOut = wOut;
                    Vector3 travelIn = -nbOut;
                    float align = Vector3.Dot(travelOut, travelIn);

                    Vector3 delta = nb.transform.position - w.transform.position;
                    float geomFwd = delta.sqrMagnitude > 1e-6f ? Vector3.Dot(delta.normalized, travelOut) : 0f;

                    if (align < 0.5f || geomFwd < 0.3f)
                        bad.Add(new BadEdge { owner = w, neighborIndex = i, neighbor = nb, reason = CleanupIssue.Reversed });
                }
            }
        }
        return bad;
    }

    public static int ApplyCleanup(List<BadEdge> bad)
    {
        var byOwner = new Dictionary<Waypoint, List<int>>();
        foreach (var b in bad)
        {
            if (!byOwner.ContainsKey(b.owner)) byOwner[b.owner] = new List<int>();
            byOwner[b.owner].Add(b.neighborIndex);
        }

        int removed = 0;
        foreach (var kv in byOwner)
        {
            var w = kv.Key;
            var indices = kv.Value;
            indices.Sort((a, b) => b.CompareTo(a));
            Undo.RecordObject(w, "Cleanup Waypoint Neighbors");
            foreach (var idx in indices)
            {
                if (idx >= 0 && idx < w.neighbors.Count)
                {
                    w.neighbors.RemoveAt(idx);
                    removed++;
                }
            }
            EditorUtility.SetDirty(w);
        }
        return removed;
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
                    if (entry.transform.parent == exit.transform.parent) continue;
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
                if (!exit.neighbors.Contains(best))
                {
                    Undo.RecordObject(exit, "Connect Exit");
                    exit.neighbors.Add(best);
                    linked++;
                    EditorUtility.SetDirty(exit);
                }
            }
        }
        return linked;
    }

    static bool AddNeighbor(Waypoint src, Waypoint dst)
    {
        if (src.neighbors.Contains(dst)) return false;
        Undo.RecordObject(src, "Connect Waypoints");
        src.neighbors.Add(dst);
        EditorUtility.SetDirty(src);
        return true;
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
    bool swapDirection;
    List<WaypointTools.BadEdge> lastScan;
    bool cleanupEmpty = true;
    bool cleanupExitToOwnEntry = true;
    bool cleanupUTurn = true;
    bool cleanupReversed = true;

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

    void OnSelectionChange()
    {
        Repaint();
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Połącz 2 nody (z podglądem kierunku)", EditorStyles.boldLabel);

        var selected = GetSelectedWaypoints();

        Waypoint src = null, dst = null;
        if (selected.Length == 2)
        {
            var active = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Waypoint>() : null;
            if (active != null && (selected[0] == active || selected[1] == active))
            {
                dst = active;
                src = selected[0] == active ? selected[1] : selected[0];
            }
            else
            {
                src = selected[0];
                dst = selected[1];
            }
            if (swapDirection) (src, dst) = (dst, src);
        }

        if (src != null && dst != null)
        {
            EditorGUILayout.LabelField($"Kierunek: {src.name}  →  {dst.name}");
        }
        else
        {
            EditorGUILayout.LabelField($"Zaznacz dokładnie 2 waypointy (teraz: {selected.Length}).");
        }

        using (new EditorGUI.DisabledScope(src == null || dst == null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Connect (one-way)"))
                {
                    int n = WaypointTools.ConnectPair(src, dst, false);
                    Debug.Log(n > 0
                        ? $"Waypoint Tools: connected {src.name} → {dst.name}."
                        : $"Waypoint Tools: link {src.name} → {dst.name} already exists.");
                }
                if (GUILayout.Button("Connect (two-way)"))
                {
                    int n = WaypointTools.ConnectPair(src, dst, true);
                    Debug.Log($"Waypoint Tools: added {n} link(s) between {src.name} and {dst.name}.");
                }
                if (GUILayout.Button("Swap ↔"))
                {
                    swapDirection = !swapDirection;
                }
            }
        }
        EditorGUILayout.HelpBox("Zaznacz 2 waypointy. Ostatnio kliknięty = cel. Sprawdź kierunek wyżej, w razie czego kliknij Swap.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Renumeracja duplikatów", EditorStyles.boldLabel);
        if (GUILayout.Button("Rename duplicate Intersection_X to unique names"))
        {
            int n = WaypointTools.RenameDuplicateIntersections();
            Debug.Log($"Waypoint Tools: renamed {n} duplicate intersections.");
        }
        EditorGUILayout.HelpBox("Skanuje wszystkie GameObjecty 'Intersection_*'. Pierwsza kopia każdej nazwy zostaje, kolejne dostają nowy najniższy wolny numer (Intersection_22, _23...). Tylko nazwy — referencje (połączenia waypointów) nie ruszone.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cleanup (usuwanie złych krawędzi)", EditorStyles.boldLabel);

        cleanupEmpty            = EditorGUILayout.ToggleLeft("Empty slots (puste sloty Neighbors)",       cleanupEmpty);
        cleanupExitToOwnEntry   = EditorGUILayout.ToggleLeft("Exit → entry tej samej krzyżówki",         cleanupExitToOwnEntry);
        cleanupUTurn            = EditorGUILayout.ToggleLeft("U-turny (entry → exit po przeciwnej osi)", cleanupUTurn);
        cleanupReversed         = EditorGUILayout.ToggleLeft("Reversed / direction mismatch",            cleanupReversed);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan"))
            {
                lastScan = WaypointTools.ScanIssues();
                int e = 0, oe = 0, u = 0, r = 0;
                foreach (var b in lastScan)
                {
                    switch (b.reason)
                    {
                        case WaypointTools.CleanupIssue.EmptySlot: e++; break;
                        case WaypointTools.CleanupIssue.ExitToOwnEntry: oe++; break;
                        case WaypointTools.CleanupIssue.UTurn: u++; break;
                        case WaypointTools.CleanupIssue.Reversed: r++; break;
                    }
                }
                Debug.Log($"Cleanup scan: empty={e}, exit→ownEntry={oe}, uTurn={u}, reversed={r} (total {lastScan.Count})");
            }
            using (new EditorGUI.DisabledScope(lastScan == null))
            {
                if (GUILayout.Button("Apply (selected categories)"))
                {
                    var filtered = new List<WaypointTools.BadEdge>();
                    foreach (var b in lastScan)
                    {
                        bool keep =
                            (b.reason == WaypointTools.CleanupIssue.EmptySlot       && cleanupEmpty) ||
                            (b.reason == WaypointTools.CleanupIssue.ExitToOwnEntry  && cleanupExitToOwnEntry) ||
                            (b.reason == WaypointTools.CleanupIssue.UTurn           && cleanupUTurn) ||
                            (b.reason == WaypointTools.CleanupIssue.Reversed        && cleanupReversed);
                        if (keep) filtered.Add(b);
                    }
                    int removed = WaypointTools.ApplyCleanup(filtered);
                    Debug.Log($"Cleanup applied: removed {removed} bad edges.");
                    lastScan = null;
                }
            }
        }

        if (lastScan != null)
        {
            EditorGUILayout.LabelField($"Ostatni scan: {lastScan.Count} krawędzi do usunięcia (wg powyższych checkboxów Apply usunie wybrane).");
        }
        EditorGUILayout.HelpBox("1) Scan — przeskanuje scenę i policzy złe krawędzie do konsoli. 2) Odznacz kategorie których NIE chcesz tknąć. 3) Apply — usuwa, z Undo.", MessageType.Info);
    }

    static Waypoint[] GetSelectedWaypoints()
    {
        var gos = Selection.gameObjects;
        var list = new List<Waypoint>(gos.Length);
        foreach (var go in gos)
        {
            var w = go.GetComponent<Waypoint>();
            if (w != null) list.Add(w);
        }
        return list.ToArray();
    }

    static Vector3 GetCenter()
    {
        if (Selection.activeGameObject != null) return Selection.activeGameObject.transform.position;
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) return sv.pivot;
        return Vector3.zero;
    }
}
