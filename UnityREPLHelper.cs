//-----------------------------------------------------------------
//  GUIHelper v0.11
//  Copyright 2009 MrJoy, Inc.
//  All rights reserved
//
//-----------------------------------------------------------------
// Helper for UnityGUI tasks, particularly editor-related.
//-----------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using System.Text;
using System.Collections;

public class UnityREPLHelper {
  private static Hashtable styleCache = new Hashtable();
  public static GUIStyle CachedStyle(string name) {
    if(!styleCache.ContainsKey(name))
      styleCache[name] = GUI.skin.GetStyle(name);
    return (GUIStyle)styleCache[name];
  }

  public static NumberedEditorState NumberedTextArea(string controlName, NumberedEditorState editorState) {
    // This is a WAG about Unity's box model.  Seems to work though, so...
    // yeah.
    float effectiveWidgetHeight = 7 * GUI.skin.label.lineHeight
//        + GUI.skin.label.margin.top + GUI.skin.label.margin.bottom
      + GUI.skin.label.padding.top + GUI.skin.label.padding.bottom
    ;
    Rect r = EditorGUILayout.BeginVertical();
    if(r.width > 0) {
      editorState.scrollViewWidth = r.width;
      editorState.scrollViewHeight = r.height;
    }
//Debug.Log(r);
    editorState.scrollPos = GUILayout.BeginScrollView(editorState.scrollPos, false, false, CachedStyle("HorizontalScrollbar"), CachedStyle("VerticalScrollbar"), CachedStyle("TextField"), GUILayout.Height(effectiveWidgetHeight));
//      int scrollId = GUIUtility.GetControlID(FocusType.Passive);
//      Debug.Log(scrollId);
//      object o = GUIUtility.QueryStateObject(typeof(System.Object), scrollId);
//      if(o != null)
//        Debug.Log(o.GetType().FullName);
//      else
//        Debug.Log("Well, honey...  I blew it again.");

      GUILayout.BeginHorizontal();
        GUILayout.Label(editorState.lineNumberingContent, NumberedEditorStyles.LineNumbering);
        GUIContent txt = new GUIContent(editorState.text);
        GUIContent dTxt = new GUIContent(editorState.dummyText);
        float minW, maxW;
        NumberedEditorStyles.NumberedEditor.CalcMinMaxWidth(dTxt, out minW, out maxW);
        GUI.SetNextControlName(controlName);
        Rect editorRect = GUILayoutUtility.GetRect(txt, NumberedEditorStyles.NumberedEditor, GUILayout.Width(maxW));
        editorRect.width = maxW;
//        Debug.Log("B: " + Event.current.type.ToString() + ", " + Event.current.isKey + "," + Event.current.isMouse + "," + Event.current.button);
        bool wasMouseDrag = Event.current.type == EventType.MouseDrag;
        bool wasRelevantEvent = wasMouseDrag || Event.current.type == EventType.KeyDown;
        editorState.text = GUI.TextField(editorRect, editorState.text, NumberedEditorStyles.NumberedEditor);

//        Debug.Log("A: " + Event.current.type.ToString() + ", " + Event.current.isKey + "," + Event.current.isMouse + "," + Event.current.button);
        if ((GUI.GetNameOfFocusedControl() == controlName) &&
            wasRelevantEvent) {
          int editorId = GUIUtility.keyboardControl;
          TextEditor te = GUIUtility.QueryStateObject(typeof(System.Object), editorId) as TextEditor;
          int pos = te.pos; // TODO: How does this play with keyboard selection?  We want the actual cursor pos, not necessarily the right-end.
          if(pos != editorState.lastPos) {
            Vector2 cursorPixelPos = NumberedEditorStyles.NumberedEditor.GetCursorPixelPosition(editorRect, txt, pos);
            cursorPixelPos.y -= 1; // 0-align...
            float yBuffer = NumberedEditorStyles.NumberedEditor.lineHeight * 2;
            float xBuffer = 40f; // TODO: Make this a little less arbitrary?
            if(wasMouseDrag) {
              yBuffer = 0;
              xBuffer = 0;
            }
//            cursorPixelPos.x -= CachedStyle("TextField").padding.left;
//            cursorPixelPos.y -= CachedStyle("TextField").padding.top;

            if(editorState.scrollViewWidth > 0) {
              if(cursorPixelPos.y + yBuffer > editorState.scrollPos.y + editorState.scrollViewHeight - NumberedEditorStyles.NumberedEditor.lineHeight)
                editorState.scrollPos.y = cursorPixelPos.y + yBuffer + NumberedEditorStyles.NumberedEditor.lineHeight - editorState.scrollViewHeight;
              if(cursorPixelPos.y - yBuffer < editorState.scrollPos.y)
                editorState.scrollPos.y = cursorPixelPos.y - yBuffer;

              if(cursorPixelPos.x + xBuffer > editorState.scrollPos.x + editorState.scrollViewWidth)
                editorState.scrollPos.x = cursorPixelPos.x + xBuffer - editorState.scrollViewWidth;
              if(cursorPixelPos.x - xBuffer < editorState.scrollPos.x)
                editorState.scrollPos.x = cursorPixelPos.x - xBuffer;
            }
          }
          editorState.lastPos = pos;
        }
//        editorState.scrollPos = Vector2.zero;

        if(GUI.changed) {
//Debug.Log("A");

          // TODO: Determine properly if we have a *keystroke* event.


/*            // The text actually changed...
            if(editorState.text.StartsWith(oldValue)) {
              // The text was appended to.
              editorState.scrollPos.y = Mathf.Infinity;
            }
          } */
        }
      GUILayout.EndHorizontal();
    GUILayout.EndScrollView();
    EditorGUILayout.EndVertical();

    return editorState;
  }

//  private static Rect zero = new Rect(0,0,0,0);
//  private static char NEWLINE = "\n"[0];
}

public class NumberedEditorStyles {
  private static GUIStyle _LineNumbering = null;
  public static GUIStyle LineNumbering {
    get {
      if(_LineNumbering == null) {
        _LineNumbering = new GUIStyle(EditorStyles.textField) {
          name = "LineNumbering",
          alignment = TextAnchor.UpperRight,
          fixedWidth = 0,
          fixedHeight = 0,
          wordWrap = false,
          stretchWidth = false,
          stretchHeight = true,
          imagePosition = ImagePosition.TextOnly,
          clipping = TextClipping.Clip,
          border = new RectOffset(),
          margin = new RectOffset(),
          padding = new RectOffset(),
          overflow = new RectOffset()
        };

        _LineNumbering.padding.left = 5;

        _LineNumbering.normal.background = null;
        _LineNumbering.active.background = null;
        _LineNumbering.hover.background = null;
        _LineNumbering.focused.background = null;
        _LineNumbering.onNormal.background = null;
        _LineNumbering.onActive.background = null;
        _LineNumbering.onHover.background = null;
        _LineNumbering.onFocused.background = null;
        _LineNumbering.normal.textColor = new Color(0.5f, 0.5f, 0.5f, 1f);
      }
      return _LineNumbering;
    }
  }

  private static GUIStyle _NumberedEditor = null;
  public static GUIStyle NumberedEditor {
    get {
      if(_NumberedEditor == null) {
        _NumberedEditor = new GUIStyle(LineNumbering) {
          name = "NumberedEditor",
          alignment = TextAnchor.UpperLeft,
          stretchWidth = true,
          stretchHeight = true,

          clipping = TextClipping.Clip,
          border = new RectOffset(),
          margin = new RectOffset(5, 0, 0, 0),
          padding = new RectOffset(0, 4, 0, 0),
          overflow = new RectOffset()
        };

        _NumberedEditor.normal.textColor = new Color(0f, 0f, 0f, 1f);
      }
      return _NumberedEditor;
    }
  }

  private static GUIStyle _DummyStyle = null;
  public static GUIStyle DummyStyle {
    get {
      if(_DummyStyle == null) {
        _DummyStyle = new GUIStyle(NumberedEditor) {
          name = "DummyStyle"
        };
        _DummyStyle.normal.textColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
      }
      return _DummyStyle;
    }
  }
}

public class LogEntryStyles {
  private static GUIStyle _Default = null;
  public static GUIStyle Default {
    get {
      if(_Default == null) {
        _Default = new GUIStyle("Label") {
          name = "Default"
        };
        _Default.normal.textColor =
          _Default.active.textColor =
          _Default.onNormal.textColor =
          _Default.onActive.textColor =
          new Color(1.0f, 1.0f, 1.0f, 1.0f);
      }
      return _Default;
    }
  }

  private static GUIStyle _DefaultCommandStyle = null;
  public static GUIStyle DefaultCommandStyle {
    get {
      if(_DefaultCommandStyle == null) {
        _DefaultCommandStyle = new GUIStyle(Default) {
          name = "DefaultCommandStyle"
        };
        _DefaultCommandStyle.normal.textColor =
          _DefaultCommandStyle.active.textColor =
          _DefaultCommandStyle.onNormal.textColor =
          _DefaultCommandStyle.onActive.textColor =
          Color.blue;
      }
      return _DefaultCommandStyle;
    }
  }

  private static GUIStyle _FoldoutCommandStyle = null;
  public static GUIStyle FoldoutCommandStyle {
    get {
      if(_FoldoutCommandStyle == null) {
        _FoldoutCommandStyle = new GUIStyle(EditorStyles.foldout) {
          name = "FoldoutCommandStyle"
        };
        _FoldoutCommandStyle.normal.textColor =
          _FoldoutCommandStyle.active.textColor =
          _FoldoutCommandStyle.onNormal.textColor =
          _FoldoutCommandStyle.onActive.textColor =
          Color.blue;
      }
      return _FoldoutCommandStyle;
    }
  }

  private static GUIStyle _OutputStyle = null;
  public static GUIStyle OutputStyle {
    get {
      if(_OutputStyle == null) {
        _OutputStyle = new GUIStyle(Default) {
          name = "OutputStyle"
        };
        _OutputStyle.normal.textColor =
          _OutputStyle.active.textColor =
          _OutputStyle.onNormal.textColor =
          _OutputStyle.onActive.textColor =
          new Color(0f, 0.5f, 0f, 1f);
      }
      return _OutputStyle;
    }
  }

  private static GUIStyle _EvaluationErrorStyle = null;
  public static GUIStyle EvaluationErrorStyle {
    get {
      if(_EvaluationErrorStyle == null) {
        _EvaluationErrorStyle = new GUIStyle(Default) {
          name = "EvaluationErrorStyle"
        };
        _EvaluationErrorStyle.normal.textColor =
          _EvaluationErrorStyle.active.textColor =
          _EvaluationErrorStyle.onNormal.textColor =
          _EvaluationErrorStyle.onActive.textColor =
          new Color(0.5f, 0f, 0f, 1f);
      }
      return _EvaluationErrorStyle;
    }
  }

  private static GUIStyle _SystemConsoleStyle = null;
  public static GUIStyle SystemConsoleStyle {
    get {
      if(_SystemConsoleStyle == null) {
        _SystemConsoleStyle = new GUIStyle(Default) {
          name = "SystemConsoleStyle"
        };
        _SystemConsoleStyle.normal.textColor =
          _SystemConsoleStyle.active.textColor =
          _SystemConsoleStyle.onNormal.textColor =
          _SystemConsoleStyle.onActive.textColor =
          new Color(0.5f, 0.5f, 0f, 1f);
      }
      return _SystemConsoleStyle;
    }
  }

  private static GUIStyle _ConsoleLogStyle = null;
  public static GUIStyle ConsoleLogStyle {
    get {
      if(_ConsoleLogStyle == null) {
        _ConsoleLogStyle = new GUIStyle(Default) {
          name = "ConsoleLogStyle"
        };
        _ConsoleLogStyle.normal.textColor = GUI.contentColor;
      }
      return _ConsoleLogStyle;
    }
  }

  private static GUIStyle _ConsoleLogConditionStyle = null;
  public static GUIStyle ConsoleLogConditionStyle {
    get {
      if(_ConsoleLogConditionStyle == null) {
        _ConsoleLogConditionStyle = new GUIStyle(ConsoleLogStyle) {
          name = "ConsoleLogConditionStyle"
        };
      }
      return _ConsoleLogConditionStyle;
    }
  }

  private static GUIStyle _ConsoleLogStackTraceStyle = null;
  public static GUIStyle ConsoleLogStackTraceStyle {
    get {
      if(_ConsoleLogStackTraceStyle == null) {
        _ConsoleLogStackTraceStyle = new GUIStyle(ConsoleLogStyle) {
          name = "ConsoleLogStackTraceStyle"
        };
        _ConsoleLogStackTraceStyle.normal.textColor =
          _ConsoleLogStackTraceStyle.active.textColor =
          _ConsoleLogStackTraceStyle.onNormal.textColor =
          _ConsoleLogStackTraceStyle.onActive.textColor =
          Color.red;
      }
      return _ConsoleLogStackTraceStyle;
    }
  }

}

[System.Serializable]
public class NumberedEditorState {
  public Vector2 scrollPos;
  public float scrollViewWidth, scrollViewHeight;
  public int lastPos;
  public bool textChanged = false;
  private string _text = "";
  public string text {
    get { return _text; }
    set {
      if(_text != value) {
        _text = value;
        _lineNumberingContent = null;
        _textContent = null;
        _dummyText = null;
        textChanged = true;
      }
    }
  }

  private GUIContent _textContent = null;
  public GUIContent textContent {
    get {
      if(_textContent == null)
        _textContent = new GUIContent(text);
      return _textContent;
    }
  }

  private string _dummyText = null;
  public string dummyText {
    get {
      return _dummyText;
    }
  }

  private GUIContent _lineNumberingContent = null;
  public GUIContent lineNumberingContent {
    get {
      // Unity likes to ignore trailing space when sizing content, which is a
      // problem for us, so we construct a version of our content that has a .
      // at the end of each line -- small enough to not consume too much extra
      // width, but not a space, so we can use that for sizing later on.
      if(_lineNumberingContent == null) {
        string[] linesRaw = text.Split('\n');
        int lines = linesRaw.Length;
        if(lines == 0) lines = 1;

        StringBuilder sb = new StringBuilder();
        for(int j = 0; j < lines; j++)
          sb.Append(linesRaw[j]).Append(".").Append("\n");
        _dummyText = sb.ToString();

        // While we're at it, build a single string with all our line numbers.
        sb.Length = 0;
        for(int j = 0; j < lines; j++)
          sb.Append(j+1).Append('\n');

        _lineNumberingContent = new GUIContent(sb.ToString());
      }
      return _lineNumberingContent;
    }
  }
}
