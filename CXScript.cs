﻿using DynamicExpresso;
using System;
using System.Collections.Generic;
using System.Text;

namespace CXS
{
    public enum CXType
    {
        NULL,
        IF,
        ELSE,
        ENDIF,
        WHILE,
        ENDW,
        BREAKW,
        GOTO,
        CALL,
    }

    public class CXObj
    {
        public CXObj(int l)
        {
            CurLine = l;
            Type = CXType.NULL;
            V1 = -1;
            V2 = -1;
        }

        public CXType Type;
        public int CurLine;
        public int V1;
        public int V2;
    }

    public class CXScript
    {
        Interpreter Interpreter;
        object Context;
        string[] Line;
        int CurLine;
        int ExeLine;
        bool Exit;
        Dictionary<int, CXObj> Flow = new Dictionary<int, CXObj>();
        Stack<int> IFStack = new Stack<int>();
        Stack<int> WStack = new Stack<int>();
        Stack<int> CallStack = new Stack<int>();

        public CXScript()
        {
            Interpreter = new Interpreter();
        }

        public string Dump()
        {
            var s = new StringBuilder();
            foreach (var v in Interpreter.Identifiers) {
                s.Append(v.Name);
                s.Append(" = ");
                s.AppendLine(v.Expression.ToString());
            }
            return s.ToString();
        }

        public void SetupContext(string name, object context)
        {
            Context = context;
            Interpreter.SetVariable(name, Context);
        }

        public void SetVar(string name, object value)
        {
            Interpreter.SetVariable(name, value);
        }

        public object GetVar(string name)
        {
            try {
                return Interpreter.Eval(name);
            }
            catch { return null; }
        }

        public void Compile(string script)
        {
            Line = script.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0; i < Line.Length; i++)
                Line[i] = Line[i].Trim();

            for (int i = 0; i < Line.Length; i++) {
                ExeLine = i;
                if (Line[i].StartsWith("IF ")) {
                    Flow.Add(i, new CXObj(i) { Type = CXType.IF });
                    IFStack.Push(i);
                } else if (Line[i] == "ELSE") {
                    if (IFStack.Count > 0) {
                        int l = IFStack.Peek();
                        if (Flow[l].V1 < 0) {
                            Flow[l].V1 = i;
                            Flow.Add(i, new CXObj(l) { Type = CXType.ELSE });
                        } else
                            throw new Exception("Unmatched ELSE");
                    } else
                        throw new Exception("ELSE unexpected");
                } else if (Line[i] == "ENDIF") {
                    if (IFStack.Count > 0) {
                        int l = IFStack.Pop();
                        Flow[l].V2 = i;
                        Flow.Add(i, new CXObj(l) { Type = CXType.ENDIF });
                    } else
                        throw new Exception("ENDIF unexpected");
                } else if (Line[i].StartsWith("WHILE ")) {
                    Flow.Add(i, new CXObj(i) { Type = CXType.WHILE });
                    WStack.Push(i);
                } else if (Line[i] == "BREAKW") {
                    if (WStack.Count > 0) {
                        int l = WStack.Peek();
                        Flow.Add(i, new CXObj(l) { Type = CXType.BREAKW });
                    } else
                        throw new Exception("BREAKW unexpected");
                } else if (Line[i] == "ENDW") {
                    if (WStack.Count > 0) {
                        int l = WStack.Pop();
                        Flow[l].V1 = i;
                        Flow.Add(i, new CXObj(l) { Type = CXType.ENDW });
                    } else
                        throw new Exception("ENDW unexpected");
                } else if (Line[i].StartsWith("GOTO ")) {
                    string label = ":" + Line[i].Substring(5);
                    var idx = Array.FindIndex(Line, (x) => x == label);
                    if (idx >= 0)
                        Flow.Add(i, new CXObj(idx) { Type = CXType.GOTO });
                    else
                        throw new Exception("Label not found.");
                } else if (Line[i].StartsWith("CALL ")) {
                    string label = ":" + Line[i].Substring(5);
                    var idx = Array.FindIndex(Line, (x) => x == label);
                    if (idx >= 0)
                        Flow.Add(i, new CXObj(idx) { Type = CXType.CALL });
                    else
                        throw new Exception("Label not found.");
                }
            }

            if (IFStack.Count > 0) {
                ExeLine = Flow[IFStack.Peek()].CurLine;
                throw new Exception("ENDIF missing");
            }

            if (WStack.Count > 0) {
                ExeLine = Flow[WStack.Peek()].CurLine;
                throw new Exception("ENDW missing");
            }
        }

        public object Execute(string script, out string Error)
        {
            Error = null;
            Exit = false;
            try {
                Compile(script);

                CurLine = 0;

                while (CurLine < Line.Length && !Exit) {
                    ExeLine = CurLine;

                    if (Flow.ContainsKey(CurLine)) {
                        var f = Flow[CurLine];
                        if (f.Type == CXType.IF) {
                            if (Interpreter.Eval<bool>(Line[CurLine].Substring(3))) {
                                // DO NOTHING
                            } else {
                                if (f.V1 >= 0)
                                    CurLine = f.V1;
                                else
                                    CurLine = f.V2;
                            }
                        } else if (f.Type == CXType.ELSE) {
                            CurLine = Flow[f.CurLine].V2;
                        } else if (f.Type == CXType.WHILE) {
                            if (!Interpreter.Eval<bool>(Line[CurLine].Substring(6))) {
                                CurLine = f.V1;
                            }
                        } else if (f.Type == CXType.BREAKW) {
                            CurLine = Flow[f.CurLine].V1;
                        } else if (f.Type == CXType.ENDW) {
                            CurLine = Flow[f.CurLine].CurLine - 1;
                        } else if (f.Type == CXType.GOTO) {
                            CurLine = f.CurLine;
                        } else if (f.Type == CXType.CALL) {
                            CallStack.Push(CurLine);
                            CurLine = f.CurLine;
                        }
                    } else
                        Execute(CurLine);

                    CurLine++;
                }
            }
            catch (Exception ex) {
                Error = $"{ex.Message} - Line: {ExeLine + 1}\r\n{Line[ExeLine]}";
            }

            return Context;
        }

        private void Execute(int line)
        {
            System.Diagnostics.Debug.WriteLine($"{line}: {Line[line]}");

            ExeLine = line;
            if (string.IsNullOrWhiteSpace(Line[line]) || Line[line].StartsWith("//") || Line[line].StartsWith(":"))
                return;

            if (Line[line] == "EXIT") {
                Exit = true;
                return;
            }

            if (Line[line] == "RETURN") {
                if (CallStack.Count > 0) {
                    CurLine = CallStack.Pop();
                    return;
                } else
                    throw new Exception("RETURN without CALL");
            }

            if (Line[line].StartsWith("SET ")) {
                int pos = Line[line].IndexOf("=");
                if (pos < 0)
                    throw new Exception("Syntax Error");
                string varname = Line[line].Substring(4, pos - 4).Trim();

                Interpreter.SetVariable(varname, Interpreter.Eval(Line[line].Substring(pos + 1)));
                return;
            }

            Lambda code;
            code = Interpreter.Parse(Line[line]);
            code.Invoke();
        }
    }
}
