//-----------------------------------------------------------------
//  GUIHelper v0.11 (2009-10-12)
//  Copyright 2009 MrJoy, Inc.
//  All rights reserved
//
//  2009-10-12 - jdf - Compile clean on Unity/iPhone.  Not terribly USEFUL...
//  2009-10-08 - jdf - Initial version.
//
//-----------------------------------------------------------------
// Helper for UnityGUI tasks, particularly editor-related.
//-----------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using System.Text;
using System.Collections;

public class GUIHelper {
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
    editorState.scrollPos = GUILayout.BeginScrollView(editorState.scrollPos, false, false, CachedStyle("HorizontalScrollbar"), CachedStyle("VerticalScrollbar"), CachedStyle("TextField"), GUILayout.Height(effectiveWidgetHeight));
      int scrollId = GUIUtility.GetControlID(FocusType.Passive);
      Debug.Log(scrollId);
      GUILayout.BeginHorizontal();
        if((editorState.lineNumberingContent == null) || (editorState.textChanged)) {
          editorState.textChanged = false;

          int lines = 1;
          for(int i = 0; i < editorState.text.Length; i++)
            if(editorState.text[i] == '\n') 
              lines++;

          StringBuilder sb = new StringBuilder();
          for(int j = 0; j < lines; j++)
            sb.Append(j+1).Append('\n');

          editorState.lineNumberingContent = new GUIContent(sb.ToString());
        }
        GUILayout.Label(editorState.lineNumberingContent, NumberedEditorStyles.LineNumbering, GUILayout.ExpandWidth(false));
        string oldValue = editorState.text;
        GUI.SetNextControlName(controlName);
        editorState.text = GUILayout.TextField(editorState.text, NumberedEditorStyles.NumberedEditor, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        if(GUI.changed) {
          editorState.lineNumberingContent = null;
          // TODO: Figure out how to auto-scroll to the right on long lines...
          if(editorState.text.Length != oldValue.Length) {
            // The text actually changed...

            if(editorState.text.StartsWith(oldValue)) {
              // The text was appended to.
              editorState.scrollPos.y = Mathf.Infinity;
            }
          }
        }
      GUILayout.EndHorizontal();
    GUILayout.EndScrollView();

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
#if UNITY_2_6 || UNITY_2_6_1 || UNITY_3_0 || UNITY_3_0_0
        _LineNumbering = new GUIStyle(EditorStyles.textField);
#else
        _LineNumbering = new GUIStyle("textField");
#endif
        _LineNumbering.name = "LineNumbering";
        _LineNumbering.alignment = TextAnchor.UpperRight;
        _LineNumbering.fixedWidth = 0;
        _LineNumbering.fixedHeight = 0;
        _LineNumbering.wordWrap = false;
        _LineNumbering.stretchWidth = false;
        _LineNumbering.stretchHeight = true;
        _LineNumbering.imagePosition = ImagePosition.TextOnly;
        _LineNumbering.clipping = TextClipping.Clip;

        _LineNumbering.border = new RectOffset();
        _LineNumbering.margin = new RectOffset();
        _LineNumbering.padding = new RectOffset();
        _LineNumbering.overflow = new RectOffset();

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
        _NumberedEditor = new GUIStyle(LineNumbering);
        _NumberedEditor.name = "NumberedEditor";
        _NumberedEditor.alignment = TextAnchor.UpperLeft;
        _NumberedEditor.stretchWidth = true;
        _NumberedEditor.stretchHeight = true;

        _NumberedEditor.border = new RectOffset();
        _NumberedEditor.margin = new RectOffset();
        _NumberedEditor.padding = new RectOffset();
        _NumberedEditor.overflow = new RectOffset();
        _NumberedEditor.clipping = TextClipping.Overflow;

        _NumberedEditor.margin.left = 5;
        _NumberedEditor.padding.right = 2;

        _NumberedEditor.normal.textColor = new Color(0f, 0f, 0f, 1f);
      }
      return _NumberedEditor;
    }
  }
}

[System.Serializable]
public class NumberedEditorState {
  public Vector2 scrollPos;
  public bool textChanged = false;
  private string _text = "";
  public string text {
    get { return _text; }
    set {
      if(_text != value) {
        _text = value;
        textChanged = true;
      }
    }
  }
  public GUIContent lineNumberingContent = null;
}