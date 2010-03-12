//-----------------------------------------------------------------
//  HistoryStyles v0.1
//  Copyright 2009-2010 MrJoy, Inc.
//  All rights reserved
//-----------------------------------------------------------------
// Styles used by history viewer.
//
// TODO: Take a look at Firebug again and see what distinctions its making.  Try
//       to emulate it where meaningful.
//-----------------------------------------------------------------
using UnityEditor;
using UnityEngine;

public static class HistoryStyles {
#if !UNITY_IPHONE
  private static GUIStyle _Foldout = null;
  public static GUIStyle Foldout {
    get {
      if(_Foldout == null) {
        _Foldout = new GUIStyle(EditorStyles.foldout);
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
        _CodeFoldout = new GUIStyle(Foldout);
        _CodeFoldout.font = EditorStyles.boldFont;
        _CodeFoldout.name = "REPLCodeFoldout";
      }
      return _CodeFoldout;
    }
  }
  
  private static GUIStyle _PseudoFoldout = null;
  public static GUIStyle PseudoFoldout {
    get {
      if(_PseudoFoldout == null) {
        _PseudoFoldout = new GUIStyle(Foldout);
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
        _CodePseudoFoldout = new GUIStyle(PseudoFoldout);
        _CodePseudoFoldout.font = EditorStyles.boldFont;
        _CodePseudoFoldout.name = "REPLCodePseudoFoldout";
      }
      return _CodePseudoFoldout;
    }
  }
#endif
}
