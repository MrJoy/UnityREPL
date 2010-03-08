//-----------------------------------------------------------------
//  HistoryStyles v0.1 (2010-03-08)
//  Copyright 2009-2010 MrJoy, Inc.
//  All rights reserved
//
//  2010-03-08 - jdf - Initial version.
//
//-----------------------------------------------------------------
// Styles used by history viewer.
//
// TODO: Take a look at Firebug again and see what distinctions its making.  Try
//       to emulate it where meaningful.
//-----------------------------------------------------------------
using UnityEditor;
using UnityEngine;

public static class HistoryStyles {
  private static GUIStyle _Foldout = null;
  public static GUIStyle Foldout {
    get {
      if(_Foldout == null) {
#if UNITY_IPHONE
        _Foldout = new GUIStyle();
#else
        _Foldout = new GUIStyle(EditorStyles.foldout);
#endif
        _Foldout.name = "REPLFoldout";
        _Foldout.clipping = TextClipping.Clip;
      }
      return _Foldout;
    }
  }

  private static GUIStyle _CodeFoldout = null;
  public static GUIStyle CodeFoldout {
    get {
      if(_CodeFoldout == null) {
#if UNITY_IPHONE
        _CodeFoldout = new GUIStyle();
#else
        _CodeFoldout = new GUIStyle(Foldout);
        _CodeFoldout.font = EditorStyles.boldFont;
#endif
        _CodeFoldout.name = "REPLCodeFoldout";
      }
      return _CodeFoldout;
    }
  }
  
  private static GUIStyle _PseudoFoldout = null;
  public static GUIStyle PseudoFoldout {
    get {
      if(_PseudoFoldout == null) {
#if UNITY_IPHONE
        _PseudoFoldout = new GUIStyle();
#else
        _PseudoFoldout = new GUIStyle(Foldout);
#endif
        _PseudoFoldout.name = "REPLPseudoFoldout";
        _PseudoFoldout.stretchHeight = false;
        _PseudoFoldout.normal.background = null;
      }
      return _PseudoFoldout;
    }
  }
  
  private static GUIStyle _CodePseudoFoldout = null;
  public static GUIStyle CodePseudoFoldout {
    get {
      if(_CodePseudoFoldout == null) {
#if UNITY_IPHONE
        _CodePseudoFoldout = new GUIStyle();
#else
        _CodePseudoFoldout = new GUIStyle(PseudoFoldout);
        _CodePseudoFoldout.font = EditorStyles.boldFont;
#endif
        _CodePseudoFoldout.name = "REPLCodePseudoFoldout";
      }
      return _CodePseudoFoldout;
    }
  }
}
