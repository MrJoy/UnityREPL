//-----------------------------------------------------------------
//  Evaluator
//  Copyright 2009-2013 Jon Frisby
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

class EvaluationHelper
{
	public EvaluationHelper ()
	{
		TryLoadingAssemblies (false);
	}

	protected bool TryLoadingAssemblies (bool isInitialized)
	{
		if (isInitialized)
			return true;

		Debug.Log ("Attempting to load assemblies...");

		foreach (Assembly b in AppDomain.CurrentDomain.GetAssemblies()) {
			string assemblyShortName = b.GetName ().Name;
			if (!(assemblyShortName.StartsWith ("Mono.CSharp") || assemblyShortName.StartsWith ("UnityDomainLoad") || assemblyShortName.StartsWith ("interactive"))) {
				Debug.Log("Giving Mono.CSharp a reference to assembly: " + assemblyShortName);
				Evaluator.ReferenceAssembly (b);
			}
			else
			{
				Debug.LogWarning("Ignoring assembly: " + assemblyShortName);
			}
		}


		// These won't work the first time through after an assembly reload.  No
		// clue why, but the Unity* namespaces don't get found.  Perhaps they're
		// being loaded into our AppDomain asynchronously and just aren't done yet?
		// Regardless, attempting to hit them early and then trying again later
		// seems to work fine.
		Evaluator.Run ("using System;");
		Evaluator.Run ("using System.IO;");
		Evaluator.Run ("using System.Linq;");
		Evaluator.Run ("using System.Collections;");
		Evaluator.Run ("using System.Collections.Generic;");
		Evaluator.Run ("using UnityEditor;");
		Evaluator.Run ("using UnityEngine;");

		return true;
	}

	public void Init (ref bool isInitialized)
	{
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
		isInitialized = TryLoadingAssemblies (isInitialized);

		if (Evaluator.InteractiveBaseClass != typeof(UnityBaseClass))
			Evaluator.InteractiveBaseClass = typeof(UnityBaseClass);
	}

	public bool Eval (string code)
	{
		EditorApplication.LockReloadAssemblies ();

		bool status = false,
			 hasOutput = false;
		object output = null;
		string res = null,
			   tmpCode = code.Trim ();
		Debug.Log ("Evaluating: " + tmpCode);

		try {
			if (tmpCode.StartsWith ("=")) {
				// Special case handling of calculator mode.  The problem is that
				// expressions involving multiplication are grammatically ambiguous
				// without a var declaration or some other grammatical construct.
				tmpCode = "(" + tmpCode.Substring (1, tmpCode.Length - 1) + ");";
			}
			res = Evaluator.Evaluate (tmpCode, out output, out hasOutput);
			//if(res == tmpCode)
			//  Debug.Log("Unfinished input...");
		} catch (Exception e) {
			Debug.LogError (e);

			output = new Evaluator.NoValueSet ();
			hasOutput = false;
			status = true; // Need this to avoid 'stickiness' where we let user
						   // continue editing due to incomplete code.
		} finally {
			status = res == null;
		}

		if (hasOutput) {
			if (status) {
				try {
					// PrettyPrint.PP (outputBuffer, output, true);
				} catch (Exception e) {
					Debug.LogError (e.ToString().Trim());
				}
			}
		}

		EditorApplication.UnlockReloadAssemblies ();
		return status;
	}
}

// WARNING: Absolutely NOT thread-safe!
internal class EvaluatorProxy : ReflectionProxy
{
	private static readonly Type _Evaluator = typeof(Evaluator);
	private static readonly FieldInfo _fields = _Evaluator.GetField ("fields", NONPUBLIC_STATIC);

	internal static Hashtable fields { get { return (Hashtable)_fields.GetValue (null); } }
}


// WARNING: Absolutely NOT thread-safe!
internal class TypeManagerProxy : ReflectionProxy
{
	private static readonly Type _TypeManager = typeof(Evaluator).Assembly.GetType ("Mono.CSharp.TypeManager");
	private static readonly MethodInfo _CSharpName = _TypeManager.GetMethod ("CSharpName", PUBLIC_STATIC, null, Signature (typeof(Type)), null);

	// Save an allocation per access here...
	private static readonly object[] _CSharpNameParams = new object[] { null };

	internal static string CSharpName (Type t)
	{
		// TODO: What am I doing wrong here that this throws on generics??
		string name = "";
		try {
			_CSharpNameParams [0] = t;
			name = (string)_CSharpName.Invoke (null, _CSharpNameParams);
		} catch (Exception) {
			name = "?";
		}
		return name;
	}
}

// Dummy class so we can output a string and bypass pretty-printing of it.
public struct REPLMessage
{
	public string msg;

	public REPLMessage (string m)
	{
		msg = m;
	}
}

public class UnityBaseClass
{
	private static readonly REPLMessage _help = new REPLMessage (@"UnityREPL v." + Shell.VERSION + @":

help;     -- This screen; help for helper commands.  Click the '?' icon on the toolbar for more comprehensive help.
vars;     -- Show the variables you've created this session, and their current values.
");

	public static REPLMessage help { get { return _help; } }

	public static REPLMessage vars {
		get {
			Hashtable fields = EvaluatorProxy.fields;
			StringBuilder tmp = new StringBuilder ();
			// TODO: Sort this list...
			foreach (DictionaryEntry kvp in fields) {
				FieldInfo field = (FieldInfo)kvp.Value;
				tmp
					.Append (TypeManagerProxy.CSharpName (field.FieldType))
					.Append (" ")
					.Append (kvp.Key)
					.Append (" = ");
				PrettyPrint.PP (tmp, field.GetValue (null));
				tmp.Append (";\n");
			}
			return new REPLMessage (tmp.ToString ());
		}
	}
}
