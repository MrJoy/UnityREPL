//-----------------------------------------------------------------
// Adapted from repl.cs:
// https://github.com/mono/mono/tree/master/mcs/tools/csharp
//
//-----------------------------------------------------------------
using System;
using System.Collections;
using System.Text;
using Mono.CSharp;
using UnityEngine;

public class PrettyPrint {
  static string EscapeString(string s) {
    return s.Replace("\"", "\\\"");
  }

  static void EscapeChar(StringBuilder output, char c) {
    if(c == '\'')
      output.Append("'\\''");
    else if(c > 32)
      output.AppendFormat("'{0}'", c);
    else {
      switch(c) {
      case '\a':
        output.Append("'\\a'");
        break;
      case '\b':
        output.Append("'\\b'");
        break;
      case '\n':
        output.Append("'\\n'");
        break;
      case '\v':
        output.Append("'\\v'");
        break;
      case '\r':
        output.Append("'\\r'");
        break;
      case '\f':
        output.Append("'\\f'");
        break;
      case '\t':
        output.Append("'\\t");
        break;
      default:
        output.AppendFormat("'\\x{0:x}", (int)c);
        break;
      }
    }
  }

  private static int _depth = 0;

  private static void OpenInline(StringBuilder output, int listLength) {
    output.Append(listLength < 10 ? "{ " : "{\n\t");
    _depth++;
  }

  private static void CloseInline(StringBuilder output, int listLength) {
    output.Append(listLength < 10 ? " }" : "\n}");
    _depth--;
  }

  private static void NextItem(StringBuilder output, int listLength) {
    output.Append(listLength < 10 ? ", " : ",\n\t");
  }

  private static void Open(StringBuilder output) {
    output.Append("{");
    _depth++;
  }

  private static void Close(StringBuilder output) {
    output.Append("}");
    _depth--;
  }

  public static void PP(StringBuilder output, object result, bool expandTypes = false) {
    _depth = 0;
    InternalPP(output, result, expandTypes);
  }

  protected static void InternalPP(StringBuilder output, object result, bool expandTypes = false) {
    if(result == null)
      output.Append("null");
    else {
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
        Array a = (Array)result;
        int top = a.GetUpperBound(0), bottom = a.GetLowerBound(0);
        OpenInline(output, top - bottom);
        for(int i = bottom; i <= top; i++) {
          InternalPP(output, a.GetValue(i));
          if(i != top)
            NextItem(output, top - bottom);
        }
        CloseInline(output, top - bottom);
      } else if(result is bool)
        output.Append(((bool)result) ? "true" : "false");
      else if(result is string)
        output.Append('"').Append(EscapeString((string)result)).Append('"');
      else if(result is IDictionary) {
        IDictionary dict = (IDictionary)result;
        int top = dict.Count, count = 0;
        Open(output);
        foreach(DictionaryEntry entry in dict) {
          count++;
          InternalPP(output, entry.Key);
          output.Append(": ");
          InternalPP(output, entry.Value);
          if(count != top)
            NextItem(output, 0);
        }
        Close(output);
      } else if(result is IEnumerable) {
        int i = 0;
        ArrayList tmp = new ArrayList();
        foreach(object item in(IEnumerable)result)
          tmp.Add(item);
        OpenInline(output, tmp.Count);
        foreach(object item in tmp) {
          if(i++ != 0)
            NextItem(output, tmp.Count);
          InternalPP(output, item);
        }
        CloseInline(output, tmp.Count);
      } else if(result is char)
        EscapeChar(output, (char)result);
      else if(result is Type || result.GetType().Name == "MonoType") {
        if(_depth > 0 || !expandTypes)
          output.Append("typeof(" + ((Type)result).Namespace + "." + ((Type)result).Name + ")");
        else
          output.Append(InteractiveBase.Describe(result));
      } else
        output.Append(result.ToString());
    }
  }
}
