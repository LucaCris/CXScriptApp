using DynamicExpresso;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CXScriptApp
{
    public class CXScript
    {
        Interpreter Interpreter;
        object Context;
        string[] Line;
        int CurLine;
        int ExeLine;
        bool Stop;

        public CXScript()
        {
            Interpreter = new Interpreter();
        }

        public void SetupContext(string name, object context)
        {
            Context = context;
            Interpreter.SetVariable(name, Context);
        }

        public object Execute(string script, out string Error)
        {
            Error = null;
            Stop = false;
            try {
                Line = script.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                CurLine = 0;

                while (CurLine < Line.Length && !Stop) {
                    ExeLine = CurLine;
                    if (Line[CurLine].StartsWith("IF ")) {
                        var blocks = GetIFBlock(CurLine);
                        if (blocks.Valid) {
                            if (Interpreter.Eval<bool>(Line[CurLine].Substring(3))) {
                                for (int i = CurLine + 1; i <= blocks.IFend; i++)
                                    Execute(i);
                            } else {
                                if (blocks.ELSEstart >= 0)
                                    for (int i = blocks.ELSEstart; i <= blocks.ELSEend; i++)
                                        Execute(i);
                            }

                            CurLine = Math.Max(blocks.IFend, blocks.ELSEend) + 1;
                        } else
                            break;  // ERROR
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
            ExeLine = line;
            if (Line[line].StartsWith("//"))
                return;

            if (Line[line].Trim() == "STOP") {
                Stop = true;
                return;
            }


            Lambda code;
            if (Line[line].StartsWith("SET ")) {
                int pos = Line[line].IndexOf("=");
                if (pos < 0)
                    throw new Exception("Syntax Error");
                string varname = Line[line].Substring(4, pos - 4).Trim();

                //                code = Interpreter.Parse($@"VAR[""{varname}""]{Line[line].Substring(pos)}");
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
                if (Line[ParseLine] == "END") {
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

            return (IFend >= 0 || ELSEend >= 0, IFend, ELSEstart, ELSEend);
        }
    }

}
