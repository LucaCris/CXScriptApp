using CXS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CXScriptApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var script = System.IO.File.ReadAllText("Script.txt");

            var cxs = new CXScript();
            cxs.SetupContext("Detail", new Context());
            var res = cxs.Execute(script, out string Err) as Context;

            Console.WriteLine(cxs.Dump());
            Console.WriteLine("Done.");

            Console.WriteLine(Err);
            Environment.Exit(Err != null ? 5 : 0);
        }
    }

    public class Context
    {
        Dictionary<string, string> Fields = new Dictionary<string, string>();
        public Context()
        {
            Fields.Add("Nome", "Anto");
            Fields.Add("Cognome", "");
            Fields.Add("Data", "");
            Fields.Add("Ctr", "");
        }

        public void Set(string f, string n)
        {
            Fields[f] = n;
        }

        public string Get(string f)
        {
            return Fields[f];
        }
    }
}
