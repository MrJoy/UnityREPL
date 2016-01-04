//-----------------------------------------------------------------
// Helper for editor-related UnityGUI tasks.
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
    // This is a WAG about Unity's box model.  Seems to work though, so... yeah.
    float effectiveWidgetHeight = 7 * GUI.skin.label.lineHeight
                                  + GUI.skin.label.padding.top + GUI.skin.label.padding.bottom;
    Rect r = EditorGUILayout.BeginVertical();
    if(r.width > 0) {
      editorState.scrollViewWidth = r.width;
      editorState.scrollViewHeight = r.height;
    }

    editorState.scrollPos = GUILayout.BeginScrollView(editorState.scrollPos, false, false, CachedStyle("HorizontalScrollbar"), CachedStyle("VerticalScrollbar"), CachedStyle("TextField"), GUILayout.Height(effectiveWidgetHeight));
    GUILayout.BeginHorizontal();
    GUILayout.Label(editorState.lineNumberingContent, NumberedEditorStyles.LineNumbering);
    GUIContent txt = new GUIContent(editorState.text);
    GUIContent dTxt = new GUIContent(editorState.dummyText);
    float minW, maxW;
    NumberedEditorStyles.NumberedEditor.CalcMinMaxWidth(dTxt, out minW, out maxW);
    GUI.SetNextControlName(controlName);
    Rect editorRect = GUILayoutUtility.GetRect(txt, NumberedEditorStyles.NumberedEditor, GUILayout.Width(maxW));
    editorRect.width = maxW;
    bool wasMouseDrag = Event.current.type == EventType.MouseDrag;
    bool wasRelevantEvent = wasMouseDrag || Event.current.type == EventType.KeyDown;
    editorState.text = GUI.TextField(editorRect, editorState.text, NumberedEditorStyles.NumberedEditor);

    if((GUI.GetNameOfFocusedControl() == controlName) &&
        wasRelevantEvent) {
      int editorId = GUIUtility.keyboardControl;
      TextEditor te = GUIUtility.QueryStateObject(typeof(System.Object), editorId) as TextEditor;
      int pos = te.cursorIndex; // TODO: How does this play with keyboard selection?  We want the actual cursor pos, not necessarily the right-end.
      if(pos != editorState.lastPos) {
        Vector2 cursorPixelPos = NumberedEditorStyles.NumberedEditor.GetCursorPixelPosition(editorRect, txt, pos);
        cursorPixelPos.y -= 1; // 0-align...
        float yBuffer = NumberedEditorStyles.NumberedEditor.lineHeight * 2;
        float xBuffer = 40f; // TODO: Make this a little less arbitrary?
        if(wasMouseDrag) {
          yBuffer = 0;
          xBuffer = 0;
        }

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
    GUILayout.EndHorizontal();
    GUILayout.EndScrollView();
    EditorGUILayout.EndVertical();

    return editorState;
  }
}

public class HelpStyles {
  private static GUIStyle _Header = null;

  public static GUIStyle Header {
    get {
      if(_Header == null) {
        _Header = new GUIStyle(EditorStyles.largeLabel) {
          alignment = TextAnchor.UpperLeft,
          wordWrap = true,
          imagePosition = ImagePosition.TextOnly
        }
          .Named("HelpHeader")
          .Size(0, 0, false, false)
          .NoBackgroundImages()
          .ResetBoxModel()
          .Margin(4, 4, 0, 0)
          .ClipText();
      }
      return _Header;
    }
  }

  private static GUIStyle _SubHeader = null;

  public static GUIStyle SubHeader {
    get {
      if(_SubHeader == null) {
        _SubHeader = new GUIStyle(EditorStyles.foldout) {
          alignment = TextAnchor.UpperLeft,
          wordWrap = false,
          imagePosition = ImagePosition.TextOnly,
          font = EditorStyles.boldLabel.font
        }
          .Named("HelpSubHeader")
          .Size(0, 0, true, false)
          .Margin(4, 4, 0, 0);
      }
      return _SubHeader;
    }
  }

  private static GUIStyle _Content = null;

  public static GUIStyle Content {
    get {
      if(_Content == null) {
        _Content = new GUIStyle(EditorStyles.wordWrappedLabel) {
          alignment = TextAnchor.UpperLeft,
          wordWrap = true,
          imagePosition = ImagePosition.TextOnly
        }
          .Named("HelpContent")
          .Size(0, 0, false, false)
          .NoBackgroundImages()
          .ResetBoxModel()
          .Margin(4 + 14, 4, 0, 0)
          .ClipText();
      }
      return _Content;
    }
  }

  private static GUIStyle _Code = null;

  public static GUIStyle Code {
    get {
      if(_Code == null) {
        _Code = new GUIStyle(EditorStyles.boldLabel) {
          alignment = TextAnchor.UpperLeft,
          wordWrap = false,
          imagePosition = ImagePosition.TextOnly
        }
          .Named("HelpCode")
          .Size(0, 0, false, false)
          .NoBackgroundImages()
          .ResetBoxModel()
          .Margin(4 + 14 + 10, 4, 4, 0)
          .ClipText();
      }
      return _Code;
    }
  }

  private static GUIStyle _Shortcut = null;

  public static GUIStyle Shortcut {
    get {
      if(_Shortcut == null) {
        _Shortcut = new GUIStyle(EditorStyles.boldLabel) {
          alignment = TextAnchor.UpperLeft,
          wordWrap = false,
          imagePosition = ImagePosition.TextOnly
        }
          .Named("HelpShortcut")
          .Size(120, 0, false, false)
          .NoBackgroundImages()
          .ResetBoxModel()
          .Margin(4 + 14 + 10, 0, 0, 0)
          .ClipText();
      }
      return _Shortcut;
    }
  }

  private static GUIStyle _Explanation = null;

  public static GUIStyle Explanation {
    get {
      if(_Explanation == null) {
        _Explanation = new GUIStyle(EditorStyles.label) {
          alignment = TextAnchor.UpperLeft,
          wordWrap = true,
          imagePosition = ImagePosition.TextOnly
        }
          .Named("HelpExplanation")
          .Size(0, 0, false, false)
          .NoBackgroundImages()
          .ResetBoxModel()
          .Margin(0, 4, 0, 0)
          .ClipText();
      }
      return _Explanation;
    }
  }

}

public class NumberedEditorStyles {
  private static GUIStyle _NumberedEditor = null;

  public static GUIStyle NumberedEditor {
    get {
      if(_NumberedEditor == null) {
        _NumberedEditor = new GUIStyle(EditorStyles.textField) {
          alignment = TextAnchor.UpperLeft,
          wordWrap = false,
          imagePosition = ImagePosition.TextOnly
        }
          .Named("NumberedEditor")
          .Size(0, 0, true, true)
          .NoBackgroundImages()
          .ResetBoxModel()
          .Padding(0, 4, 0, 0)
          .Margin(5, 0, 0, 0)
          .ClipText();
      }
      return _NumberedEditor;
    }
  }

  private static GUIStyle _LineNumbering = null;

  public static GUIStyle LineNumbering {
    get {
      if(_LineNumbering == null) {
        _LineNumbering = new GUIStyle(NumberedEditor) {
          alignment = TextAnchor.UpperRight
        }
          .Named("LineNumbering")
          .Size(0, 0, false, true)
          .Padding(5, 0, 0, 0)
          .Margin(0, 0, 0, 0)
          .BaseTextColor(new Color(0.5f, 0.5f, 0.5f, 1f));
      }
      return _LineNumbering;
    }
  }
}

public class LogEntryStyles {
  private static GUIStyle[] styles = new GUIStyle[_Count];
  private const int _Default = 0;

  public static GUIStyle Default {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_Default] == null) {
        styles[_Default] = new GUIStyle(EditorStyles.label)
          .Named("DefaultLogEntry")
          .ResetBoxModel()
          .Padding(2, 2, 2, 2)
          .Size(0, 0, true, false);
      }
      return styles[_Default];
    }
  }

  private const int _DefaultCommandStyle = 1;

  public static GUIStyle DefaultCommandStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_DefaultCommandStyle] == null) {
        styles[_DefaultCommandStyle] = new GUIStyle(Default)
          .Named("DefaultCommandStyle");
      }
      return styles[_DefaultCommandStyle];
    }
  }

  private const int _FoldoutCommandStyle = 2;

  public static GUIStyle FoldoutCommandStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_FoldoutCommandStyle] == null) {
        styles[_FoldoutCommandStyle] = new GUIStyle(EditorStyles.foldout)
          .Named("FoldoutCommandStyle")
          .BaseTextColor(DefaultCommandStyle.active.textColor);
      }
      return styles[_FoldoutCommandStyle];
    }
  }

  private const int _FoldoutCopyContentStyle = 3;

  public static GUIStyle FoldoutCopyContentStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_FoldoutCopyContentStyle] == null)
        styles[_FoldoutCopyContentStyle] = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).GetStyle("OL Plus"));
      return styles[_FoldoutCopyContentStyle];
    }
  }

  private const int PLAIN_TEXT = 0,
                    ERROR_TEXT = 1,
                    WARNING_TEXT = 2;
  private static Color[,] COLORS = new Color[,] {
    { new Color(0f, 0f, 0f, 1f), new Color(0.75f, 0.75f, 0.75f, 1f) },
    { new Color(0.5f, 0f, 0f, 1f), new Color(1f, 0.25f, 0.25f, 1f) },
    { new Color(0.4f, 0.3f, 0f, 1f), new Color(1f, 0.7f, 0f, 1f) }
  };
  private const int _OutputStyle = 4;

  public static GUIStyle OutputStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_OutputStyle] == null) {
        styles[_OutputStyle] = new GUIStyle(Default)
          .Named("OutputStyle")
          .BaseTextColor(COLORS[PLAIN_TEXT, 0], COLORS[PLAIN_TEXT, 1]);
      }
      return styles[_OutputStyle];
    }
  }

  private const int _EvaluationErrorStyle = 5;

  public static GUIStyle EvaluationErrorStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_EvaluationErrorStyle] == null) {
        styles[_EvaluationErrorStyle] = new GUIStyle(Default)
          .Named("EvaluationErrorStyle")
          .BaseTextColor(COLORS[ERROR_TEXT, 0], COLORS[ERROR_TEXT, 1]);
      }
      return styles[_EvaluationErrorStyle];
    }
  }

  private const int _SystemConsoleOutStyle = 6;

  public static GUIStyle SystemConsoleOutStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_SystemConsoleOutStyle] == null) {
        styles[_SystemConsoleOutStyle] = new GUIStyle(Default)
          .Named("SystemConsoleOutStyle")
          .BaseTextColor(COLORS[WARNING_TEXT, 0], COLORS[WARNING_TEXT, 1]);
      }
      return styles[_SystemConsoleOutStyle];
    }
  }

  private const int _SystemConsoleErrStyle = 7;

  public static GUIStyle SystemConsoleErrStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_SystemConsoleErrStyle] == null) {
        styles[_SystemConsoleErrStyle] = new GUIStyle(Default)
          .Named("SystemConsoleErrStyle")
          .BaseTextColor(COLORS[ERROR_TEXT, 0], COLORS[ERROR_TEXT, 1]);
      }
      return styles[_SystemConsoleErrStyle];
    }
  }

  private const int _ConsoleLogStyle = 8;

  public static GUIStyle ConsoleLogStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_ConsoleLogStyle] == null) {
        styles[_ConsoleLogStyle] = new GUIStyle(Default)
          .Named("ConsoleLogStyle");
      }
      return styles[_ConsoleLogStyle];
    }
  }

  private const int _ConsoleLogNormalStyle = 9;

  public static GUIStyle ConsoleLogNormalStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_ConsoleLogNormalStyle] == null) {
        styles[_ConsoleLogNormalStyle] = new GUIStyle(ConsoleLogStyle)
          .Named("ConsoleLogNormalStyle");
      }
      return styles[_ConsoleLogNormalStyle];
    }
  }

  private const int _ConsoleLogWarningStyle = 10;

  public static GUIStyle ConsoleLogWarningStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_ConsoleLogWarningStyle] == null) {
        styles[_ConsoleLogWarningStyle] = new GUIStyle(ConsoleLogStyle)
          .Named("ConsoleLogWarningStyle")
          .BaseTextColor(COLORS[WARNING_TEXT, 0], COLORS[WARNING_TEXT, 1]);
      }
      return styles[_ConsoleLogWarningStyle];
    }
  }

  private const int _ConsoleLogErrorStyle = 11;

  public static GUIStyle ConsoleLogErrorStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_ConsoleLogErrorStyle] == null) {
        styles[_ConsoleLogErrorStyle] = new GUIStyle(ConsoleLogStyle)
          .Named("ConsoleLogErrorStyle")
          .BaseTextColor(COLORS[ERROR_TEXT, 0], COLORS[ERROR_TEXT, 1]);
      }
      return styles[_ConsoleLogErrorStyle];
    }
  }

  private const int _ConsoleLogStackTraceStyle = 12;

  public static GUIStyle ConsoleLogStackTraceStyle {
    get {
      EditorGUIStyleExtensions.InvalidateOnSkinChange(styles);
      if(styles[_ConsoleLogStackTraceStyle] == null) {
        styles[_ConsoleLogStackTraceStyle] = new GUIStyle(ConsoleLogStyle)
          .Named("ConsoleLogStackTraceStyle")
          .BaseTextColor(COLORS[PLAIN_TEXT, 0], COLORS[PLAIN_TEXT, 1]);
      }
      return styles[_ConsoleLogStackTraceStyle];
    }
  }

  private const int _Count = 13;
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
        if(lines == 0)
          lines = 1;

        StringBuilder sb = new StringBuilder();
        for(int j = 0; j < lines; j++)
          sb.Append(linesRaw[j]).Append(".").Append("\n");
        _dummyText = sb.ToString();

        // While we're at it, build a single string with all our line numbers.
        sb.Length = 0;
        for(int j = 0; j < lines; j++)
          sb.Append(j + 1).Append('\n');

        _lineNumberingContent = new GUIContent(sb.ToString());
      }
      return _lineNumberingContent;
    }
  }
}
