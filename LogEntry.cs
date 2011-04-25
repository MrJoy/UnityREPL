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

  private string _stackTrace = null;
  public string stackTrace {
    get { return _stackTrace; }
    set {
      string tmp = value;
      if(tmp.EndsWith("\n")) {
        tmp = tmp.Substring(0, tmp.Length - 1);
      }
      if(_stackTrace != tmp) {
        _stackTrace = tmp;
      }
    }
  }

  public string condition;
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
              GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.BeginVertical();
                  foreach(LogEntry le in children)
                    le.OnGUI();
                GUILayout.EndVertical();
              GUILayout.EndHorizontal();
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
          GUILayout.Label(output, LogEntryStyles.OutputStyle);
        GUILayout.EndHorizontal();
        break;
      case LogEntryType.EvaluationError:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Label(error, LogEntryStyles.EvaluationErrorStyle);
        GUILayout.EndHorizontal();
        break;
      case LogEntryType.SystemConsole:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.Label(error, LogEntryStyles.SystemConsoleStyle);
        GUILayout.EndHorizontal();
        break;
      case LogEntryType.ConsoleLog:
        GUILayout.BeginHorizontal(GUI.skin.box);
          GUILayout.BeginVertical();
            GUIStyle logStyle = null;
            switch(consoleLogType) {
              case LogType.Error: // Debug.LogError(...).
              case LogType.Assert: // Unity internal error.
              case LogType.Exception: // Uncaught exception.
                logStyle = LogEntryStyles.ConsoleLogErrorStyle;
                break;
              case LogType.Warning: // Debug.LogWarning(...).
                logStyle = LogEntryStyles.ConsoleLogWarningStyle;
                break;
              case LogType.Log: // Debug.Log(...).
                logStyle = LogEntryStyles.ConsoleLogNormalStyle;
                break;
            }
            GUILayout.Label(condition, logStyle);
            if(!String.IsNullOrEmpty(stackTrace))
              GUILayout.Label(stackTrace, LogEntryStyles.ConsoleLogStackTraceStyle);
          GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        break;
    }
  }
}
