//-----------------------------------------------------------------
//  LogEntry
//  Copyright 2009-2012 MrJoy, Inc.
//  All rights reserved
//
//-----------------------------------------------------------------
// Models for the execution/result log, and editor history.
//-----------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

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
        _filteredStackTrace = null;
        _stackTrace = tmp;
      }
    }
  }

  private static char[] NEWLINE = new char[] { '\n' },
                        COLON = new char[] { ':' };

  public string _filteredStackTrace = null;
  public string filteredStackTrace {
    get {
      if(_filteredStackTrace == null) {
        if(_stackTrace != null) {
          string[] traceEntries = _stackTrace.Split(NEWLINE);
          List<string> filteredTraceEntries = new List<string>();
          foreach(string entry in traceEntries) {
            bool filter = false;

            int i = entry.IndexOf(") (");
            string signature, position;
            if(i > 0) {
              signature = entry.Substring(0, i + 1);
              position = entry.Substring(i + 2, entry.Length - (i + 2));
            } else {
              // Nada.
              signature = entry;
              position = "";
            }

            string[] signaturePieces = signature.Split(COLON, 2);
            if(signaturePieces.Length > 1) {
              string classDesignation = signaturePieces[0];
              string methodSignature = signaturePieces[1];

              if((classDesignation == "UnityEngine.Debug" && position == "") ||
                 (classDesignation.StartsWith("Class") && methodSignature == "Host(Object&)" && position == "") ||
                 (classDesignation == "Mono.CSharp.Evaluator" && methodSignature == "Evaluate(String, Object&, Boolean&)" && position == "") ||
                 (classDesignation == "EvaluationHelper" && methodSignature == "Eval(List`1, String)" && position.IndexOf("UnityREPL/Evaluator.cs") >= 0) ||
                 (classDesignation == "Shell" && methodSignature == "Update()" && position.IndexOf("UnityREPL/Shell.cs") >= 0) ||
                 (classDesignation == "UnityEditor.EditorApplication" && methodSignature == "Internal_CallUpdateFunctions()" && position == "")
                 )
                filter = true;
            } else {
              // WTF?!
            }

            if(!filter)
              filteredTraceEntries.Add(entry);
          }
          StringBuilder sb = new StringBuilder();
          foreach(string s in filteredTraceEntries)
            sb.Append(s).Append("\n");
          string tmp = sb.ToString();
          if(tmp.Length > 0)
            tmp = tmp.Substring(0, tmp.Length - 1);
          _filteredStackTrace = tmp;
        } else {
          _filteredStackTrace = null;
        }
      }
      return _filteredStackTrace;
    }
  }
  public string condition;
  public LogType consoleLogType;

  public void Add(LogEntry child) {
    if(children == null)
      children = new List<LogEntry>();
    children.Add(child);
  }

  public bool OnGUI(bool filterTraces) {
    bool retVal = false;
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
            GUILayout.BeginHorizontal();
              isExpanded = GUILayout.Toggle(isExpanded, (isExpanded) ? command: shortCommand, LogEntryStyles.FoldoutCommandStyle, GUILayout.ExpandWidth(false));
              GUILayout.FlexibleSpace();
              retVal = GUILayout.Button("+", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            if(isExpanded && hasChildren) {
              GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.BeginVertical();
                  foreach(LogEntry le in children)
                    le.OnGUI(filterTraces);
                GUILayout.EndVertical();
              GUILayout.EndHorizontal();
            }
          } else {
            GUILayout.BeginHorizontal();
              GUILayout.Space(15);
              GUILayout.Label(command, LogEntryStyles.DefaultCommandStyle);
              GUILayout.FlexibleSpace();
              retVal = GUILayout.Button("+", GUILayout.ExpandWidth(false));
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
            if(!String.IsNullOrEmpty(filterTraces ? filteredStackTrace : stackTrace))
              GUILayout.Label(filterTraces ? filteredStackTrace : stackTrace, LogEntryStyles.ConsoleLogStackTraceStyle);
          GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        break;
    }
    return retVal;
  }
}
