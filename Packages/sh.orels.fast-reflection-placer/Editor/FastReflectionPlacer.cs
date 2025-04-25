using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace ORL.FRP
{
    [EditorTool("Fast Reflection Placer")]
    public class FastReflectionPlacerTool : EditorTool
    {
        GUIContent _toolbarIcon;
        public override GUIContent toolbarIcon => _toolbarIcon;

        private void OnEnable()
        {
            _toolbarIcon = EditorGUIUtility.IconContent("d_ReflectionProbeSelector");
        }

        private void OnDisable()
        {
            _toolbarIcon = null;
        }

        [Shortcut("FRP.StartProbePlacer", displayName = "Fast Reflection Placer/Start Probe Placer")]
        public static void StartProbePlacer()
        {
            ToolManager.SetActiveTool<FastReflectionPlacerTool>();
        }


        private MethodInfo _bakeReflectionMethod;
        public override void OnActivated()
        {
            SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Starting Probe Placing"), .1f);
            _bakeReflectionMethod = typeof(Lightmapping).GetMethod("BakeReflectionProbeSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
            _probesPlaced = 0;
        }

        public override void OnWillBeDeactivated()
        {
            SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Stopped Probe Placing"), .1f);
            _probesPlaced = 0;
            _isPlacing = false;
        }

        private float _holdStart = 0f;
        private int _probesPlaced = 0;
        private bool _isPlacing = false;

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView _) return;


            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                ToolManager.RestorePreviousPersistentTool();
                Event.current.Use();
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var hits = new RaycastHit[100];

            var hitCount = Physics.RaycastNonAlloc(ray, hits, Mathf.Infinity);
            {
                for (var i = 0; i < hitCount; i++)
                {
                    var hit = hits[i];
                    var go = hit.collider.gameObject;
                    if (go == null) continue;
                    // Skip any objects that are hidden in scene or have picking disabled
                    if (SceneVisibilityManager.instance.IsHidden(go) || SceneVisibilityManager.instance.IsPickingDisabled(go)) continue;
                    // safe to hit in cardinal directions

                    // Add a little pushoff to avoid wall intersections
                    var pushedPoint = hit.point + hit.normal * 0.1f;

                    if (!Physics.Raycast(pushedPoint + Vector3.up * 0.1f, Vector3.down, out var hitDown, Mathf.Infinity)) continue;
                    if (!Physics.Raycast(pushedPoint + Vector3.down * 0.1f, Vector3.up, out var hitUp, Mathf.Infinity)) continue;

                    Handles.DrawWireDisc(hitUp.point, Vector3.down, 0.25f);
                    Handles.DrawWireDisc(hitDown.point, Vector3.down, 0.25f);


                    // Got Y, can use correct offsets
                    var ySize = hitUp.point.y - hitDown.point.y;
                    var correctHit = new Vector3(pushedPoint.x, hitDown.point.y + ySize / 2f, pushedPoint.z);

                    Handles.color = Color.green;
                    Handles.DrawWireDisc(correctHit, Vector3.up, 0.25f);
                    Handles.color = Color.white;
                    Handles.Label(correctHit + Vector3.up, "Height: " + ySize);

                    if (!Physics.Raycast(correctHit + Vector3.right * 0.1f, Vector3.left, out var hitLeft, Mathf.Infinity)) continue;
                    if (!Physics.Raycast(correctHit + Vector3.left * 0.1f, Vector3.right, out var hitRight, Mathf.Infinity)) continue;

                    Handles.DrawWireDisc(hitLeft.point, Vector3.right, 0.25f);
                    Handles.DrawWireDisc(hitRight.point, Vector3.left, 0.25f);

                    // Got X - can use correct offsets
                    var xSize = hitRight.point.x - hitLeft.point.x;
                    correctHit = new Vector3(hitLeft.point.x + xSize / 2f, correctHit.y, correctHit.z);

                    Handles.color = Color.red;
                    Handles.DrawWireDisc(correctHit, Vector3.right, 0.25f);
                    Handles.color = Color.white;
                    Handles.Label(correctHit + Vector3.right, "Width: " + xSize);

                    if (!Physics.Raycast(correctHit + Vector3.back * 0.1f, Vector3.forward, out var hitForward, Mathf.Infinity)) continue;
                    if (!Physics.Raycast(correctHit + Vector3.forward * 0.1f, Vector3.back, out var hitBack, Mathf.Infinity)) continue;

                    Handles.DrawWireDisc(hitForward.point, Vector3.forward, 0.25f);
                    Handles.DrawWireDisc(hitBack.point, Vector3.back, 0.25f);

                    // Got Z - can use correct offsets
                    var zSize = hitForward.point.z - hitBack.point.z;
                    correctHit = new Vector3(correctHit.x, correctHit.y, hitBack.point.z + zSize / 2f);

                    Handles.color = Color.blue;
                    Handles.DrawWireDisc(correctHit, Vector3.forward, 0.25f);
                    Handles.color = Color.white;
                    Handles.Label(correctHit + Vector3.forward, "Length: " + zSize);

                    Handles.color = Color.grey;
                    Handles.DrawLine(hitUp.point, hitDown.point);
                    Handles.DrawLine(hitRight.point, hitLeft.point);
                    Handles.DrawLine(hitBack.point, hitForward.point);

                    Handles.color = Color.blue;
                    Handles.DrawWireCube(correctHit, new Vector3(xSize, ySize, zSize));

                    if (Event.current.control || Event.current.alt) break;
                    if (Event.current.button == 1 && Event.current.type == EventType.MouseDown)
                    {

                        ToolManager.RestorePreviousPersistentTool();
                        Event.current.Use();
                        break;
                    }
                    if (Event.current.button != 0) break;

                    if (Event.current.type == EventType.MouseDown)
                    {
                        _holdStart = Time.timeSinceLevelLoad;
                    }

                    if (Event.current.type == EventType.MouseDrag)
                    {
                        Event.current.Use();
                    }

                    if (_holdStart > 0 && (Time.timeSinceLevelLoad - _holdStart) > 0.25f)
                    {
                        if (!_isPlacing)
                        {
                            _isPlacing = true;
                            // This is probably too annoying
                            // SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Release to drop at mouse location"), .25f);
                        }
                        correctHit = new Vector3(hit.point.x, correctHit.y, hit.point.z);
                        Handles.color = Color.cyan;
                        Handles.DrawWireCube(correctHit, Vector3.one * 0.25f);
                        Handles.DrawWireDisc(correctHit, Vector3.forward, 0.5f, 2f);
                        Handles.DrawWireDisc(correctHit, Vector3.right, 0.5f, 2f);
                        Handles.DrawWireDisc(correctHit, Vector3.up, 0.5f, 2f);
                        Handles.color = Color.white;
                    }

                    if (Event.current.type == EventType.MouseUp)
                    {
                        _holdStart = -1f;
                        var group = Undo.GetCurrentGroup();
                        var newProbeGO = new GameObject("Probe (" + _probesPlaced + ")");
                        Undo.RegisterCreatedObjectUndo(newProbeGO, "Create Reflection Probe");
                        newProbeGO.transform.position = correctHit;
                        var probe = Undo.AddComponent<ReflectionProbe>(newProbeGO);
                        Undo.RecordObject(probe, "Set Reflection Probe Settings");
                        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
                        probe.size = new Vector3(xSize, ySize, zSize);
                        if (_holdStart > 0 && (Time.timeSinceLevelLoad - _holdStart) > 0.25f)
                        {
                            probe.center = newProbeGO.transform.InverseTransformPoint(new Vector3(hit.point.x, correctHit.y, hit.point.z));
                        }
                        probe.hdr = true;
                        probe.boxProjection = true;
                        Undo.CollapseUndoOperations(group);
                        _bakeReflectionMethod?.Invoke(null, new object[] { probe });

                        _probesPlaced++;
                        _isPlacing = false;
                        Event.current.Use();
                    }
                    break;
                }
            }
        }
    }
}
