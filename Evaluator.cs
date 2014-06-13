//-----------------------------------------------------------------
//  Evaluator
//  Copyright 2009-2014 Jon Frisby
//  All rights reserved
//
//-----------------------------------------------------------------
// Core evaluation loop, including environment handling for living in the Unity
// editor and dealing with its code reloading behaviors.
//-----------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using Mono.CSharp;

class DebugReportPrinter : ReportPrinter {
  List<AbstractMessage> messages = new List<AbstractMessage>();
  public List<AbstractMessage> Messages { get { return messages; } }

  // public bool MissingTypeReported (ITypeDefinition typeDefinition) {
  //   if (this.reported_missing_definitions == null)
  //     this.reported_missing_definitions = new HashSet<ITypeDefinition> ();
  //   if (this.reported_missing_definitions.Contains (typeDefinition))
  //     return true;
  //   this.reported_missing_definitions.Add (typeDefinition);
  //   return false;
  // }

  // protected void Print (AbstractMessage msg, TextWriter output, bool showFullPath) {
  //   StringBuilder stringBuilder = new StringBuilder ();
  //   if (!msg.Location.IsNull) {
  //     if (showFullPath)
  //       stringBuilder.Append (msg.Location.ToStringFullName ());
  //     else
  //       stringBuilder.Append (msg.Location.ToString ());
  //     stringBuilder.Append (" ");
  //   }
  //   stringBuilder.AppendFormat ("{0} CS{1:0000}: {2}", msg.MessageType, msg.Code, msg.Text);
  //   if (!msg.IsWarning)
  //     output.WriteLine (this.FormatText (stringBuilder.ToString ()));
  //   else
  //     output.WriteLine (stringBuilder.ToString ());
  //   if (msg.RelatedSymbols != null) {
  //     string[] relatedSymbols = msg.RelatedSymbols;
  //     for (int i = 0; i < relatedSymbols.Length; i++) {
  //       string str = relatedSymbols [i];
  //       output.WriteLine (str + msg.MessageType + ")");
  //     }
  //   }
  // }

  public override void Print(AbstractMessage msg, bool showFullPath) {
    base.Print(msg, showFullPath);
    messages.Add(msg);
  }

  public new void Reset() {
    base.Reset();
    messages.Clear();
  }
}

public class EvaluationHelper {
  private static DebugReportPrinter reporter  = new DebugReportPrinter();
  private static CompilerSettings   settings  = new CompilerSettings();
  private static CompilerContext    context   = new CompilerContext(settings, reporter);
  public static readonly Evaluator  evaluator = new Evaluator(context);

  private static string[] ASSEMBLIES_TO_IGNORE = {
    "mscorlib",
    "System",
    "System.Core",
    "Mono.CSharp"
  };
  private static string[] ASSEMBLIES_TO_REFERENCE = {
    "System.Configuration",
    "System.Xml",

    "Mono.Security",
    "Mono.Cecil",

    "nunit.framework",
    "ICSharpCode.NRefactory",

    "UnityScript",
    "UnityScript.Lang",
    "Boo.Lang",
    "Boo.Lang.Parser",
    "Boo.Lang.Compiler",
    "Unity.IvyParser",
    "Unity.DataContract",
    "Unity.PackageManager",
    "Unity.Locator",

    "UnityEngine",

    "UnityEditor",
    "UnityEditor.Graphs",

    "@@EXTENSIONS@@",
    // "UnityEditor.BB10.Extensions",
    // "UnityEditor.iOS.Extensions",
    // "UnityEditor.Android.Extensions",

    "Assembly-CSharp-firstpass",
    "Assembly-UnityScript-firstpass",
    "Assembly-Boo-firstpass",
    "Assembly-CSharp",
    "Assembly-UnityScript",
    "Assembly-Boo",
    "Assembly-CSharp-Editor-firstpass",
    "Assembly-UnityScript-Editor-firstpass",
    "Assembly-Boo-Editor-firstpass",
    "Assembly-CSharp-Editor",
    "Assembly-UnityScript-Editor",
    "Assembly-Boo-Editor"
  };

  private bool IsPlatformSupportAssembly(string shortName) {
    return shortName.StartsWith("UnityEditor.") &&
           shortName.EndsWith(".Extensions");
  }

  private bool IsExpected(string shortName) {
    foreach(var name in ASSEMBLIES_TO_REFERENCE)
      if(shortName == name) return true;
    return false;
  }

  private bool IsIgnored(string shortName) {
    foreach(var name in ASSEMBLIES_TO_IGNORE)
      if(shortName == name) return true;
    return false;
  }

  // public static Assembly channel;
  protected void TryLoadingAssemblies() {
    Dictionary<string,Assembly> assemblyMap = new Dictionary<string,Assembly>();
    HashSet<string> extensionAssemblies = new HashSet<string>();
    HashSet<string> unknownAssemblies = new HashSet<string>();
    foreach(var b in AppDomain.CurrentDomain.GetAssemblies()) {
      string  shortName   = b.GetName().Name;
      bool    isExpected  = IsExpected(shortName),
              isSupport   = IsPlatformSupportAssembly(shortName),
              isIgnored   = IsIgnored(shortName);
      if(isSupport)
        extensionAssemblies.Add(shortName);
      if(!isIgnored) {
        if(!isExpected && !isSupport)
          unknownAssemblies.Add(shortName);
        assemblyMap[shortName] = b;
      }
    }
    foreach(var name in ASSEMBLIES_TO_REFERENCE) {
      if(name == "@@EXTENSIONS@@") {
        foreach(var extName in extensionAssemblies) {
          // Debug.Log("Loading Platform Support Assembly: " + extName);
          try { evaluator.ReferenceAssembly(assemblyMap[extName]); }
          catch {}
        }
        foreach(var unkName in unknownAssemblies) {
          // Debug.Log("Loading Plugin(?) Assembly: " + unkName);
          try { evaluator.ReferenceAssembly(assemblyMap[unkName]); }
          catch {}
        }
      } else {
        // Debug.Log("Loading Assembly: " + name);
        try { evaluator.ReferenceAssembly(assemblyMap[name]); }
        catch {}
      }
    }

    // int i = 0;
    // foreach(var b in AppDomain.CurrentDomain.GetAssemblies()) {
    //   string  shortName   = b.GetName().Name;
    //   bool    isExpected  = IsExpected(shortName),
    //           isSupport   = IsPlatformSupportAssembly(shortName),
    //           isIgnored   = IsIgnored(shortName);

    //   if(!isIgnored && !isExpected && !isSupport) {
    //     channel = b;
    //     i += 1;
    //     evaluator.Run("var assembly" + i.ToString() + " = EvaluationHelper.channel;");
    //   }
    // }

    // TODO: Anything else we should toss in here?
    evaluator.Run("using System;");
    evaluator.Run("using System.IO;");
    // evaluator.Run("using System.IO.Pipes;");
    evaluator.Run("using System.Linq;");
    evaluator.Run("using System.Linq.Expressions;");
    evaluator.Run("using System.Collections;");
    evaluator.Run("using System.Collections.Generic;");
    evaluator.Run("using System.Reflection;");
    evaluator.Run("using System.Text;");
    evaluator.Run("using UnityEditor;");
    evaluator.Run("using UnityEngine;");
  }

  public bool Init(ref bool isInitialized) {
    // Don't be executing code when we're about to reload it.  Not sure this is
    // actually needed but seems prudent to be wary of it.
    if(EditorApplication.isCompiling) return false;

    /*
    We need to tell the evaluator to reference stuff we care about.  Since
    there's a lot of dynamically named stuff that we might want, we just pull
    the list of loaded assemblies and include them "all" (with the exception of
    a couple that I have a sneaking suspicion may be bad to reference -- noted
    below).

    Examples of what we might get when asking the current AppDomain for all
    assemblies (short names only):

    Stuff we avoid:
      UnityDomainLoad <-- Unity gubbins.  Probably want to avoid this.
      Mono.CSharp <-- The self-same package used to pull this off.  Probably
                      safe, but not taking any chances.
      interactive0 <-- Looks like what Mono.CSharp is making on the fly.  If we
                       load those, it APPEARS we may wind up holding onto them
                       'forever', so...  Don't even try.


    Mono runtime, which we probably get 'for free', but include just in case:
      System
      mscorlib

    Unity runtime, which we definitely want:
      UnityEditor
      UnityEngine
      UnityScript.Lang
      Boo.Lang

    The assemblies Unity generated from our project code, and whose names we
    can't predict (thus all this headache of doing this dynamically):
      66e7989537eed4bf0b3da7923cca36a5
      3355ac06262db4cc485a4df3f5d80f92
      056b51e0f06e443768d0eec4b9a4e6c0
      bb4826d22f7064220b2889d64c02fea6
      5025cb470ec5941b9b5afef2b57be7d4
      78b446ec0e1c748e3ba1927569415c6a
    */
    bool retVal = false;
    if(!isInitialized) {
      TryLoadingAssemblies();

      isInitialized = true;
    } else {
      retVal = true;
    }

    if(retVal) {
      if(evaluator.InteractiveBaseClass != typeof(UnityBaseClass))
        evaluator.InteractiveBaseClass = typeof(UnityBaseClass);
    }
    return retVal;
  }

  [System.NonSerialized]
  private StringBuilder outputBuffer = new StringBuilder();

  public bool Eval(string code, bool isRetry = false) {
    EditorApplication.LockReloadAssemblies();

    bool      hasOutput       = false;
    object    output          = null;
    string    res             = null,
              tmpCode         = code.Trim();
    Exception ex              = null;

    try {
      if(tmpCode.StartsWith("=", StringComparison.Ordinal)) {
        // Special case handling of calculator mode.  The problem is that
        // expressions involving multiplication are grammatically ambiguous
        // without a var declaration or some other grammatical construct.
        tmpCode = "(" + tmpCode.Substring(1, tmpCode.Length - 1) + ");";
      }
      res = evaluator.Evaluate(tmpCode, out output, out hasOutput);
    } catch(Exception e) {
      Debug.Log("Ooga booga");
      ex = e;
    } finally {
      // TODO: Add a debugging button to the UI. >.<
      Debug.Log(
        tmpCode + "\n\n" +
        "Output (hasOutput == " + (hasOutput) + ", null? == " + (output == null) + "):\n" + output + "\n\n" +
        "Res (null? == " + (res == null) + "):\n" + res
      );
    }

    //  hasOutput,  output==null, res==null,  throws?,  error-output?,  use-case                                    outcome
    //  ---------   ------------  ---------   --------  --------------  -----------------------------------------   ----------------------------------
    //  true,       false?,       true?,      false?,   false?,         Complete input, output, no exception.       log code, log output,                 reset input
    //  false,      true,         true,       false,    false?,         Complete input, no output, no exception.    log code,                             reset input
    //  false,      true,         true,       true,     false?,         Runtime error.                              log code,             log exception,  reset input
    //  false,      true,         true,       false,    true,           Syntax error.                                                     log exception
    //  false,      true,         false,      false,    false?,         Incomplete input.                           N/A
    bool  logOutput     = false,
          logException  = false,
          logReports    = (reporter.WarningsCount > 0) || (reporter.ErrorsCount > 0),
          resetInput    = true;

    if(hasOutput)                   logOutput     = true;   // Apparently, we has OUTPUT!
    if(ex != null)                  logException  = true;   // Always log any exceptions we get.
    if(res != null && !logReports)  resetInput    = false;  // Incomplete input

    // Handle Outcomes...
    if(resetInput)      Debug.Log(code);
    if(logOutput)       Debug.Log(FormatObject(output));
    if(logException)    Debug.LogException(ex);
    if(logReports) {
      foreach(var msg in reporter.Messages) {
        Debug.Log(msg.MessageType);
        // TODO: Handle msg.RelatedSymbols...
        if(msg.IsWarning)
          Debug.LogWarning(msg.Text);
        // else if(msg.IsError)
        //   Debug.LogError(msg.Text);
        else
          Debug.LogError(msg.Text);
      }
      reporter.Reset();
    }

    EditorApplication.UnlockReloadAssemblies();
    return resetInput;
  }

  private string FormatObject(object output) {
    outputBuffer.Length = 0;
    PrettyPrint.PP(outputBuffer, output, true);

    return outputBuffer.ToString();
  }
}


// WARNING: Absolutely NOT thread-safe!
internal class EvaluatorProxy : ReflectionProxy {
  private static readonly Type _Evaluator = typeof(Evaluator);
  private static readonly FieldInfo _fields = _Evaluator.GetField("fields", NONPUBLIC_INSTANCE);

  internal static Dictionary<string, Tuple<FieldSpec, FieldInfo>> fields {
    get {
      return (Dictionary<string, Tuple<FieldSpec, FieldInfo>>)_fields.GetValue(EvaluationHelper.evaluator);
    }
  }
}


// WARNING: Absolutely NOT thread-safe!
internal class TypeManagerProxy : ReflectionProxy {
  private static readonly Type _TypeManager = typeof(Evaluator).Assembly.GetType("Mono.CSharp.TypeManager");
  private static readonly MethodInfo _CSharpName = _TypeManager.GetMethod("CSharpName", PUBLIC_STATIC, null, Signature(typeof(List<TypeSpec>)), null);

  // Save an allocation per access here...
  private static readonly object[] _CSharpNameParams = new object[] { new List<TypeSpec>() };
  internal static string CSharpName(TypeSpec t) {
    string name = "";
    try {
      var list = (List<TypeSpec>)_CSharpNameParams[0];
      if(list.Count == 0) {
        list.Add(null);
      }
      list[0] = t;
      name = (string)_CSharpName.Invoke(null, _CSharpNameParams);
    } catch(Exception) {
      name = "?";
    }
    return name;
  }
}


// Dummy class so we can output a string and bypass pretty-printing of it.
public struct REPLMessage {
  public string msg;
  public REPLMessage(string m) { msg = m; }
}


public class UnityBaseClass {
  private static readonly REPLMessage _help = new REPLMessage(@"UnityREPL v." + Shell.VERSION + @":

help;     -- This screen; help for helper commands.  Click the '?' icon on the toolbar for more comprehensive help.
vars;     -- Show the variables you've created this session, and their current values.
");
  public static REPLMessage help { get { return _help; } }

  public static REPLMessage vars {
    get {
      var tmp = new StringBuilder();
      // TODO: Sort this list...
      foreach(var kvp in EvaluatorProxy.fields) {
        var field = kvp.Value;
        tmp
          .Append(TypeManagerProxy.CSharpName(field.Item1.MemberType))
          .Append(" ")
          .Append(kvp.Key)
          .Append(" = ");
        PrettyPrint.PP(tmp, field.Item2.GetValue(null));
        tmp.Append(";\n");
      }
      return new REPLMessage(tmp.ToString());
    }
  }
}
