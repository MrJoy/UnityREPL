//-----------------------------------------------------------------
//  ReflectionProxy
//  Copyright 2009-2014 Jon Frisby
//  All rights reserved
//
//-----------------------------------------------------------------
// Base class for utility classes to make reflection less cumbersome to use.
//-----------------------------------------------------------------
using System;
using System.Reflection;

internal class ReflectionProxy {
  internal const BindingFlags PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;
  internal const BindingFlags NONPUBLIC_STATIC = BindingFlags.NonPublic | BindingFlags.Static;
  internal const BindingFlags NONPUBLIC_INSTANCE = BindingFlags.NonPublic | BindingFlags.Instance;

  protected static Type[] Signature(params Type[] sig) {
    return sig;
  }
}
