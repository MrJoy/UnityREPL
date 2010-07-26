//-----------------------------------------------------------------
//  Editor v0.4
//  Copyright 2009-2010 MrJoy, Inc.
//  All rights reserved
//
//-----------------------------------------------------------------
// C#-based REPL tool.
//
// TODO: Make sure this plays nice with assembly reloads by:
//    A) Make sure the newest version of our code is made available after a
//       reload.
//    B) Make sure we're not inadvertently 'leaking' things into Mono's heap
//       (I.E. preventing unloading of old stuff) via dangling references and 
//       such.
// TODO: Integrate with Mono.CSharp.Report to get warning/error info in a more
//       elegant manner (just capturing it all raw is bad since we can't 
//       reliably format it.)
// TODO: Find the serializer used for the 'csharp' program to dump out objects 
//       gracefully and incorporate that.
// TODO: Replace the InteractiveBase class in which user-entered code runs so
//       things like 'quit' aren't accessible.
// TODO: Only auto-scroll to the bottom of the history view IFF we were at the 
//       bottom before the last command was executed.
// TODO: Persist history.
// TODO: Suss out undo and wrap code execution accordingly.
// TODO: Suss out undo and wrap editor accordingly.
// TODO: Capture System.Console.
// TODO: Capture Debug console outputs while running code.
// TODO: Allow expansion of history children, colorize meaningfully, etc...
//-----------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using Mono.CSharp;

class EvaluationHelper {
  private StringWriter reportWriter = new StringWriter();
  public EvaluationHelper() {
    StringBuilder buffer = FluffReporter();
    TryLoadingAssemblies();
    buffer.Length = 0;
  }

  protected StringBuilder FluffReporter() {
    if(Report.Stderr is StringWriter) {
      // In case the brutal assembly reloading Unity does caused an old instance
      // of our StringWriter to stick around, we resurrect it to avoid the 
      // chance that someone's holding a reference to it and writing to THAT
      // instead of what we assign to Report.Stderr.
      reportWriter = (StringWriter)Report.Stderr;
    }
    Report.Stderr = reportWriter;
    InteractiveBase.Error = reportWriter;
    InteractiveBase.Output = reportWriter;
    return reportWriter.GetStringBuilder();    
  }
  
  protected void TryLoadingAssemblies() {
    foreach(Assembly b in AppDomain.CurrentDomain.GetAssemblies()) {
      string assemblyShortName = b.GetName().Name;
      if(!(assemblyShortName.StartsWith("Mono.CSharp") || assemblyShortName.StartsWith("UnityDomainLoad") || assemblyShortName.StartsWith("interactive"))) {
        //System.Console.WriteLine("Giving Mono.CSharp a reference to " + assemblyShortName);
        Evaluator.ReferenceAssembly(b);
      }
    }

    // These won't work the first time through after an assembly reload.  No 
    // clue why, but the Unity* namespaces don't get found.  Perhaps they're 
    // being loaded into our AppDomain asynchronously and just aren't done yet?
    // Regardless, attempting to hit them early and then trying again later 
    // seems to work fine.
    Evaluator.Run("using System;");
    Evaluator.Run("using System.Linq;");
    Evaluator.Run("using System.Collections;");
    Evaluator.Run("using System.Collections.Generic;");
    Evaluator.Run("using UnityEditor;");
    Evaluator.Run("using UnityEngine;");
  }

  public bool Init(ref bool isInitialized) {
    // Don't be executing code when we're about to reload it.  Not sure this is
    // actually needed but seems prudent to be wary of it.
    if(EditorApplication.isCompiling) return false;

    StringBuilder buffer = FluffReporter();
    buffer.Length = 0;
    
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
    if(!isInitialized) {
      TryLoadingAssemblies();
      
      if(buffer.Length > 0) {
        // Whoops!  Something didn't go right!
        //Console.WriteLine("Got some (hopefully transient) static while initializing:");
        //Console.WriteLine(buffer);
        buffer.Length = 0;
        return false;
      } else {
        isInitialized = true;
        return true;
      }
    } else {
      return true;
    }
  }
  private StringBuilder messageBuffer = new StringBuilder();
  
  public string Messages {
    get {
      string tmp = messageBuffer.ToString();
      messageBuffer.Length = 0;
      return tmp;
    }
  }

  public bool Eval(string code, out bool hasOutput, out object output) {
    EditorApplication.LockReloadAssemblies();
    bool status = false;
    try {
      status = Evaluator.Evaluate(code, out output, out hasOutput) == null;
    } catch(Exception e) {
      output = new Evaluator.NoValueSet();
      hasOutput = false;
      messageBuffer.Append(e.ToString());
    }
    StringBuilder buffer = reportWriter.GetStringBuilder();
    messageBuffer.Append(buffer.ToString());
    buffer.Length = 0;

    EditorApplication.UnlockReloadAssemblies();
    return status;
  }
}

public class Shell : EditorWindow {
  //----------------------------------------------------------------------------
  // Code Execution Functionality
  //----------------------------------------------------------------------------
  private EvaluationHelper helper = new EvaluationHelper();
  
  [System.NonSerialized]
  private bool isInitialized = false;
  
  void Update() {
    if(doProcess) {
      if(helper.Init(ref isInitialized)) {
        doProcess = false;
        bool hasOutput = false;
        object output = null;
        bool success = helper.Eval(codeToProcess, out hasOutput, out output);
        if(success) {
          resetCommand = true;
          Debug.Log(codeToProcess);
          string messages = helper.Messages;
          if((messages != null) && (messages != "")) {
            Debug.LogWarning(messages);
          }

          if(hasOutput) {
            StringBuilder sb = new StringBuilder();
            PrettyPrint.PP(sb, output);
            Debug.Log(sb.ToString());
          }
        }
      } else {
        // For some reason, we weren't ready to run.
        // TODO: Make sure it's not some sort of permanent error!
      }
    }
  }
  //----------------------------------------------------------------------------


  //----------------------------------------------------------------------------
  // Code Editor Functionality
  //----------------------------------------------------------------------------
  private bool doProcess = false, useContinuationPrompt = false, resetCommand = false;
  private string codeToProcess = "";

  // WARNING: Undocumented spookiness from deep within the bowels of Unity!
  public TextEditor editorState = null;

  // Need to use menu items because otherwise we don't receive events for cmd-]
  // and cmd-[.
  [MenuItem("Edit/Indent %]", false, 256)]
  public static void IndentCommand() {
    Shell w = focusedWindow as Shell;
    if(w != null) {
      w.codeToProcess = w.Indent(w.editorState);
      w.Repaint();
    }
  }

  [MenuItem("Edit/Unindent %[", false, 256)]
  public static void UnindentCommand() {
    Shell w = focusedWindow as Shell;
    if(w != null) {
      w.codeToProcess = w.Unindent(w.editorState);
      w.Repaint();
    }
  }
  
  [MenuItem("Edit/Indent %]", true)]
  public static bool ValidateIndentCommand() {
    Shell w = focusedWindow as Shell;
    return (w != null) && (w.editorState != null);
  }

  [MenuItem("Edit/Unindent %[", true)]
  public static bool ValidateUnindentCommand() {
    Shell w = focusedWindow as Shell;
    return (w != null) && (w.editorState != null);
  }
  
  // Make our state object go away if we do, or if we lose focus, or whatnot
  // to ensure menu items disable properly regardless of possible dangling 
  // references, etc.
  public void OnDisable() { editorState = null; }
  public void OnLostFocus() { editorState = null; }
  public void OnDestroy() { editorState = null; }

  public string Indent(TextEditor editor) {
    if(editor.hasSelection) {
      string codeToIndent = editor.SelectedText;
      string[] rawLines = codeToIndent.Split('\n');
      for(int i = 0; i < rawLines.Length; i++) {
        rawLines[i] = '\t' + rawLines[i];
      }

      // Eep!  We don't want to indent a trailing empty line because that means
      // the user had a 'perfect' block selection and we're accidentally 
      // indenting the next line.  Yuck!
      if(rawLines[rawLines.Length - 1] == "\t")
        rawLines[rawLines.Length - 1] = "";

      return Paste(editor, String.Join("\n", rawLines), true);
    } else {
      string[] rawLines = codeToProcess.Split('\n');
      int counter = -1, curLine = 0;
      while((counter < editor.pos) && (curLine < rawLines.Length))
        counter += rawLines[curLine++].Length + 1; // The +1 is for the \n.

      if(counter >= editor.pos) {
        curLine--;
        rawLines[curLine] = '\t' + rawLines[curLine];
        editor.pos++;
        editor.selectPos++;
        codeToProcess = String.Join("\n", rawLines);
      }

      return codeToProcess;
    }
  }

  public string Unindent(TextEditor editor) {
    if(editor.hasSelection) {
      string codeToIndent = editor.SelectedText;
      string[] rawLines = codeToIndent.Split('\n');
      for(int i = 0; i < rawLines.Length; i++) {
        if(rawLines[i].StartsWith("\t"))
          rawLines[i] = rawLines[i].Substring(1);
      }
      return Paste(editor, String.Join("\n", rawLines), true);
    } else {
      string[] rawLines = codeToProcess.Split('\n');
      int counter = 0, curLine = 0;
      while((counter < editor.pos) && (curLine < rawLines.Length))
        counter += rawLines[curLine++].Length + 1; // The +1 is for the \n.

      if(counter >= editor.pos) {
        // If counter == editor.pos, then the cursor is at the beginning of a 
        // line and we run into a couple annoying issues where the logic here 
        // acts as though it should be operating on the previous line (OY!).
        // SO.  To that end, we treat that as a bit of a special-case.  We 
        // don't decrement the current line counter if cursor is at start of 
        // line:
        if(counter > editor.pos) curLine--;
        if(rawLines[curLine].StartsWith("\t")) {
          rawLines[curLine] = rawLines[curLine].Substring(1);
          // AAAAAAAAAND, we don't try to unindent the cursor.  Although, truth
          // be told, we should probably preserve the TextMate-esaue edit 
          // behavior of having the cursor not move when changing indentation.
          if(counter > editor.pos) {
            editor.pos--;
            editor.selectPos--;
          }
          codeToProcess = String.Join("\n", rawLines);
        }
      }

      return codeToProcess;
    }
  }

  public string Paste(TextEditor editor, string textToPaste, bool continueSelection) {
    // The user can select from right-to-left and Unity gives us data that's
    // different than if they selected left-to-right.  That can be handy, but
    // here we just want to know substring indexes to slice out.
    int startAt = Mathf.Min(editor.pos, editor.selectPos);
    int endAt = Mathf.Max(editor.pos, editor.selectPos);
    string prefix = "", suffix = "";
    if(startAt > 0)
      prefix = editor.content.text.Substring(0, startAt);
    if(endAt < editor.content.text.Length)
      suffix = editor.content.text.Substring(endAt);
    string newCorpus = prefix + textToPaste + suffix;
    
    editor.content.text = newCorpus;
    if(continueSelection) {
      if(editor.pos > editor.selectPos)
        editor.pos = prefix.Length + textToPaste.Length;
      else
        editor.selectPos = prefix.Length + textToPaste.Length;
    } else
      editor.pos = editor.selectPos = prefix.Length + textToPaste.Length;
    return newCorpus;
  }

  public string Cut(TextEditor editor) {
    EditorGUIUtility.systemCopyBuffer = editor.SelectedText;

    // The user can select from right-to-left and Unity gives us data that's
    // different than if they selected left-to-right.  That can be handy, but
    // here we just want to know substring indexes to slice out.
    int startAt = Mathf.Min(editor.pos, editor.selectPos);
    int endAt = Mathf.Max(editor.pos, editor.selectPos);
    string prefix = "", suffix = "";
    if(startAt > 0)
      prefix = editor.content.text.Substring(0, startAt);
    if(endAt < editor.content.text.Length)
      suffix = editor.content.text.Substring(endAt);
    string newCorpus = prefix + suffix;
    
    editor.content.text = newCorpus;
    editor.pos = editor.selectPos = prefix.Length;
    return newCorpus;
  }

  // Handy-dandy method to deal with keyboard inputs which we get as actual 
  // events.  Basically lets us deal with copy & paste, etc which GUI.TextArea
  // ordinarily does not support.
  private void FilterEditorInputs() {
    Event evt = Event.current;
    if(focusedWindow == this) {
      // Only attempt to grab this if our window has focus in order to make 
      // indent/unindent menu items behave sanely.
      int editorId = GUIUtility.keyboardControl;
      editorState = GUIUtility.QueryStateObject(typeof(System.Object), editorId) as TextEditor;
      if(editorState == null) return;
    } else {
      return;
    }

    if(doProcess) {
      // If we're waiting for a command to run, don't muck with the text!
// HACK: Disabling this to avoid errors in Unity3.  Ugh.  Basically, for some 
// reason the GUILayout.FlexibleSpace() ending the history panel goes bonkers
// if we do this (and, presumably, there is no history -- based on the error it
// reports).
//      evt.Use();
      return;
    }

    if(evt.isKey) {
      if(evt.type == EventType.KeyDown) {
        // KeyDown gets the key press + repeating.  We only care about a few 
        // things...
        if(evt.functionKey) {
          // TODO: Make sure we don't have modifier keys pressed...
          
          // TODO: Proper edit-history support!
          if(evt.keyCode == KeyCode.UpArrow) {
            // TODO: If we're at the top of the input, move to the previous 
            // TODO: history item.  If the current item is the last history item, 
            // TODO: update the history with changes?
//            Debug.Log("UP");
          } else if(evt.keyCode == KeyCode.DownArrow) {
            // TODO: If we're at the bottom of the input, move to the previous 
            // TODO: history item.  If the current item is the last history item, 
            // TODO: update the history with changes?
//            Debug.Log("DOWN");
//          } else {
//            Debug.Log("{OTHER:" + evt.keyCode + "}");
          }
        } else if(evt.keyCode == KeyCode.Return) {
          // TODO: Only insert the return IF executing the code fails.

          // For now, just try to execute it...
          doProcess = true;
          useContinuationPrompt = true; // In case we fail.
        } else if(evt.keyCode == KeyCode.Tab) {
          // Unity doesn't like using tab for actual editing.  We're gonna 
          // change that.  So here we inject a tab, and later we'll deal with 
          // focus issues.
          codeToProcess = Paste(editorState, "\t", false);
        }
      }
    } else if(evt.type == EventType.ValidateCommand) {
      switch(evt.commandName) {
        case "SelectAll":
        case "Paste": 
          // Always allowed to muck with selection or paste stuff...
          evt.Use(); 
          break;
        case "Copy": 
        case "Cut":
          // ... but can only copy & cut when we have a selection.
          if(editorState.hasSelection) evt.Use(); 
          break;
        default:
          // If we need to suss out other commands to support...
//          Debug.Log("Validate: " + evt.commandName);
          break;
      }
    } else if(evt.type == EventType.ExecuteCommand) {
      switch(evt.commandName) {
        // A couple TextEditor functions actually work, so use them...
        case "SelectAll": editorState.SelectAll(); break;
        case "Copy": editorState.Copy(); break;
        // But some don't.
        case "Paste": 
          // Manually paste.  Keeping Use() out of the Paste() method so we can
          // re-use the functionality elsewhere.
          codeToProcess = Paste(editorState, EditorGUIUtility.systemCopyBuffer, false);
          evt.Use();
          break;
        case "Cut": 
          // Ditto -- manual cut.
          codeToProcess = Cut(editorState);
          evt.Use();
          break;
      }
    }
  }

  private void ForceFocus(string selectedControl, string desiredControl) {
    // Now here's how we deal with tabbing and hitting enter and whatnot.
    // Basically, if we're the current editor window we assume that we always
    // want the editor to have focus.  BUT there's a gotcha!  If we just blindly
    // plow through with this, we'll wind up interfering with copy/paste/etc.
    // It SEEMS to be the case that if we constrain mucking with the focus until
    // the repaint event that stuff Just Works<tm>.
    //
    // The only issue remaining after that is the actual selection -- mucking 
    // with the focus will cause it to get reset.  So we need to capture and 
    // restore it.
    if(focusedWindow == this) {
      if((Event.current != null) && (Event.current.type == EventType.Repaint)) {
        if(selectedControl != desiredControl) {
          int p = 0, sp = 0;
          if(editorState != null) {
            p = editorState.pos;
            sp = editorState.selectPos;
          }
          GUI.FocusControl(desiredControl);
          if(editorState != null) {
            editorState.pos = p;
            editorState.selectPos = sp;
          }
        }
      }
    }
  }

  private void HandleInputFocusAndStateForEditor() {
    string selectedControl = GUI.GetNameOfFocusedControl();
    ForceFocus(selectedControl, editorControlName);
    if(selectedControl == editorControlName) FilterEditorInputs();

    if(resetCommand) {
      resetCommand = false;
      useContinuationPrompt = false;
      codeToProcess = "";
    }
  }

  private void ShowEditor() {
    // TODO: Suss out scrolling and the like.
    GUILayout.BeginHorizontal();
      EditorGUILayout.BeginVertical(GUILayout.Width(35));
        GUILayout.Label(useContinuationPrompt ? "cont>" : "---->", EditorStyles.wordWrappedLabel, GUILayout.Width(35));
//        if(GUILayout.Button("Clr")) {
//          history.Clear();
//        }
      EditorGUILayout.EndVertical();

      // This is a WAG about Unity's box model.  Seems to work though, so... 
      // yeah.
      float effectiveWidgetHeight = 7 * GUI.skin.label.lineHeight
//        + GUI.skin.label.margin.top + GUI.skin.label.margin.bottom
        + GUI.skin.label.padding.top + GUI.skin.label.padding.bottom
      ;

      GUI.SetNextControlName(editorControlName);
      codeToProcess = GUILayout.TextArea(codeToProcess, GUILayout.ExpandWidth(true), GUILayout.Height(effectiveWidgetHeight));
    GUILayout.EndHorizontal();
  }

  private const string editorControlName = "REPLEditor";
  //----------------------------------------------------------------------------


  //----------------------------------------------------------------------------
  // Tying It All Together...
  //----------------------------------------------------------------------------
  void OnGUI() {
    // TODO: Turn history and editor components into more general, reusable GUI 
    // TODO: widgets.
    HandleInputFocusAndStateForEditor();

    ShowEditor();
  }

  [MenuItem("Window/REPL/Shell")]
  static void Init() {
    Shell window = (Shell)EditorWindow.GetWindow(typeof(Shell));
    window.Show();
  }
}