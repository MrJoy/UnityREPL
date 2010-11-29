using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public enum LogEntryType : int {
  Command = 0,
  Output = 1,
  EvaluationError = 2,
  SystemConsole = 3,

  ConsoleLog = 10
}

[Serializable]
public class LogEntry {
  public LogEntryType logEntryType;

  public string command;
  public bool isExpanded = true;
  public List<LogEntry> children;

  public string output;

  public string error;

  public string condition, stackTrace;
  public LogType consoleLogType;

  public void Add(LogEntry child) {
    if(children == null)
      children = new List<LogEntry>();
    children.Add(child);
  }

  public void OnGUI() {
    Color c = GUI.contentColor;
    switch(logEntryType) {
      case LogEntryType.Command:
        GUI.contentColor = Color.blue;
          if(children != null && children.Count > 0) {
            isExpanded = GUILayout.Toggle(isExpanded, command, EditorStyles.foldout, GUILayout.ExpandWidth(false));
          } else {
            GUILayout.BeginHorizontal();
              GUILayout.Space(15);
              GUILayout.Label(command);
            GUILayout.EndHorizontal();
          }
          if(isExpanded) {
            foreach(LogEntry le in children)
              le.OnGUI();
          }
        break;
      case LogEntryType.Output:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Space(14);
          GUI.contentColor = Color.green;
          GUILayout.Label(output);
        GUILayout.EndHorizontal();
        break;
      case LogEntryType.EvaluationError:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Space(14);
          GUI.contentColor = Color.red;
          GUILayout.Label(error);
        GUILayout.EndHorizontal();
        break;
      case LogEntryType.SystemConsole:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Space(14);
          GUI.contentColor = Color.yellow;
          GUILayout.Label(error);
        GUILayout.EndHorizontal();
        break;
      case LogEntryType.ConsoleLog:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Space(14);
          GUILayout.BeginVertical();
            GUI.contentColor = c;
            GUILayout.Label(condition);
            GUI.contentColor = Color.red;
            GUILayout.Label(stackTrace);
          GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        break;
    }
    GUI.contentColor = c;
  }
}
