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
using System.Threading;
using System.IO;
using Mono.CSharp;

class EvaluationException : Exception {
}

class EvaluationHelper {
  public EvaluationHelper() {
    TryLoadingAssemblies(false);
  }

  protected bool TryLoadingAssemblies(bool isInitialized) {
    if(isInitialized)
      return true;

//    Debug.Log("Attempting to load assemblies...");

    foreach(Assembly b in AppDomain.CurrentDomain.GetAssemblies()) {
      string assemblyShortName = b.GetName().Name;
      if(!(assemblyShortName.StartsWith("Mono.CSharp") || assemblyShortName.StartsWith("UnityDomainLoad") ||
           assemblyShortName.StartsWith("interactive"))) {
        //Debug.Log("Giving Mono.CSharp a reference to assembly: " + assemblyShortName);
        Evaluator.ReferenceAssembly(b);
      }
    }


    // These won't work the first time through after an assembly reload.  No
    // clue why, but the Unity* namespaces don't get found.  Perhaps they're
    // being loaded into our AppDomain asynchronously and just aren't done yet?
    // Regardless, attempting to hit them early and then trying again later
    // seems to work fine.
    Evaluator.Run("using System;");
    Evaluator.Run("using System.IO;");
    Evaluator.Run("using System.Linq;");
    Evaluator.Run("using System.Collections;");
    Evaluator.Run("using System.Collections.Generic;");
    Evaluator.Run("using UnityEditor;");
    Evaluator.Run("using UnityEngine;");

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

    if(Evaluator.InteractiveBaseClass != typeof(UnityBaseClass))
      Evaluator.InteractiveBaseClass = typeof(UnityBaseClass);
  }

  public bool Eval(string code) {
    EditorApplication.LockReloadAssemblies();

    bool status     = false,
         hasOutput  = false;
    object output   = null;
    string res      = null,
           tmpCode  = code.Trim();
//    Debug.Log("Evaluating: " + tmpCode);

    try {
      if(tmpCode.StartsWith("=")) {
        // Special case handling of calculator mode.  The problem is that
        // expressions involving multiplication are grammatically ambiguous
        // without a var declaration or some other grammatical construct.
        // TODO: Change the prompt in calculator mode.  Needs to be done from Shell.
        tmpCode = "(" + tmpCode.Substring(1, tmpCode.Length - 1) + ");";
      }
      res = Evaluate(tmpCode, out output, out hasOutput);
    } catch(EvaluationException) {
      Debug.LogError(@"Error compiling/executing code.  Please double-check syntax, method/variable names, etc.
You can find more information in Unity's `Editor.log` file (*not* the editor console!).");

      output    = new Evaluator.NoValueSet();
      hasOutput = false;
      res       = tmpCode; // Enable continued editing on syntax errors, etc.
    } catch(Exception e) {
      Debug.LogError(e);

      res       = tmpCode; // Enable continued editing on unexpected errors.
    } finally {
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

  /* Copy-pasta'd from the DLL to try and differentiate between kinds of failure mode. */
  private string Evaluate(string input, out object result, out bool result_set) {
    result_set = false;
    result = null;

    CompiledMethod compiledMethod;
    string remainder = null;
    remainder = Evaluator.Compile(input, out compiledMethod);
    if(remainder != null)
      return remainder;
    if(compiledMethod == null)
      throw new EvaluationException();

    object typeFromHandle = typeof(Evaluator.NoValueSet);
    try {
      EvaluatorProxy.invoke_thread = Thread.CurrentThread;
      EvaluatorProxy.invoking      = true;
      compiledMethod(ref typeFromHandle);
    } catch(ThreadAbortException arg) {
      Thread.ResetAbort();
      Console.WriteLine("Interrupted!\n{0}", arg);
      // TODO: How best to handle this?
    } finally {
      EvaluatorProxy.invoking = false;
    }
    if(typeFromHandle != typeof(Evaluator.NoValueSet)) {
      result_set  = true;
      result      = typeFromHandle;
    }
    return null;
  }
}

// WARNING: Absolutely NOT thread-safe!
internal class EvaluatorProxy : ReflectionProxy {
  private static readonly Type _Evaluator = typeof(Evaluator);
  private static readonly FieldInfo _fields = _Evaluator.GetField("fields", NONPUBLIC_STATIC);
  private static readonly FieldInfo _invoke_thread = _Evaluator.GetField("invoke_thread", NONPUBLIC_STATIC);
  private static readonly FieldInfo _invoking = _Evaluator.GetField("invoking", NONPUBLIC_STATIC);

  internal static Hashtable fields { get { return (Hashtable)_fields.GetValue(null); } }
  internal static Thread invoke_thread {
    get { return (Thread)_invoke_thread.GetValue(null); }
    set { _invoke_thread.SetValue(null, value); }
  }
  internal static bool invoking {
    get { return (bool)_invoking.GetValue(false); }
    set { _invoking.SetValue(null, value); }
  }
}

// WARNING: Absolutely NOT thread-safe!
internal class TypeManagerProxy : ReflectionProxy {
  private static readonly Type _TypeManager = typeof(Evaluator).Assembly.GetType("Mono.CSharp.TypeManager");
  private static readonly MethodInfo _CSharpName = _TypeManager.GetMethod("CSharpName",
                                                                          PUBLIC_STATIC,
                                                                          null,
                                                                          Signature(typeof(Type)),
                                                                          null);

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
      Hashtable fields  = EvaluatorProxy.fields;
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
