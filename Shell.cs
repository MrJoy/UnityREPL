//-----------------------------------------------------------------
//  Shell v0.8
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
// TODO: Format Unity objects more gracefully.
// TODO: Turn editor components into more general, reusable GUI widget.
// TODO: Suss out undo and wrap code execution accordingly.
// TODO: Suss out undo and wrap editor accordingly.
// TODO: Make use of EditorWindow.minSize/EditorWindow.maxSize.
//-----------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

public class Shell : EditorWindow {
  //----------------------------------------------------------------------------
  // Code Execution Functionality
  //----------------------------------------------------------------------------
  private EvaluationHelper helper = new EvaluationHelper();

  public List<LogEntry> logEntries = new List<LogEntry>();


  [System.NonSerialized]
  private bool isInitialized = false;

  [System.NonSerialized]
  private StringBuilder outputBuffer = new StringBuilder();
  public void Update() {
    if(doProcess) {
      if(helper.Init(ref isInitialized)) {
        doProcess = false;
        bool hasOutput = false;
        object output = null;
        LogEntry cmdEntry = null;
        bool compiledCorrectly = helper.Eval(logEntries, codeToProcess, out hasOutput, out output, out cmdEntry);
        if(compiledCorrectly) {
          resetCommand = true;

          if(hasOutput) {
            outputBuffer.Length = 0;
            PrettyPrint.PP(outputBuffer, output);
            cmdEntry.Add(new LogEntry() {
              logEntryType = LogEntryType.Output,
              output = outputBuffer.ToString()
            });
          }
        } else {
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
  public void OnDisable() {
    editorState = null;
    Application.RegisterLogCallback(null);
  }
  public void OnLostFocus() { editorState = null; }
  public void OnDestroy() { OnDisable(); }

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
          // TODO: Do we only want to do this only when the cursor is at the
          // TODO: end of the input?  (Avoids unexpectedly putting newlines in
          // TODO: the middle of peoples' input...)
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
      GUILayout.Label(useContinuationPrompt ? "cont>" : "---->", EditorStyles.wordWrappedLabel, GUILayout.Width(35));

      lnEditorState.text = codeToProcess;
      lnEditorState = GUIHelper.NumberedTextArea(editorControlName, lnEditorState);
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

  public Vector2 logScrollPos;
  private void ShowLog() {
    logScrollPos = EditorGUILayout.BeginScrollView(logScrollPos);
    foreach(LogEntry le in logEntries) {
      le.OnGUI();
      //GUILayout.Label(le.ToString());
    }
    EditorGUILayout.EndScrollView();
  }
  private const string editorControlName = "REPLEditor";
  //----------------------------------------------------------------------------
  protected class VerticalPaneState {
    public int id = 0;
    public bool isDraggingSplitter = false,
                isPaneHeightChanged = false;
    public float topPaneHeight = -1, initialTopPaneHeight = -1,
                 lastAvailableHeight = -1, availableHeight = 0,
                 minPaneHeightTop = 75, minPaneHeightBottom = 75;

    private float _splitterHeight = 5;
    public float splitterHeight {
      get { return _splitterHeight; }
      set {
        if(value != _splitterHeight) {
          _splitterHeight = value;
          _SplitterHeight = null;
        }
      }
    }

    private GUILayoutOption _SplitterHeight = null;
    public GUILayoutOption SplitterHeight {
      get {
        if(_SplitterHeight == null)
          _SplitterHeight = GUILayout.Height(_splitterHeight);
        return _SplitterHeight;
      }
    }

    /*
    * Unity can, apparently, recycle state objects.  In that event we want to
    * wipe the slate clean and just start over to avoid wackiness.
    */
    protected virtual void Reset(int newId) {
      id = newId;
      isDraggingSplitter = false;
      isPaneHeightChanged = false;
      topPaneHeight = -1;
      initialTopPaneHeight = -1;
      lastAvailableHeight = -1;
      availableHeight = 0;
      minPaneHeightTop = 75;
      minPaneHeightBottom = 75;
    }

    /*
    * Some aspects of our state are really just static configuration that
    * shouldn't be modified by the control, so we blindly set them if we have a
    * prototype from which to do so.
    */
    protected virtual void InitFromPrototype(int newId, VerticalPaneState prototype) {
      id = newId;
      initialTopPaneHeight = prototype.initialTopPaneHeight;
      minPaneHeightTop = prototype.minPaneHeightTop;
      minPaneHeightBottom = prototype.minPaneHeightBottom;
    }

    /*
    * This method takes care of guarding against state object recycling, and
    * ensures we pick up what we need, when we need to, from the prototype state
    * object.
    */
    public void ResolveStateToCurrentContext(int currentId, VerticalPaneState prototype) {
      if(id != currentId) {
        Reset(currentId);
      } else if(prototype != null) {
        InitFromPrototype(currentId, prototype);
      }
    }
  }


  private static VerticalPaneState vState;
  protected static void BeginVerticalPanes() {
    BeginVerticalPanes(null);
  }

  protected static void BeginVerticalPanes(VerticalPaneState prototype) {
    int id = GUIUtility.GetControlID(FocusType.Passive);
    vState = (VerticalPaneState)GUIUtility.GetStateObject(typeof(VerticalPaneState), id);
    vState.ResolveStateToCurrentContext(id, prototype);

    Rect totalArea = EditorGUILayout.BeginVertical();
      vState.availableHeight = totalArea.height - vState.splitterHeight;
      vState.isPaneHeightChanged = false;
      if(totalArea.height > 0) {
        if(vState.topPaneHeight < 0) {
          if(vState.initialTopPaneHeight < 0)
            vState.topPaneHeight = vState.availableHeight * 0.5f;
          else
            vState.topPaneHeight = vState.initialTopPaneHeight;
          vState.isPaneHeightChanged = true;
        }
        if(vState.lastAvailableHeight < 0)
          vState.lastAvailableHeight = vState.availableHeight;
        if(vState.lastAvailableHeight != vState.availableHeight) {
          vState.topPaneHeight = vState.availableHeight * (vState.topPaneHeight / vState.lastAvailableHeight);
          vState.isPaneHeightChanged = true;
        }
        vState.lastAvailableHeight = vState.availableHeight;
      }

      GUILayout.BeginVertical(GUILayout.Height(vState.topPaneHeight));
  }

  protected static void VerticalSplitter() {
    GUILayout.EndVertical();

    float availableHeightForOnePanel = vState.availableHeight - (vState.splitterHeight + vState.minPaneHeightBottom);
    Rect splitterArea = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.box, vState.SplitterHeight, GUILayout.ExpandWidth(true));
    if(splitterArea.Contains(Event.current.mousePosition) || vState.isDraggingSplitter) {
      switch(Event.current.type) {
        case EventType.MouseDown:
          vState.isDraggingSplitter = true;
          break;
        case EventType.MouseDrag:
          if(vState.isDraggingSplitter) {
            vState.topPaneHeight += Event.current.delta.y;
            vState.isPaneHeightChanged = true;
          }
          break;
        case EventType.MouseUp:
          vState.isDraggingSplitter = false;
          break;
      }
    }
    if(vState.isPaneHeightChanged) {
      if(vState.topPaneHeight < vState.minPaneHeightTop) vState.topPaneHeight = vState.minPaneHeightTop;
      if(vState.topPaneHeight >= availableHeightForOnePanel) vState.topPaneHeight = availableHeightForOnePanel;
      if(EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
    }
    GUI.Label(splitterArea, vSplitterContent, GUI.skin.box);
    //EditorGUIUtility.AddCursorRect(splitterArea, MouseCursor.ResizeVertical);
  }
  private static GUIContent vSplitterContent = new GUIContent("--");

  protected static void EndVerticalPanes() {
    EditorGUILayout.EndVertical();
  }


  //----------------------------------------------------------------------------
  // Tying It All Together...
  //----------------------------------------------------------------------------
  public bool showVars = true;
  // TODO: Save pane sizing states...
  private VerticalPaneState paneConfiguration = new VerticalPaneState() {
    minPaneHeightTop = 65,
    minPaneHeightBottom = 100
  };
  public void OnGUI() {
    HandleInputFocusAndStateForEditor();

    GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));
      if(GUILayout.Button("Clear Log", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
        logEntries.Clear();

      GUILayout.Label(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true));
    GUILayout.EndHorizontal();

    ShowEditor();

    BeginVerticalPanes(paneConfiguration);
      ShowVars();
    VerticalSplitter();
      ShowLog();
    EndVerticalPanes();
  }

  public void OnEnable() {
    List<LogEntry> log = logEntries;
    Application.RegisterLogCallback(delegate(string cond, string sTrace, LogType lType) {
      log.Add(new LogEntry() {
        logEntryType = LogEntryType.ConsoleLog,
        condition = cond,
        stackTrace = sTrace,
        consoleLogType = lType
      });
    });
  }

  [MenuItem("Window/C# Shell #%r")]
  public static void Init() {
    Shell window = (Shell)EditorWindow.GetWindow(typeof(Shell));
    window.title = "C# Shell";
    window.Show();
  }
}
