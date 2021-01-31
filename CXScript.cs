using DynamicExpresso;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CXScriptApp
{
    public enum CXType
    {
        NULL,
        IF,
        ELSE,
        ENDIF,
        WHILE,
        ENDW,
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
        bool Stop;
        Dictionary<int, CXObj> Flow = new Dictionary<int, CXObj>();
        Stack<int> IFStack = new Stack<int>();
        Stack<int> WStack = new Stack<int>();

        public CXScript()
        {
            Interpreter = new Interpreter();
        }

        public void SetupContext(string name, object context)
        {
            Context = context;
            Interpreter.SetVariable(name, Context);
        }

        public void Compile(string script)
        {
            Line = script.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
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
                } else if (Line[i] == "ENDW") {
                    if (WStack.Count > 0) {
                        int l = WStack.Pop();
                        Flow[l].V1 = i;
                        Flow.Add(i, new CXObj(l) { Type = CXType.ENDW });
                    } else
                        throw new Exception("ENDW unexpected");
                }
            }
        }

        public object Execute(string script, out string Error)
        {
            Error = null;
            Stop = false;
            try {
                Compile(script);

                CurLine = 0;

                while (CurLine < Line.Length && !Stop) {
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
                        } else if (f.Type == CXType.ENDW) {
                            CurLine = Flow[f.CurLine].CurLine - 1;
                        }
                    } else
                        Execute(CurLine);

                    CurLine++;
                }
            }
            catch (Exception ex) {
                Error = $"{ex.Message}\r\n{Line[ExeLine]}";
            }

            return Context;
        }

        private void Execute(int line)
        {
            System.Diagnostics.Debug.WriteLine($"{line}: {Line[line]}");

            ExeLine = line;
            if (Line[line].StartsWith("//") || Line[line].StartsWith(":"))
                return;

            if (Line[line] == "STOP") {
                Stop = true;
                return;
            }

            if (Line[line].StartsWith("GOTO ")) {
                string label = ":" + Line[line].Substring(5);
                for (int i = 0; i < Line.Length; i++)
                    if (Line[i] == label) {
                        CurLine = i;
                        return;
                    }
                throw new Exception("Label not found.");
            }

            Lambda code;
            if (Line[line].StartsWith("SET ")) {
                int pos = Line[line].IndexOf("=");
                if (pos < 0)
                    throw new Exception("Syntax Error");
                string varname = Line[line].Substring(4, pos - 4).Trim();

                Interpreter.SetVariable(varname, Interpreter.Eval(Line[line].Substring(pos + 1)));
                return;
            }

            code = Interpreter.Parse(Line[line]);
            code.Invoke();
        }

        private (bool Valid, int IFend, int ELSEstart, int ELSEend) GetIFBlock(int ParseLine)
        {
            int IFend = -1, ELSEstart = -1, ELSEend = -1;

            while (++ParseLine < Line.Length) {
                if (Line[ParseLine] == "ENDIF") {
                    if (ELSEstart == -1)
                        IFend = ParseLine - 1;
                    else
                        ELSEend = ParseLine - 1;
                    break;
                } else if (Line[ParseLine] == "ELSE") {
                    IFend = ParseLine - 1;
                    ELSEstart = ParseLine + 1;
                }
            }

            bool isValid = IFend >= 0;
            if (ELSEstart >= 0)
                isValid &= ELSEend >= 0;
            return (isValid, IFend, ELSEstart, ELSEend);
        }

        private (bool Valid, int WStart, int WEnd) GetWHILEBlock(int ParseLine)
        {
            int WEnd = -1;
            int WStart = ParseLine;
            while (++ParseLine < Line.Length) {
                if (Line[ParseLine] == "ENDW") {
                    WEnd = ParseLine - 1;
                    break;
                }
            }

            return (WEnd > WStart, WStart, WEnd);
        }
    }
}
