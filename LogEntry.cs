// //-----------------------------------------------------------------
// //  LogEntry
// //  Copyright 2009-2014 Jon Frisby
// //  All rights reserved
// //
// //-----------------------------------------------------------------
// // Models for the execution/result log, and editor history.
// //-----------------------------------------------------------------
// using UnityEditor;
// using UnityEngine;
// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Text;

// [Serializable]
// public enum LogEntryType : int {
//   Output            = 0,
//   EvaluationError   = 1,
//   SystemConsoleOut  = 2,
//   SystemConsoleErr  = 3
// }

// [Serializable]
// public class LogEntry {
//   public LogEntryType logEntryType;

//   public string message,
//                 stackTrace;

//   public bool isExpanded = true;

//   protected static char[] NEWLINE = new char[] { '\n' },
//                           COLON   = new char[] { ':' };


//   [System.NonSerialized]
//   private string _shortMessage = null;
//   public string DisplayMessage {
//     get {
//       if(_shortMessage == null) _shortMessage  = (message.Split(NEWLINE, 2))[0];
//       return isExpanded ? message : _shortMessage;
//     }
//   }

//   public bool HasLongMessage { get { return _shortMessage != message; } }

//   [System.NonSerialized]
//   private string _filteredStackTrace = null;
//   public string filteredStackTrace {
//     get {
//       if(stackTrace == null) return null;
//       if(_filteredStackTrace != null) return _filteredStackTrace;

//       string[] traceEntries = stackTrace.Split(NEWLINE);
//       List<string> filteredTraceEntries = new List<string>();
//       foreach(string entry in traceEntries) {
//         bool filter = false;

//         int i = entry.IndexOf(") (", StringComparison.Ordinal);
//         string signature, position;
//         if(i > 0) {
//           signature = entry.Substring(0, i + 1);
//           position = entry.Substring(i + 2, entry.Length - (i + 2));
//         } else {
//           // Nada.
//           signature = entry;
//           position = "";
//         }

//         string[] signaturePieces = signature.Split(COLON, 2);
//         if(signaturePieces.Length > 1) {
//           string classDesignation = signaturePieces[0];
//           string methodSignature = signaturePieces[1];

//           if((classDesignation == "UnityEngine.Debug" && position == "") ||
//              (classDesignation.StartsWith("Class", StringComparison.Ordinal) && methodSignature == "Host(Object&)" && position == "") ||
//              (classDesignation == "Mono.CSharp.Evaluator" && methodSignature == "Evaluate(String, Object&, Boolean&)" && position == "") ||
//              (position.IndexOf("/UnityREPL/", StringComparison.Ordinal) >= 0) ||
//              (classDesignation == "UnityEditor.EditorApplication" && methodSignature == "Internal_CallUpdateFunctions()" && position == "")) {
//             filter = true;
//           }
//         } else {
//           Debug.Log("WAT.");
//           // WTF?!
//         }

//         if(!filter)
//           filteredTraceEntries.Add(entry);
//       }
//       var sb = new StringBuilder();
//       foreach(var s in filteredTraceEntries)
//         sb.Append(s).Append("\n");
//       _filteredStackTrace = sb.ToString();
//       return _filteredStackTrace;
//     }
//   }

//   public virtual void OnGUI(bool filterTraces) {
//     GUIStyle style = GUIStyle.none;

//     switch(logEntryType) {
//       case LogEntryType.Output:           style = LogEntryStyles.OutputStyle; break;
//       case LogEntryType.EvaluationError:  style = LogEntryStyles.EvaluationErrorStyle; break;
//       case LogEntryType.SystemConsoleOut: style = LogEntryStyles.SystemConsoleOutStyle; break;
//       case LogEntryType.SystemConsoleErr: style = LogEntryStyles.SystemConsoleErrStyle; break;
//     }
//     if(style == GUIStyle.none) {
//       Debug.Log("OY: " + logEntryType);
//     }

//     BeginContentBox();
//       ShowMessage(message, style);
//       OnSubGUI(filterTraces);
//     EndContentBox();
//   }

//   public virtual bool IsExpandable { get { return HasLongMessage; } }
//   public virtual void OnSubGUI(bool filterTraces) {}

//   protected void BeginContentBox()  { GUILayout.BeginVertical(GUI.skin.box); }
//   protected void EndContentBox()    { GUILayout.EndVertical(); }

//   protected void BeginIndent() {
//     GUILayout.BeginHorizontal();
//       GUILayout.Space(15);
//       GUILayout.BeginVertical();
//   }

//   protected void EndIndent() {
//       GUILayout.EndVertical();
//     GUILayout.EndHorizontal();
//   }

//   protected void ShowMessage(string msg, GUIStyle style) {
//     GUILayout.Label(msg ?? "", style);
//   }

//   protected void ShowStackTrace(bool filterTraces, GUIStyle style) {
//     if(!String.IsNullOrEmpty(filterTraces ? filteredStackTrace : stackTrace))
//       ShowMessage(filterTraces ? filteredStackTrace : stackTrace, style);
//   }
// }

// [Serializable]
// public class ConsoleLogEntry : LogEntry {
//   public LogType consoleLogType;

//   public override void OnSubGUI(bool filterTraces) {
//     BeginContentBox();
//       // case LogType.Error: // Debug.LogError(...).
//       // case LogType.Assert: // Unity internal error.
//       // case LogType.Exception: // Uncaught exception.
//       //   logStyle = LogEntryStyles.ConsoleLogErrorStyle;
//       //   break;
//       // case LogType.Warning: // Debug.LogWarning(...).
//       //   logStyle = LogEntryStyles.ConsoleLogWarningStyle;
//       //   break;
//       // case LogType.Log: // Debug.Log(...).
//       //   logStyle = LogEntryStyles.ConsoleLogNormalStyle;
//       //   break;
//       ShowStackTrace(filterTraces, LogEntryStyles.ConsoleLogStackTraceStyle);
//     EndContentBox();
//   }
// }

// [Serializable]
// public class CommandLogEntry : LogEntry {
//   public List<LogEntry> children;

//   public void Add(LogEntry child) {
//     if(child is CommandLogEntry) return;
//     children = children ?? new List<LogEntry>();
//     children.Add(child);
//   }

//   public override bool IsExpandable {
//     get {
//       if(children == null) children = new List<LogEntry>();
//       return HasLongMessage || children.Count > 0;
//     }
//   }

//   public override void OnSubGUI(bool filterTraces) {
//     var hasChildren = false;
//     if(isExpanded && hasChildren) {
//       BeginIndent();
//         foreach(LogEntry le in children)
//           le.OnGUI(filterTraces);
//       EndIndent();
//     }
//   }
// }
