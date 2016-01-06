//-----------------------------------------------------------------
// Main Unity shell for REPL.
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
// TODO: Format Unity objects more gracefully.
// TODO: Turn editor components into more general, reusable GUI widgets.
// TODO: Suss out undo and wrap code execution accordingly.
// TODO: Suss out undo and wrap editor accordingly.
// TODO: Make use of EditorWindow.minSize/EditorWindow.maxSize.
//-----------------------------------------------------------------
#if UNITY_5_2 || UNITY_5_1 || UNITY_5_0 || UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3 || UNITY_4_2 || UNITY_4_1 || UNITY_4_0_1 || UNITY_4_0
  #define UNITY_5_3_PLUS
#endif

using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

// TODO:
//  static string[] Evaluator.GetCompletions(string input, out string prefix) <-----

public class Shell : EditorWindow {
  //----------------------------------------------------------------------------
  // Constants, specified here to keep things DRY.
  //----------------------------------------------------------------------------
  public const string VERSION = "2.0.1",
                      COPYRIGHT = "(C) Copyright 2009-2015 Jon Frisby\nAll rights reserved",

                      MAIN_PROMPT = "---->",
                      CONTINUATION_PROMPT = "cont>";

  //----------------------------------------------------------------------------
  // Code Execution Functionality
  //----------------------------------------------------------------------------
  private EvaluationHelper helper = new EvaluationHelper();
  [System.NonSerialized]
  private bool isInitialized = false;

  public void Update() {
    if(doProcess) {
      // Don't be executing code when we're about to reload it.  Not sure this is
      // actually needed but seems prudent to be wary of it.
      if(EditorApplication.isCompiling)
        return;

      helper.Init(ref isInitialized);
      doProcess = false;
      bool compiledCorrectly = helper.Eval(codeToProcess);
      if(compiledCorrectly)
        resetCommand = true;
      else {
        // Continue with what enter the user pressed...  Yes, this is an ugly
        // way to handle it.
        codeToProcess = Paste(editorState, "\n", false);
      }
    }
  }
  //----------------------------------------------------------------------------


  //----------------------------------------------------------------------------
  // Code Editor Functionality
  //----------------------------------------------------------------------------
  private bool doProcess = false,
               useContinuationPrompt = false,
               resetCommand = false;
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
      for(int i = 0; i < rawLines.Length; i++)
        rawLines[i] = '\t' + rawLines[i];

      // Eep!  We don't want to indent a trailing empty line because that means
      // the user had a 'perfect' block selection and we're accidentally
      // indenting the next line.  Yuck!
      if(rawLines[rawLines.Length - 1] == "\t")
        rawLines[rawLines.Length - 1] = "";

      return Paste(editor, String.Join("\n", rawLines), true);
    } else {
      string[] rawLines = codeToProcess.Split('\n');
      int counter = -1, curLine = 0;
      while((counter < editor.cursorIndex) && (curLine < rawLines.Length))
        counter += rawLines[curLine++].Length + 1; // The +1 is for the \n.

      if(counter >= editor.cursorIndex) {
        curLine--;
        rawLines[curLine] = '\t' + rawLines[curLine];
        editor.cursorIndex++;
        editor.selectIndex++;
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
      while((counter < editor.cursorIndex) && (curLine < rawLines.Length))
        counter += rawLines[curLine++].Length + 1; // The +1 is for the \n.

      if(counter >= editor.cursorIndex) {
        // If counter == editor.pos, then the cursor is at the beginning of a
        // line and we run into a couple annoying issues where the logic here
        // acts as though it should be operating on the previous line (OY!).
        // SO.  To that end, we treat that as a bit of a special-case.  We
        // don't decrement the current line counter if cursor is at start of
        // line:
        if(counter > editor.cursorIndex)
          curLine--;
        if(rawLines[curLine].StartsWith("\t")) {
          rawLines[curLine] = rawLines[curLine].Substring(1);
          // AAAAAAAAAND, we don't try to unindent the cursor.  Although, truth
          // be told, we should probably preserve the TextMate-esaue edit
          // behavior of having the cursor not move when changing indentation.
          if(counter > editor.cursorIndex) {
            editor.cursorIndex--;
            editor.selectIndex--;
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
    int startAt = Mathf.Min(editor.cursorIndex, editor.selectIndex),
        endAt = Mathf.Max(editor.cursorIndex, editor.selectIndex);
    string prefix = "",
           suffix = "";
#if UNITY_5_3_PLUS
    if(startAt > 0)
      prefix = editor.content.text.Substring(0, startAt);
    if(endAt < editor.content.text.Length)
      suffix = editor.content.text.Substring(endAt);
#else
    if(startAt > 0)
      prefix = editor.text.Substring(0, startAt);
    if(endAt < editor.text.Length)
      suffix = editor.text.Substring(endAt);
#endif
    string newCorpus = prefix + textToPaste + suffix;

#if UNITY_5_3_PLUS
    editor.content.text = newCorpus;
#else
    editor.text = newCorpus;
#endif
    if(continueSelection) {
      if(editor.cursorIndex > editor.selectIndex)
        editor.cursorIndex = prefix.Length + textToPaste.Length;
      else
        editor.selectIndex = prefix.Length + textToPaste.Length;
    } else
      editor.cursorIndex = editor.selectIndex = prefix.Length + textToPaste.Length;
    return newCorpus;
  }

  public string Cut(TextEditor editor) {
    EditorGUIUtility.systemCopyBuffer = editor.SelectedText;

    // The user can select from right-to-left and Unity gives us data that's
    // different than if they selected left-to-right.  That can be handy, but
    // here we just want to know substring indexes to slice out.
    int startAt = Mathf.Min(editor.cursorIndex, editor.selectIndex),
        endAt = Mathf.Max(editor.cursorIndex, editor.selectIndex);
    string prefix = "",
           suffix = "";

#if UNITY_5_3_PLUS
    if(startAt > 0)
      prefix = editor.content.text.Substring(0, startAt);
    if(endAt < editor.content.text.Length)
      suffix = editor.content.text.Substring(endAt);
#else
    if(startAt > 0)
      prefix = editor.text.Substring(0, startAt);
    if(endAt < editor.text.Length)
      suffix = editor.text.Substring(endAt);
#endif
    string newCorpus = prefix + suffix;

#if UNITY_5_3_PLUS
    editor.content.text = newCorpus;
#else
    editor.text = newCorpus;
#endif
    editor.cursorIndex = editor.selectIndex = prefix.Length;
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
      try {
        editorState = GUIUtility.QueryStateObject(typeof(System.Object), editorId) as TextEditor;
      } catch(KeyNotFoundException) {
        // Ignoring because this seems to only mean that no such object was found.
      }
      if(editorState == null)
        return;
    } else
      return;

    if(doProcess) {
      // If we're waiting for a command to run, don't muck with the text!
      if(evt.isKey)
        evt.Use();
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
          } else if(evt.keyCode == KeyCode.DownArrow) {
            // TODO: If we're at the bottom of the input, move to the previous
            // TODO: history item.  If the current item is the last history item,
            // TODO: update the history with changes?
          }
        } else if(evt.keyCode == KeyCode.Return) {
          // TODO: Do we only want to do this only when the cursor is at the
          // TODO: end of the input?  (Avoids unexpectedly putting newlines in
          // TODO: the middle of peoples' input...)
          if(Event.current.shift)
            codeToProcess = Paste(editorState, "\n", false);
          else
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
        if(editorState.hasSelection)
          evt.Use();
        break;
      default:
        // If we need to suss out other commands to support...
        // Debug.Log("Validate: " + evt.commandName);
        break;
      }
    } else if(evt.type == EventType.ExecuteCommand) {
      switch(evt.commandName) {
      // A couple TextEditor functions actually work, so use them...
      case "SelectAll":
        editorState.SelectAll();
        break;
      case "Copy":
        editorState.Copy();
        break;
      // But some don't:
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
            p = editorState.cursorIndex;
            sp = editorState.selectIndex;
          }
          GUI.FocusControl(desiredControl);
          if(editorState != null) {
            editorState.cursorIndex = p;
            editorState.selectIndex = sp;
          }
        }
      }
    }
  }

  private void HandleInputFocusAndStateForEditor() {
    string selectedControl = GUI.GetNameOfFocusedControl();
    ForceFocus(selectedControl, editorControlName);
    if(selectedControl == editorControlName)
      FilterEditorInputs();
    if(resetCommand) {
      resetCommand = false;
      useContinuationPrompt = false;
      codeToProcess = "";
    }
  }

  private NumberedEditorState lnEditorState = new NumberedEditorState();

  private void ShowEditor() {
    GUILayout.BeginHorizontal();
    GUILayout.Label(useContinuationPrompt ? Shell.CONTINUATION_PROMPT : Shell.MAIN_PROMPT, EditorStyles.wordWrappedLabel, GUILayout.Width(37));

    lnEditorState.text = codeToProcess;
    lnEditorState = UnityREPLHelper.NumberedTextArea(editorControlName, lnEditorState);
    codeToProcess = lnEditorState.text;
    GUILayout.EndHorizontal();
  }

  private Hashtable fields = null;
  public Vector2 scrollPosition = Vector2.zero;

  private void ShowVars() {
    if(fields == null)
      fields = EvaluatorProxy.fields;

    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
    EditorGUI.indentLevel++;
    GUILayout.BeginHorizontal();
    GUILayout.Space(EditorGUI.indentLevel * 14);
    GUILayout.BeginVertical();
    // TODO: This is gonna be WAY inefficient *AND* ugly.  Need a better
    // TODO: way to handle tabular data, and need a way to track what
    // TODO: has/hasn't changed here.
    StringBuilder tmp = new StringBuilder();
    foreach(DictionaryEntry kvp in fields) {
      FieldInfo field = (FieldInfo)kvp.Value;
      GUILayout.BeginHorizontal();
      GUILayout.Label(TypeManagerProxy.CSharpName(field.FieldType));
      GUILayout.Space(10);
      GUILayout.Label((string)kvp.Key);
      GUILayout.FlexibleSpace();
      PrettyPrint.PP(tmp, field.GetValue(null));
      GUILayout.Label(tmp.ToString());
      tmp.Length = 0;
      GUILayout.EndHorizontal();
    }
    GUILayout.EndVertical();
    GUILayout.EndHorizontal();
    EditorGUI.indentLevel--;
    EditorGUILayout.EndScrollView();
  }

  private const string editorControlName = "REPLEditor";
  //----------------------------------------------------------------------------

  //----------------------------------------------------------------------------
  // Help Screen
  //----------------------------------------------------------------------------
  public Vector2 helpScrollPosition = Vector2.zero;
  private bool showQuickStart = true, showEditing = true, showLogging = true,
               showShortcuts = true, showLocals = true, showKnownIssues = true,
               showExpressions = true, showLanguageFeatures = true;

  public void ShowHelp() {
    helpScrollPosition = EditorGUILayout.BeginScrollView(helpScrollPosition);
    GUILayout.Label("UnityREPL v." + Shell.VERSION, HelpStyles.Header);
    GUILayout.Label(Shell.COPYRIGHT, HelpStyles.Header);

    GUILayout.Label("", HelpStyles.Content);

    showQuickStart = EditorGUILayout.Foldout(showQuickStart, "Quick Start", HelpStyles.SubHeader);
    if(showQuickStart) {
      GUILayout.Label("Type your C# code into the main text area, and press <enter> when you're done." +
                      "  If your code has an error, or is incomplete, the prompt will change from '" + Shell.MAIN_PROMPT +
                      "' to '" + Shell.CONTINUATION_PROMPT + "', and you'll be allowed to continue typing." +
                      "  If there's a logic-error that the C# compiler can detect at compile-time, it will be" +
                      " written to the log pane as well.\n\nIf your code is correct, it will be executed immediately upon" +
                      " pressing <enter>.\n\nPlease see the Known Issues section below, to avoid some frustrating corner-cases!",
                      HelpStyles.Content);
      GUILayout.Label("", HelpStyles.Content);
    }

    showExpressions = EditorGUILayout.Foldout(showExpressions, "Expressions", HelpStyles.SubHeader);
    if(showExpressions) {
      GUILayout.Label("If you begin your code with an '=', and omit a trailing ';', then UnityREPL" +
                      " will behave a little differently than usual, and will evaluate everything after the '=' as" +
                      " an expression.  The log pane will show both the expression you entered, and the result of the" +
                      " evaluation.  This can be handy for a quick calculator, or for peeking at data in detail.  In" +
                      " particular, if an expression evaluates to a Type object, you will be shown a the interface exposed" +
                      " by that type in pseudo-C# syntax.  Try these out:", HelpStyles.Content);

      GUILayout.Label("= 4 * 20", HelpStyles.Code);
      GUILayout.Label("= typeof(EditorApplication)", HelpStyles.Code);

      GUILayout.Label("", HelpStyles.Content);
    }

    showLanguageFeatures = EditorGUILayout.Foldout(showLanguageFeatures, "Language Features", HelpStyles.SubHeader);
    if(showLanguageFeatures) {
      GUILayout.Label("UnityREPL implements a newer version of the C# language than Unity itself supports, unless" +
                      " you are using Unity 3.0 or newer.  You get a couple nifty features for code entered into the interactive" +
                      " editor...", HelpStyles.Content);
      GUILayout.Label("", HelpStyles.Content);
      GUILayout.Label("The 'var' keyword:", HelpStyles.Content);
      GUILayout.Label("var i = 3;", HelpStyles.Code);
      GUILayout.Label("", HelpStyles.Content);
      GUILayout.Label("Linq:", HelpStyles.Content);
      GUILayout.Label("= from f in Directory.GetFiles(Application.dataPath)\n" +
                      "  let fi = new FileInfo(f)\n" +
                      "  where fi.LastWriteTime > DateTime.Now - TimeSpan.FromDays(7)\n" +
                      "  select f", HelpStyles.Code);
      GUILayout.Label("", HelpStyles.Content);
      GUILayout.Label("Anonymous Types:", HelpStyles.Content);
      GUILayout.Label("var x = new { foo = \"blah\", bar = 123 };", HelpStyles.Code);
      GUILayout.Space(4);
      GUILayout.Label("... which you can access like so:", HelpStyles.Content);
      GUILayout.Label("= x.foo", HelpStyles.Code);
      GUILayout.Label("", HelpStyles.Content);
    }

    showEditing = EditorGUILayout.Foldout(showEditing, "Editing", HelpStyles.SubHeader);
    if(showEditing) {
      GUILayout.Label("The editor panel works like many familiar text editors, and supports using the <tab>" +
                      " key to indent, unlike normal Unity text fields.  Additionally, there are keyboard shortcuts to" +
                      " indent/unindent a single line or a selected block of lines.  Cut/Copy/Paste are also fully supported.\n\n" +
                      "If you wish to insert a line into your code, but pressing <enter> would execute it, you can press " +
                      "<shift>-<enter> to suppress execution.", HelpStyles.Content);
      GUILayout.Label("", HelpStyles.Content);
    }

    showLogging = EditorGUILayout.Foldout(showLogging, "Logging", HelpStyles.SubHeader);
    if(showLogging) {
      GUILayout.Label("Any output sent to Debug.Log, Debug.LogWarning, or Debug.LogError during execution of" +
                      " your code is captured and showed in the log pane, as well as the normal Unity console view.  You can" +
                      " disable this view by disabling the 'Log' toggle on the toolbar.  You can also filter certain stack" +
                      " trace elements that are unlikely to be useful by enabling the 'Filter' toggle on the toolbar.  Any" +
                      " code that takes the form of an expression will be evaluated and the result of the expression will" +
                      " appear below it in the log in green.  Any errors or exceptions will appear in red.  Warnings in yellow." +
                      "  Normal log messages will appear in black or white depending on which Unity skin is" +
                      " enabled.\n\nIf your code spans multiple lines, or there is any form of output associated with it, a" +
                      " disclosure triangle will appear next to it, allowing you to collapse the log entry down to a single" +
                      " line.\n\nFinally, a button with a '+' on it appears next to the part of the log entry showing code" +
                      " you've executed.  Pressing this button will replace the contents of the editor field with that code," +
                      " so you can run it again.", HelpStyles.Content);
      GUILayout.Label("", HelpStyles.Content);
    }

    showLocals = EditorGUILayout.Foldout(showLocals, "Locals", HelpStyles.SubHeader);
    if(showLocals) {
      GUILayout.Label("The locals pane will show you the type, name, and current value of any variables your" +
                      " code creates.  These variables will persist across multiple snippets of code, which can be very helpful" +
                      " for breaking tasks up into separate steps.", HelpStyles.Content);
      GUILayout.Label("", HelpStyles.Content);
    }

    showShortcuts = EditorGUILayout.Foldout(showShortcuts, "Keyboard Shortcuts", HelpStyles.SubHeader);
    if(showShortcuts) {
      GUILayout.BeginHorizontal();
      if(Application.platform == RuntimePlatform.OSXEditor)
        GUILayout.Label("<cmd>-<shift>-R", HelpStyles.Shortcut);
      else
        GUILayout.Label("<ctrl>-<shift>-R", HelpStyles.Shortcut);
      GUILayout.Label("Switch to the UnityREPL window (opening one if needed).", HelpStyles.Explanation);
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      GUILayout.Label("<shift>-<enter>", HelpStyles.Shortcut);
      GUILayout.Label("Insert a new line, without submitting the code for execution.", HelpStyles.Explanation);
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      if(Application.platform == RuntimePlatform.OSXEditor)
        GUILayout.Label("<cmd>-]", HelpStyles.Shortcut);
      else
        GUILayout.Label("<ctrl>-]", HelpStyles.Shortcut);
      GUILayout.Label("If text is selected, indent all of it.  If there is no text selected, indent the current line.", HelpStyles.Explanation);
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      if(Application.platform == RuntimePlatform.OSXEditor)
        GUILayout.Label("<cmd>-[", HelpStyles.Shortcut);
      else
        GUILayout.Label("<ctrl>-[", HelpStyles.Shortcut);
      GUILayout.Label("If text is selected, un-indent all of it.  If there is no text selected, un-indent the current line.", HelpStyles.Explanation);
      GUILayout.EndHorizontal();
      GUILayout.Label("", HelpStyles.Content);
    }

    showKnownIssues = EditorGUILayout.Foldout(showKnownIssues, "Known Issues", HelpStyles.SubHeader);
    if(showKnownIssues) {
      GUILayout.Label("Any 'using' statements must be executed by themselves, as separate and individual code snippets.", HelpStyles.Content);
      GUILayout.Label("Locals are wiped when Unity recompiles your code, or when entering play-mode.", HelpStyles.Content);
      GUILayout.Label("Locals view cannot display the name of a generic type.", HelpStyles.Content);
      GUILayout.Label("", HelpStyles.Content);
    }

    GUILayout.Space(4);
    EditorGUILayout.EndScrollView();
  }
  //----------------------------------------------------------------------------

  //----------------------------------------------------------------------------
  // Tying It All Together...
  //----------------------------------------------------------------------------
  public bool showVars = true, showHelp = false;

  public void OnGUI() {
    HandleInputFocusAndStateForEditor();

    EditorGUILayoutToolbar.Begin();
    showVars = GUILayout.Toggle(showVars, "Locals", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

    EditorGUILayoutToolbar.FlexibleSpace();

    showHelp = GUILayout.Toggle(showHelp, "?", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

    EditorGUILayoutToolbar.End();

    if(showHelp)
      ShowHelp();
    else {
      ShowEditor();

      if(showVars)
        ShowVars();
    }
  }

  [MenuItem("Window/C# Shell #%r")]
  public static void Init() {
    Shell window = (Shell)EditorWindow.GetWindow(typeof(Shell));
    window.titleContent = new GUIContent("C# Shell");
    window.Show();
  }
}
