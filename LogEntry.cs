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
  public string shortCommand = null;
  public bool isExpanded = true;
  public bool hasChildren = false;
  public bool isExpandable = false;
  public List<LogEntry> children;

  private char[] newline = new char[] {'\n'};

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
    switch(logEntryType) {
      case LogEntryType.Command:
          if(children != null && children.Count > 0) {
              hasChildren = true;
          }
          if(shortCommand == null) {
            command = command.TrimEnd();
            string[] commandList = command.Split(newline, 2);
            shortCommand = commandList[0];
            if(hasChildren || command != shortCommand) {
              isExpandable = true;
            }
          }
          if(isExpandable) {
            isExpanded = GUILayout.Toggle(isExpanded, (isExpanded) ? command: shortCommand, LogEntryStyles.FoldoutCommandStyle, GUILayout.ExpandWidth(false));
            if(isExpanded && hasChildren) {
              foreach(LogEntry le in children)
                le.OnGUI();
            }
          } else {
            GUILayout.BeginHorizontal();
              GUILayout.Space(15);
              GUILayout.Label(command, LogEntryStyles.DefaultCommandStyle);
            GUILayout.EndHorizontal();
          }
        break;
      case LogEntryType.Output:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Space(14);
          GUILayout.Label(output, LogEntryStyles.OutputStyle);
        GUILayout.EndHorizontal();
        break;
      case LogEntryType.EvaluationError:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Space(14);
          GUILayout.Label(error, LogEntryStyles.EvaluationErrorStyle);
        GUILayout.EndHorizontal();
        break;
      case LogEntryType.SystemConsole:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Space(14);
          GUILayout.Label(error, LogEntryStyles.SystemConsoleStyle);
        GUILayout.EndHorizontal();
        break;
      case LogEntryType.ConsoleLog:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Space(14);
          GUILayout.BeginVertical();
            GUILayout.Label(condition, LogEntryStyles.ConsoleLogConditionStyle);
            GUILayout.Label(stackTrace, LogEntryStyles.ConsoleLogStackTraceStyle);
          GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        break;
    }
  }
}
