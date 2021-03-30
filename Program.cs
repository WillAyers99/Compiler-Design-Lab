using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace parser
{
    public static class Syms
    {
        public static Dictionary<int, string> intToString =
            new Dictionary<int, string>();
        public static Dictionary<string, int> stringToInt =
            new Dictionary<string, int>();
        public static void init(string tokenfile)
        {
            using (var fs = new StreamReader(tokenfile))
            {
                while (true)
                {
                    string s = fs.ReadLine();
                    if (s == null || s.Length == 0)
                        break;
                    string[] lst = s.Split(new char[] { '=' });
                    string name = lst[0];
                    int val = Int32.Parse(lst[1]);
                    intToString[val] = name;
                    stringToInt[name] = val;
                }
            }
        }
    }
    public class ASM
    {
        public List<string> instructions = new List<string>();
        public ASM()
        {
        }
        public ASM(params string[] _args)
        {
            foreach (string s in _args)
            {
                instructions.Add(s);
            }

        }
        public static ASM operator +(ASM a1, ASM a2)
        {
            ASM res = new ASM();
            res.instructions.AddRange(a1.instructions);
            res.instructions.AddRange(a2.instructions);
            return res;
        }
        public static implicit operator ASM(string s)
        {
            ASM res = new ASM();
            res.instructions.Add(s);
            return res;
        }
        public override string ToString()
        {
            //hacky: Try to make the indentation look nice
            string[] tmp = new string[instructions.Count];
            for (int i = 0; i < instructions.Count; ++i)
            {
                string instr = instructions[i];
                if (instr.Contains(":"))
                    tmp[i] = instr;
                else
                    tmp[i] = "    " + instr;
            }
            return String.Join("\n", tmp) + "\n";
        }
    }
    public class CodeStash : ParseTreeProperty<ASM>
    {
        public void Put(IParseTree context, params ASM[] asms)
        {
            ASM val = new ASM();
            foreach (ASM a in asms)
                val += a;
            base.Put(context, val);
        }
        public override ASM Get(IParseTree node)
        {
            ASM res = new ASM();
            if (base.Get(node) != null)
                return base.Get(node);
            else
            {
                for (int i = 0; i < node.ChildCount; ++i)
                {
                    var ch = node.GetChild(i);
                    res = res + this.Get(ch);
                }
            }
            return res;
        }
    }
    class CodeGenerator : ssuplBaseListener
    {
        public CodeStash code = new CodeStash();
        public override void ExitFactor(ssuplParser.FactorContext context)
        {
            //factor -> NUM
            int d = Int32.Parse(context.NUM().GetText());
            code.Put(context, $"push qword {d}");
}
        public override void ExitReturnStmt(ssuplParser.ReturnStmtContext context)
        {
            var regname = resultRegister.Get(context.expr());
            code.Put(context,
                code.Get(context.expr()),
                "pop rax",
                "ret");
        }
        int labelCounter = 0;
        string label()
        {
            string tmp = $"lbl{labelCounter}";
            labelCounter++;
            return tmp;
        }
        public override void ExitCondNoElse(ssuplParser.CondNoElseContext context)
        {
            string endif = label();
            code.Put(context,
                code.Get(context.expr()),
                "pop rax",
                "cmp rax,0",
                $"je {endif}",
                code.Get(context.braceblock()),
                $"{endif}:"
            );
        }
        public override void ExitCondElse(ssuplParser.CondElseContext context)
        {
            string _else = label();
            string _endelse = label();
            code.Put(context,
                $"; begin condElse at line {context.Start.Line}",
                code.Get(context.expr()),
                "pop rax",
                "cmp rax, 0",
                $"je {_else}",
                $"; begin IF braceblock",
                code.Get(context.braceblock()[0]),
                $"jmp {_endelse}",
                $"{_else}:",
                $"; begin ELSE braceblock",
                code.Get(context.braceblock()[1]),
                $"{_endelse}:",
                $"; end condElse at line {context.Stop.Line}"

            );
        }
        public override void ExitLoop(ssuplParser.LoopContext context)
        {
            string loop = label();
            string endloop = label();
            code.Put(context,
                $"{loop}:",
                $"; begin LOOP at line {context.Start.Line}",
                code.Get(context.expr()),
                "pop rax",
                $"cmp rax, 0",
                $"je {endloop}",
                $"; begin LOOP braceblock",
                code.Get(context.braceblock()),
                $"jmp {loop}",
                $"{endloop}:",
                $"; end LOOP");
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            Syms.init("ssupl.tokens");

            string gdata;
            using (var r = new StreamReader("terminals.txt"))
            {
                gdata = r.ReadToEnd();
            }
            gdata = gdata.Replace("\r", "\n");
            var tokenizer = new Tokenizer(gdata);

            //existing code
            string idata;
            using (var r = new StreamReader("input.txt"))
            {
                idata = r.ReadToEnd();
            }

            //new code

            //var rex = new Regex(@"\r");

            idata = idata.Replace("\r", "");
            var rex = new Regex(@"\n[ \t]+([^\n]+)");
            //need one leading space in replacement

            idata = rex.Replace(idata, " $1");

            idata += "\n";      //make sure file ends with newline

            tokenizer.setInput(idata);
            IList<IToken> tokens = new List<IToken>();
            while (true)
            {
                Token t = tokenizer.next();
                if (t.Symbol == "$")
                    break;  //at end
                            //CommonToken is defined in the ANTLR runtime
                CommonToken T = new CommonToken(Syms.stringToInt[t.Symbol], t.Lexeme);
                T.Line = t.Line;
                tokens.Add(T);
            }

            var antlrtokenizer = new BufferedTokenStream(new ListTokenSource(tokens));
            var parser = new ssuplParser(antlrtokenizer);
            parser.BuildParseTree = true;
            //optional: parser.ErrorHandler = new BailErrorStrategy ();
            //'start' should be the name of the grammar's start symbol
            var antlrroot = parser.start();

            var listener = new CodeGenerator();
            var walker = new ParseTreeWalker();
            walker.Walk(listener, antlrroot);
            //Console.WriteLine(listener.code.Get(antlrroot).ToString());
            var allcode = new ASM(
                "default rel",
                "section .text",
                "global main",
                "main:",
                listener.code.Get(antlrroot).ToString(),
                "ret",
                "section .data"
            );
            Console.WriteLine(allcode.ToString());
            using (var w = new StreamWriter("out.asm"))
            {
                w.Write(allcode.ToString());
            }
            //Console.ReadLine();
        }
    }
}
