//William Ayers

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace parser
{
    public class Token
    {
        public string Symbol;
        public string Lexeme;
        public int Line;
        public Token(string sym, string lexeme, int line)
        {
            this.Symbol = sym;
            this.Lexeme = lexeme;
            this.Line = line;
        }
        public string ToString()
        {
            return $"[{this.Symbol} {this.Line} {this.Lexeme}]";
        }
    }
    public class Terminal
    {
        public string sym;
        public Regex rex;
        public Terminal(string sym, Regex rex)
        {
            this.sym = sym;
            this.rex = rex;
        }
    }

    public class Tokenizer
    {
        int idx;
        string input;
        int line_number;
        List<Terminal> terminals = new List<Terminal>();
        //gdata = grammar data

        public Tokenizer(string gdata)
        {
            string[] lines = gdata.Split('\n');

            foreach (var line in lines)
            {
                if (line.Length != 0)
                {
                    //Console.WriteLine("line: " + line);
                    var split = line.Split(':', (char)2);
                    terminals.Add(new Terminal(split[0].Replace(" ", ""), 
                                  new Regex(@"\G" + split[1].Replace(" ", ""))));
                    //Console.Write(terminals.ToString());
                }
            }
        }

        public void setInput(string input)
        {
            this.idx = 0;
            this.input = input;
            this.line_number = 1;
        }

        public Token next()
        {
            if (idx >= input.Length - 1)
            {
                //return special "end of file" metatoken
                return new Token("$", "", line_number);
            }
            else
            {

                foreach (Terminal t in terminals)
                {
                    //Console.WriteLine(t.rex + "<-- T.rex is: " + t.sym);
                    if (input.Substring(this.idx, 1) == "\n")
                    {
                        this.idx++;
                        this.line_number++;
                        return next();
                    }
                    else if (t.rex.IsMatch(this.input, this.idx))
                    {
                        string s = t.rex.Match(this.input, this.idx).ToString();
                        //Console.WriteLine("String Generated: " + s);
                        this.idx += s.Length;
                        if (t.sym == "WHITESPACE" || t.sym == "COMMENT")
                        {
                            return next();
                        }
                        else
                        {
                            //Console.WriteLine(s + " is a " + t.sym);

                            Token newtok = new Token(t.sym, s, this.line_number);
                            /*Console.WriteLine("NEW TOKEN:\nnewtok.Symbol= " + newtok.Symbol
                                + "\nnewtok.Line= " + newtok.Line
                                + "\nnewtok.Lexeme= " + newtok.Lexeme);*/
                            
                            return newtok;
                        }
                    }

                }
            }
            //Report syntax error
            Console.WriteLine("SYNTAX ERROR:\n"
                + "idx= " + idx
                + "\ninput= " + input
                + "\nline_number" + line_number);
            throw new Exception("syntax error");
        }
    }
}
