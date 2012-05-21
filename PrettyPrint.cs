//-----------------------------------------------------------------
//  PrettyPrint
//  Copyright 2009-2012 MrJoy, Inc.
//  All rights reserved
//
//-----------------------------------------------------------------
// Adapted from repl.cs:
// http://anonsvn.mono-project.com/viewvc/trunk/mcs/tools/csharp/
//
//-----------------------------------------------------------------
using System;
using System.Collections;
using System.Text;
using Mono.CSharp;
using UnityEngine;

public class PrettyPrint {
  static string EscapeString(string s) { return s.Replace ("\"", "\\\""); }

  static void EscapeChar(StringBuilder output, char c) {
    if(c == '\'')
      output.Append("'\\''");
    else if(c > 32)
      output.AppendFormat("'{0}'", c);
    else {
      switch (c) {
        case '\a':
          output.Append("'\\a'"); break;
        case '\b':
          output.Append("'\\b'"); break;
        case '\n':
          output.Append("'\\n'"); break;
        case '\v':
          output.Append("'\\v'"); break;
        case '\r':
          output.Append("'\\r'"); break;
        case '\f':
          output.Append("'\\f'"); break;
        case '\t':
          output.Append("'\\t"); break;
        default:
          output.AppendFormat("'\\x{0:x}", (int) c); break;
      }
    }
  }

  private static int _depth = 0;
  private static void OpenInline(StringBuilder output) { output.Append("{ "); _depth++; }
  private static void CloseInline(StringBuilder output) { output.Append(" }"); _depth--; }
  private static void NextItem(StringBuilder output) { output.Append(", "); }

  private static void Open(StringBuilder output) { output.Append("{"); _depth++; }
  private static void Close(StringBuilder output) { output.Append("}"); _depth--; }

  public static void PP(StringBuilder output, object result) {
    _depth = 0;
    InternalPP(output, result);
  }

  protected static void InternalPP(StringBuilder output, object result) {
    if(result == null) {
      output.Append("null");
    } else {
      if(result is REPLMessage) {
        // Raw, no escaping or quoting.
        output.Append(((REPLMessage)result).msg);
      } else if(result is Component) {
        string n;
        try {
          n = ((Component)result).name;
        } catch(MissingReferenceException) {
          n = "<destroyed>";
        }
        output.Append(n);
      } else if(result is GameObject) {
        string n;
        try {
          n = ((GameObject)result).name;
        } catch(MissingReferenceException) {
          n = "<destroyed>";
        }
        output.Append(n);
      } else if(result is Array) {
        Array a = (Array) result;
        OpenInline(output);
        int top = a.GetUpperBound(0);
        for(int i = a.GetLowerBound(0); i <= top; i++) {
          InternalPP(output, a.GetValue(i));
          if(i != top) NextItem(output);
        }
        CloseInline(output);
      } else if(result is bool) {
        output.Append(((bool) result) ? "true" : "false");
      } else if(result is string) {
        output.Append('"').Append(EscapeString((string)result)).Append('"');
      } else if(result is IDictionary) {
        IDictionary dict = (IDictionary)result;
        int top = dict.Count, count = 0;
        Open(output);
        foreach(DictionaryEntry entry in dict) {
          count++;
          InternalPP(output, entry.Key);
          output.Append(": ");
          InternalPP(output, entry.Value);
          if(count != top) NextItem(output);
        }
        Close(output);
      } else if(result is IEnumerable) {
        int i = 0;
        OpenInline(output);
        foreach(object item in (IEnumerable)result) {
          if(i++ != 0) NextItem(output);
          InternalPP(output, item);
        }
        CloseInline(output);
      } else if(result is char) {
        EscapeChar(output, (char)result);
      } else if(result is Type) {
        if(_depth > 0)
          output.Append(((Type)result).Name);
        else
          output.Append(InteractiveBase.Describe(result));
      } else {
        output.Append(result.ToString());
      }
    }
  }
}
