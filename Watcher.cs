//-----------------------------------------------------------------
//  Watcher v0.11 (2010-03-08)
//  Copyright 2009-2010 MrJoy, Inc.
//  All rights reserved
//
//  2010-03-08 - jdf - Remove unneeded 'using' statements.
//  2010-02-16 - jdf - Initial version.
//
//-----------------------------------------------------------------
// Handy viewer to keep track of using/vars/etc.
//
// TODO: Make sure this plays nice with assembly reloads (see Shell.cs).
// TODO: Find the serializer used for the 'csharp' program to dump out objects 
//       gracefully.
// TODO: Find a way to preserve vars across assembly reloads?
// TODO: Live value manipulator?  (Pie in the sky...)
//-----------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using Mono.CSharp;

public class Watcher : EditorWindow {
#if !UNITY_IPHONE
  [MenuItem("Window/REPL/Watcher")]
  static void Init() {
    Watcher window = (Watcher)EditorWindow.GetWindow(typeof(Watcher));
    window.Show();
  }

  void OnInspectorUpdate() {
    Repaint();
  }

  Vector2 scrollPosition = new Vector2(0,0);
  void OnGUI() {
    EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.Label(Evaluator.GetVars(), EditorStyles.wordWrappedLabel);
      EditorGUILayout.EndScrollView();
    EditorGUILayout.EndVertical();
  }
#endif
}