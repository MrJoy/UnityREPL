//----------------------------------------------------------------------------------------------------------------------
//  Shell
//  Copyright 2009-2014 Jon Frisby
//  All rights reserved
//
//----------------------------------------------------------------------------------------------------------------------
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
// TODO: Format Unity objects more gracefully.
// TODO: Turn editor components into more general, reusable GUI widgets.
// TODO: Suss out undo and wrap code execution accordingly.
// TODO: Suss out undo and wrap editor accordingly.
// TODO: Make use of EditorWindow.minSize/EditorWindow.maxSize.
//----------------------------------------------------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Mono.CSharp;

// TODO:
//  static string[] Evaluator.GetCompletions(string input, out string prefix) <-----------------------------------------

public class Shell : EditorWindow {
  //--------------------------------------------------------------------------------------------------------------------
  // Constants, specified here to keep things DRY.
  //--------------------------------------------------------------------------------------------------------------------
  public const string VERSION             = "2.0.0",
                      COPYRIGHT           = "(C) Copyright 2009-2014 Jon Frisby\nAll rights reserved",
                      MAIN_PROMPT         = "---->",
                      CONTINUATION_PROMPT = "cont>";

  //--------------------------------------------------------------------------------------------------------------------
  // Code Execution Functionality
  //--------------------------------------------------------------------------------------------------------------------
  private EvaluationHelper helper = new EvaluationHelper();

  [System.NonSerialized]
  private bool isInitialized = false;

  public void Update() {
    if(doProcess) {
      if(helper.Init(ref isInitialized)) {
        doProcess = false;
        resetCommand = helper.Eval(codeToProcess);
        if(!resetCommand) {
          // Continue with that enter the user pressed...  Yes, this is an ugly
          // way to handle it.
          codeToProcess = Paste(editorState, "\n", false);
        }
      } else {
        // For some reason, we weren't ready to run.
        // TODO: Make sure it's not some sort of permanent error!
      }
    }
  }
  //--------------------------------------------------------------------------------------------------------------------


  //--------------------------------------------------------------------------------------------------------------------
  // Code Editor Functionality
  //--------------------------------------------------------------------------------------------------------------------
  private bool        doProcess             = false,
                      useContinuationPrompt = false,
                      resetCommand          = false;
  private string      codeToProcess         = "";
  public  TextEditor  editorState           = null; // WARNING: Undocumented spookiness from deep within the bowels
                                                    // of Unity!

  // Need to use menu items because otherwise we don't receive events for cmd-] and cmd-[.
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

  // Make our state object go away if we do, or if we lose focus, or whatnot to ensure menu items disable properly
  // regardless of possible dangling references, etc.
  public void OnDisable()   { editorState = null; }
  public void OnLostFocus() { editorState = null; }
  public void OnDestroy()   { editorState = null; }

  protected void FindSelectionBounds(TextEditor editor, string[] rawLines, out int startLine, out int endLine, 
                                     out bool startAtBOL, out bool endAtBOL) {
    bool      selectingBackwards  = editor.pos < editor.selectPos;
    int       selectionStart      = selectingBackwards ? editor.pos : editor.selectPos,
              selectionEnd        = selectingBackwards ? editor.selectPos : editor.pos,
              counter             = 0,
              curLine             = 0;

    startLine   = endLine   = -1;
    startAtBOL  = endAtBOL  = false;
    while((counter <= selectionEnd) && (curLine < rawLines.Length)) {
      int curLineLen = rawLines[curLine].Length;
//Debug.Log(">>>" + startLine + ","+counter+","+selectionStart+","+curLine);
      if(startLine == -1 && (counter + curLineLen) >= selectionStart) {
        startLine   = curLine;
        startAtBOL  = counter == selectionStart;
      }
      if(endLine == -1 && (counter + curLineLen) >= selectionEnd) {
        endLine   = curLine;
        endAtBOL  = counter == selectionEnd;
      }
      counter += rawLines[curLine++].Length + 1; // The +1 is for the \n.
    }
    if(startLine == -1)     startLine = curLine - 1;
    if(endLine == -1)       endLine   = curLine - 1;
    if(endAtBOL)            endLine--; // Don't shift end of block-selection over on unindent.
    if(endLine < startLine) endLine = startLine;
//Debug.Log(startAtBOL + "/" + endAtBOL + "; " + selectionStart + ".." + selectionEnd + "; " + startLine + ".." + endLine);
  }

  // TODO: Need to handle multiple spaces as tabs...
  //
  // TODO: Make sure we're using a FIXED WIDTH FONT. >.<
  //
  // TODO: Indent/unindent whole lines only -- not in middle of string!
  public string Indent(TextEditor editor) {
    string[]  rawLines            = codeToProcess.Split('\n');
    bool      selectingBackwards  = editor.pos < editor.selectPos,
              startAtBOL,
              endAtBOL;
    int       startLine,
              endLine;
    FindSelectionBounds(editor, rawLines, out startLine, out endLine, out startAtBOL, out endAtBOL);

    for(int i = startLine; i <= endLine; i++)
      rawLines[i] = '\t' + rawLines[i]; // TODO: Make space vs. tab indentation configurable.

    int endShift = endLine - startLine;
    endShift++;
    // Shift the selection to compensate for the tabs...
    if(editor.pos != editor.selectPos) {
      editor.pos         += selectingBackwards ? 1        : endShift;
      editor.selectPos   += selectingBackwards ? endShift : 1;
      if(startAtBOL) {
        editor.pos       -= selectingBackwards ? 1 : 0;
        editor.selectPos -= selectingBackwards ? 0 : 1;
      }
      if(!endAtBOL) {
        editor.pos       += selectingBackwards ? 0 : 1;
        editor.selectPos += selectingBackwards ? 1 : 0;
      }
    } else {
      editor.pos         += endShift;
      editor.selectPos   += endShift;
    }


    codeToProcess         = String.Join("\n", rawLines);
    return codeToProcess;
  }

  public string Unindent(TextEditor editor) {
    string[]  rawLines            = codeToProcess.Split('\n');
    bool      selectingBackwards  = editor.pos < editor.selectPos,
              startAtBOL,
              endAtBOL;
    int       startLine,
              endLine;
    FindSelectionBounds(editor, rawLines, out startLine, out endLine, out startAtBOL, out endAtBOL);

    int startDeletions = 0, endDeletions = 0;
    for(int i = startLine; i <= endLine; i++) {
      if(rawLines[i].StartsWith("\t")) {
        rawLines[i] = rawLines[i].Substring(1);
        if(i == 0 && !startAtBOL)
          startDeletions++;
        endDeletions++;
      } else {
        for(int j = 0; j < 4; j++) { // TODO: make spaces-per-tab configurable!
          if(rawLines[i].StartsWith(" ")) {
            rawLines[i] = rawLines[i].Substring(1);
            if(i == 0 && !startAtBOL)
              startDeletions++;
            endDeletions++;
          } else {
            j = 4; // Don't eat a space after a non-space.
          }
        }
      }
    }

    // Shift the selection to compensate for the tabs...
    if(editor.pos != editor.selectPos) {
      editor.pos             -= selectingBackwards ? startDeletions : endDeletions;
      editor.selectPos       -= selectingBackwards ? endDeletions : startDeletions;
    } else {
      if(!startAtBOL) {
        if(endAtBOL) {
          editor.pos         -= startDeletions;
          editor.selectPos   -= startDeletions;
        } else {
          editor.pos         -= endDeletions;
          editor.selectPos   -= endDeletions;
        }
      }
    }

    codeToProcess             = String.Join("\n", rawLines);
    return codeToProcess;
  }

  public string Paste(TextEditor editor, string textToPaste, bool continueSelection) {
    // The user can select from right-to-left and Unity gives us data that's different than if they selected
    // left-to-right.  That can be handy, but here we just want to know substring indexes to slice out.
    int     startAt = Mathf.Min(editor.pos, editor.selectPos);
    int     endAt   = Mathf.Max(editor.pos, editor.selectPos);
    string  prefix  = "",
            suffix  = "";
    if(startAt > 0)
      prefix = editor.content.text.Substring(0, startAt);
    if(endAt < editor.content.text.Length)
      suffix = editor.content.text.Substring(endAt);
    string newCorpus = prefix + textToPaste + suffix;

    editor.content.text = newCorpus;
    if(continueSelection) {
      if(editor.pos > editor.selectPos)
        editor.pos        = prefix.Length + textToPaste.Length;
      else
        editor.selectPos  = prefix.Length + textToPaste.Length;
    } else
      editor.pos = editor.selectPos = prefix.Length + textToPaste.Length;
    return newCorpus;
  }

  public string Cut(TextEditor editor) {
    EditorGUIUtility.systemCopyBuffer = editor.SelectedText;

    // The user can select from right-to-left and Unity gives us data that's different than if they selected
    // left-to-right.  That can be handy, but here we just want to know substring indexes to slice out.
    int     startAt = Mathf.Min(editor.pos, editor.selectPos),
            endAt   = Mathf.Max(editor.pos, editor.selectPos);
    string  prefix  = "",
            suffix  = "";
    if(startAt > 0)
      prefix = editor.content.text.Substring(0, startAt);
    if(endAt < editor.content.text.Length)
      suffix = editor.content.text.Substring(endAt);
    string newCorpus = prefix + suffix;

    editor.content.text = newCorpus;
    editor.pos          = editor.selectPos = prefix.Length;
    return newCorpus;
  }

  // Handy-dandy method to deal with keyboard inputs which we get as actual events.  Basically lets us deal with copy
  // & paste, etc which GUI.TextArea ordinarily does not support.
  private void FilterEditorInputs() {
    UnityEngine.Event evt = UnityEngine.Event.current;
    if(focusedWindow == this) {
      // Only attempt to grab this if our window has focus in order to make indent/unindent menu items behave sanely.
      int editorId = GUIUtility.keyboardControl;
      try {
        editorState = GUIUtility.QueryStateObject(typeof(System.Object), editorId) as TextEditor;
      } catch(KeyNotFoundException) {
        // This can happen if the code is reloaded out from under us.
      }
      if(editorState == null)
        return;
    } else {
      return;
    }

    if(doProcess) {
      // If we're waiting for a command to run, don't muck with the text!
      if(evt.isKey)
        evt.Use();
      return;
    }

    if(evt.isKey) {
      if(evt.type == EventType.KeyDown) {
        // KeyDown gets the key press + repeating.  We only care about a few things...
        if(evt.functionKey) {
          // TODO: Make sure we don't have modifier keys pressed...

          // TODO: Proper edit-history support!
          if(evt.keyCode == KeyCode.UpArrow) {
            // TODO: If we're at the top of the input, move to the previous history item.  If the current item is the
            // TODO: last history item, update the history with changes?
          } else if(evt.keyCode == KeyCode.DownArrow) {
            // TODO: If we're at the bottom of the input, move to the previous history item.  If the current item is the
            // TODO: last history item, update the history with changes?
          }
        } else if(evt.keyCode == KeyCode.Return) {
          // TODO: Do we only want to do this only when the cursor is at the end of the input?  (Avoids unexpectedly
          // TODO: putting newlines in the middle of peoples' input...)
          if(evt.shift)
            codeToProcess = Paste(editorState, "\n", false);
          else
            doProcess = true;
          useContinuationPrompt = true; // In case we fail.
        } else if(evt.keyCode == KeyCode.Tab) {
          // Unity doesn't like using tab for actual editing.  We're gonna change that.  So here we inject a tab, and
          // later we'll deal with focus issues.
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
      // But some don't.
      case "Paste":
        // Manually paste.  Keeping Use() out of the Paste() method so we can re-use the functionality elsewhere.
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
    // Now here's how we deal with tabbing and hitting enter and whatnot.  Basically, if we're the current editor window
    // we assume that we always want the editor to have focus.  BUT there's a gotcha!  If we just blindly plow through
    // with this, we'll wind up interfering with copy/paste/etc.  It SEEMS to be the case that if we constrain mucking
    // with the focus until the repaint event that stuff Just Works<tm>.
    //
    // The only issue remaining after that is the actual selection -- mucking with the focus will cause it to get reset.
    // So we need to capture and restore it.
    if(focusedWindow == this) {
      UnityEngine.Event current = UnityEngine.Event.current;
      if(current == null)
        return;
      if(current.type != EventType.Repaint)
        return;

      if(selectedControl != desiredControl) {
        int p   = 0,
            sp  = 0;
        if(editorState != null) {
          p   = editorState.pos;
          sp  = editorState.selectPos;
        }
        GUI.FocusControl(desiredControl);
        if(editorState != null) {
          editorState.pos       = p;
          editorState.selectPos = sp;
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
      resetCommand          = false;
      useContinuationPrompt = false;
      codeToProcess         = "";
    }
  }

  private NumberedEditorState lnEditorState = new NumberedEditorState();

  private void ShowEditor() {
    GUILayout.BeginHorizontal();
      GUILayout.Label(useContinuationPrompt ? Shell.CONTINUATION_PROMPT : Shell.MAIN_PROMPT, EditorStyles.wordWrappedLabel, GUILayout.Width(37));

      lnEditorState.text  = codeToProcess;
      lnEditorState       = UnityREPLHelper.NumberedTextArea(editorControlName, lnEditorState);
      codeToProcess       = lnEditorState.text;
    GUILayout.EndHorizontal();
  }

  private Dictionary<string, Tuple<FieldSpec, FieldInfo>> fields          = null;
  public Vector2                                          scrollPosition  = Vector2.zero;

  private void ShowVars() {
    if(fields == null)
      fields = EvaluatorProxy.fields;

    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
      EditorGUI.indentLevel++;
      GUILayout.BeginHorizontal();
        GUILayout.Space(EditorGUI.indentLevel * 14);
        GUILayout.BeginVertical();
          // TODO: This is gonna be WAY inefficient *AND* ugly.  Need a better way to handle tabular data, and need a
          // TODO: way to track what has/hasn't changed here.
          if(fields != null) {
            StringBuilder tmp = new StringBuilder();
            foreach(var kvp in fields) {
              var field = kvp.Value;
              GUILayout.BeginHorizontal();
                GUILayout.Label(TypeManagerProxy.CSharpName(field.Item1.MemberType));
                GUILayout.Space(10);
                GUILayout.Label(kvp.Key);
                GUILayout.FlexibleSpace();
                PrettyPrint.PP(tmp, field.Item2.GetValue(null));
                GUILayout.Label(tmp.ToString());
                tmp.Length = 0;
              GUILayout.EndHorizontal();
            }
          }
        GUILayout.EndVertical();
      GUILayout.EndHorizontal();
      EditorGUI.indentLevel--;
    EditorGUILayout.EndScrollView();
  }

  private const string editorControlName = "REPLEditor";
  //--------------------------------------------------------------------------------------------------------------------

  //--------------------------------------------------------------------------------------------------------------------
  // Help Screen
  //--------------------------------------------------------------------------------------------------------------------
  public Vector2 helpScrollPosition = Vector2.zero;
  private bool showQuickStart = true, showEditing = true, showLogging = true,
               showShortcuts = true, showLocals = true, showKnownIssues = true,
               showExpressions = true;
  public void ShowHelp() {
    helpScrollPosition = EditorGUILayout.BeginScrollView(helpScrollPosition);
      GUILayout.Label("UnityREPL v." + Shell.VERSION, HelpStyles.Header);
      GUILayout.Label(Shell.COPYRIGHT, HelpStyles.Header);

      GUILayout.Label(GUIContent.none, HelpStyles.Content);

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
  //--------------------------------------------------------------------------------------------------------------------

  //--------------------------------------------------------------------------------------------------------------------
  // Tying It All Together...
  //--------------------------------------------------------------------------------------------------------------------
  public bool showVars = true, filterTraces = true, showHelp = false;
  // TODO: Save pane sizing states...
  // private VerticalPaneState paneConfiguration = new VerticalPaneState() {
  //   minPaneHeightTop = 65,
  //   minPaneHeightBottom = 100
  // };
  public void OnGUI() {
    HandleInputFocusAndStateForEditor();

    EditorGUILayoutToolbar.Begin();
      // if(GUILayout.Button("Clear Log", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
      //   Debug.ClearDeveloperConsole();

      showVars = GUILayout.Toggle(showVars, "Locals", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

      EditorGUILayoutToolbar.FlexibleSpace();

      showHelp = GUILayout.Toggle(showHelp, "?", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));

    EditorGUILayoutToolbar.End();

    if(showHelp) {
      ShowHelp();
    } else {
      ShowEditor();
      if(showVars)
        ShowVars();
    }
  }

  [MenuItem("Window/C# Shell #%r")]
  public static void Init() {
    Shell window = (Shell)EditorWindow.GetWindow(typeof(Shell));
    window.title = "C# Shell";
    window.Show();
  }
}
