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

//CompilerSettings()
//  .AddConditionalSymbol(string)
//  .AddWarningAsError(int32)
//  .AddWarningOnly(int32)
//  .IsConditionalSymbolDefined(string)
//  .IsWarningAsError(int32)
//  .IsWarningDisabledGlobally(int32)
//  .IsWarningEnabled(int32)
//  .SetIgnoreWarning(int32)
//  .NeedsEntryPoint
//  .SourceFiles
//  .AssemblyReferences
//  .AssemblyReferencesAliases
//  .BreakOnInternalError
//  .Checked
//  .EnhancedWarnings
//  .MainClass
//  .Modules
//  .ParseOnly
//  .StatementMode
//  .TabSize
//  .TokenizeOnly
//  .WarningLevel
//  .WarningsAreErrors

//CompilerContext(CompilerSettings, ReportWriter)
//  .Report

//CSharpParser(SeekableStreamReader, CompilationSourceFile, Report, ParserSession)

//Report(CompilerContext, ReportPrinter)

//ReportPrinter()
//  .ErrorsCount
//  .HasRelatedSymbolSupport
//  .WarningsCount

//Outline(Type, TextWriter, bool declared_only, bool show_private, bool filter_obsolete)
//  .OutlineType()

//Evaluator(CompilerContext)
//  .Compile(string)
//  .Compile(string, out CompiledMethod)
//  .Evaluate(string, out Object, out bool)
//  .GetCompletions(string, out string)
//  .LoadAssembly(string)
//  .ReferenceAssembly(Assembly)
//  .Run(string)
//  .InteractiveBaseClass
//  .Terse
class UnityReportPrinter : ReportPrinter {
  // public void Print(AbstractMessage msg, bool showFullPath) {
  //   Debug.Log(msg.ToString());
  // }
}

class EvaluationHelper {
  [System.NonSerialized]
  private static UnityReportPrinter printer = new UnityReportPrinter();
  [System.NonSerialized]
  private static CompilerSettings settings = new CompilerSettings();
  [System.NonSerialized]
  private static CompilerContext context = new CompilerContext(settings, printer);
  [System.NonSerialized]
  private static Evaluator evaluator = new Evaluator(context) {
    InteractiveBaseClass = typeof(UnityBaseClass)
  };

  // public EvaluationHelper() {
  //   TryLoadingAssemblies(false);
  // }

  protected bool TryLoadingAssemblies(bool isInitialized) {
    if(isInitialized)
      return true;

    Debug.Log("Attempting to load assemblies...");

    foreach(Assembly b in AppDomain.CurrentDomain.GetAssemblies()) {
      string assemblyShortName = b.GetName().Name;
      if(!(assemblyShortName.StartsWith("Mono.CSharp") || assemblyShortName.StartsWith("UnityDomainLoad") || assemblyShortName.StartsWith("interactive"))) {
        //Debug.Log("Giving Mono.CSharp a reference to assembly: " + assemblyShortName);
        evaluator.ReferenceAssembly(b);
      }
//      else
//      {
//        Debug.LogWarning("Ignoring assembly: " + assemblyShortName);
//      }
    }


    // These won't work the first time through after an assembly reload.  No
    // clue why, but the Unity* namespaces don't get found.  Perhaps they're
    // being loaded into our AppDomain asynchronously and just aren't done yet?
    // Regardless, attempting to hit them early and then trying again later
    // seems to work fine.
    evaluator.LoadAssembly("System");
    evaluator.LoadAssembly("System.IO");
    evaluator.LoadAssembly("System.Linq");
    evaluator.LoadAssembly("System.Collections");
    evaluator.LoadAssembly("System.Collections.Generic");
    evaluator.LoadAssembly("UnityEditor");
    evaluator.LoadAssembly("UnityEngine");
    // evaluator.Run("using System;");
    // evaluator.Run("using System.IO;");
    // evaluator.Run("using System.Linq;");
    // evaluator.Run("using System.Collections;");
    // evaluator.Run("using System.Collections.Generic;");
    // evaluator.Run("using UnityEditor;");
    // evaluator.Run("using UnityEngine;");

    return true;
  }

  public void Init(ref bool isInitialized) {
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

    The assemblies Unity generated from our project code now all begin with Assembly:
      Assembly-CSharp
      Assembly-CSharp-Editor
      ...
    */
    isInitialized = TryLoadingAssemblies(isInitialized);

    // if(evaluator.InteractiveBaseClass != typeof(UnityBaseClass))
    //   evaluator.InteractiveBaseClass = typeof(UnityBaseClass);
  }

  public bool Eval(string code) {
    EditorApplication.LockReloadAssemblies();

    bool status    = false,
         hasOutput = false;
    object output = null;
    string res     = null,
           tmpCode = code.Trim();
    Debug.Log("Evaluating: " + tmpCode);

    try {
      if(tmpCode.StartsWith("=")) {
        // Special case handling of calculator mode.  The problem is that
        // expressions involving multiplication are grammatically ambiguous
        // without a var declaration or some other grammatical construct.
        tmpCode = "(" + tmpCode.Substring(1, tmpCode.Length - 1) + ");";
      }
      res = evaluator.Evaluate(tmpCode, out output, out hasOutput);
      //if(res == tmpCode)
      //  Debug.Log("Unfinished input...");
    } catch(Exception e) {
      Debug.LogError(e);

      output = null; //new Evaluator.NoValueSet();
      hasOutput = false;
      status = true; // Need this to avoid 'stickiness' where we let user
      // continue editing due to incomplete code.
    } finally {
      Debug.Log("res==" + res + " (null? " + (res == null) + ")\n\noutput==" + output + "\n\nhasOutput==" + hasOutput);
      status = res == null;
    }

    if(hasOutput) {
      if(status) {
        try {
          StringBuilder sb = new StringBuilder();
          PrettyPrint.PP(sb, output, true);
          Debug.Log(sb.ToString());
        } catch(Exception e) {
          Debug.LogError(e.ToString().Trim());
        }
      }
    }

    EditorApplication.UnlockReloadAssemblies();
    return status;
  }
}

// WARNING: Absolutely NOT thread-safe!
internal class EvaluatorProxy : ReflectionProxy {
  private static readonly Type _Evaluator = typeof(Evaluator);
  private static readonly FieldInfo _fields = _Evaluator.GetField("fields", NONPUBLIC_STATIC);

  internal static Hashtable fields {
    get {
      if(_fields == null)
        return null;
      return (Hashtable)_fields.GetValue(null);
    }
  }
}


// WARNING: Absolutely NOT thread-safe!
internal class TypeManagerProxy : ReflectionProxy {
  private static readonly Type _TypeManager = typeof(Evaluator).Assembly.GetType("Mono.CSharp.TypeManager");
  private static readonly MethodInfo _CSharpName = _TypeManager.GetMethod("CSharpName", PUBLIC_STATIC, null, Signature(typeof(Type)), null);

  // Save an allocation per access here...
  private static readonly object[] _CSharpNameParams = new object[] { null };

  internal static string CSharpName(Type t) {
    // TODO: What am I doing wrong here that this throws on generics??
    string name = "";
    try {
      _CSharpNameParams[0] = t;
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

  public REPLMessage(string m) {
    msg = m;
  }
}

public class UnityBaseClass {
  private static readonly REPLMessage _help = new REPLMessage(@"UnityREPL v." + Shell.VERSION + @":

help;     -- This screen; help for helper commands.  Click the '?' icon on the toolbar for more comprehensive help.
vars;     -- Show the variables you've created this session, and their current values.
");

  public static REPLMessage help { get { return _help; } }

  public static REPLMessage vars {
    get {
      Hashtable fields = EvaluatorProxy.fields;
      StringBuilder tmp = new StringBuilder();
      // TODO: Sort this list...
      foreach(DictionaryEntry kvp in fields) {
        FieldInfo field = (FieldInfo)kvp.Value;
        tmp
          .Append(TypeManagerProxy.CSharpName(field.FieldType))
          .Append(" ")
          .Append(kvp.Key)
          .Append(" = ");
        PrettyPrint.PP(tmp, field.GetValue(null));
        tmp.Append(";\n");
      }
      return new REPLMessage(tmp.ToString());
    }
  }
}
